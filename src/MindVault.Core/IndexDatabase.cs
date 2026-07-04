using Microsoft.Data.Sqlite;

namespace MindVault.Core;

public sealed record NoteSummary(
    long Id, string Path, string Title, string Stem, string Slug,
    string? Type, string? Status, string? Project, string? Created, string? Updated,
    bool HasFrontmatter, string? ParseError);

public sealed record SearchResult(
    string Title, string Path, string? Type, string? Project, string? Status,
    string Snippet, double Score, string? Section = null, string? Scope = null,
    IReadOnlyList<string>? Why = null, string? Caution = null);

/// <summary>
/// Raw FTS candidate row before ranking policy is applied. Snippets are fetched separately
/// for the surviving page only — computing them for the whole candidate pool wastes work.
/// </summary>
public sealed record SearchCandidate(
    long Id, string Title, string Path, string? Type, string? Project, string? Status,
    string? Updated, double Bm25);

public sealed record FrontmatterValueRow(long NoteId, string NotePath, string Value);

public sealed record FileState(long ModifiedTicks, long Size, string BodyHash);

public sealed record NoteLinkRow(long NoteId, string NotePath, string Target, string TargetNorm);

/// <summary>
/// The rebuildable SQLite cache. Markdown files remain canonical; everything in here can be
/// regenerated with `rebuild-index`. All SQL is parameterised. A single connection is shared
/// and every public operation is serialised with a lock so concurrent MCP tool calls (HTTP
/// transport) cannot interleave commands or transactions.
/// </summary>
public sealed class IndexDatabase : IDisposable
{
    /// <summary>Bump when tables or the FTS tokenizer change; mismatched indexes are reset and rescanned.</summary>
    public const int CurrentSchemaVersion = 2;

    private readonly SqliteConnection _conn;
    private readonly object _lock = new();

    /// <summary>Ambient transaction for bulk operations (scans); see <see cref="BeginBulk"/>.</summary>
    private SqliteTransaction? _bulk;

    /// <summary>True when the schema was reset (new version); the caller must run a scan to repopulate.</summary>
    public bool NeedsRescan { get; private set; }

    public void MarkRescanned() => NeedsRescan = false;

    public IndexDatabase(string dbPath)
    {
        _conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false,
        }.ToString());
        _conn.Open();
        Exec("PRAGMA journal_mode=WAL;");
        // The index is disposable cache (Markdown is canonical), so NORMAL durability is the
        // right trade: no corruption risk under WAL, and commits stop fsync-ing — which is the
        // difference between seconds and minutes for a cold scan on Raspberry Pi SD storage.
        Exec("PRAGMA synchronous=NORMAL;");

        var version = Convert.ToInt64(Scalar("PRAGMA user_version;"));
        if (version != CurrentSchemaVersion)
        {
            var hadTables = Convert.ToInt64(Scalar(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'notes'")) > 0;
            if (hadTables)
            {
                Exec("DROP TABLE IF EXISTS notes; DROP TABLE IF EXISTS note_tags; " +
                     "DROP TABLE IF EXISTS note_links; DROP TABLE IF EXISTS note_headings; " +
                     "DROP TABLE IF EXISTS note_frontmatter; DROP TABLE IF EXISTS fts_notes;");
                NeedsRescan = true;
            }
            EnsureSchema();
            Exec($"PRAGMA user_version = {CurrentSchemaVersion};");
        }
        else
        {
            EnsureSchema();
        }
    }

    private void EnsureSchema()
    {
        Exec("""
            CREATE TABLE IF NOT EXISTS notes(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT NOT NULL UNIQUE,
                title TEXT NOT NULL,
                stem TEXT NOT NULL,
                slug TEXT NOT NULL,
                type TEXT,
                status TEXT,
                project TEXT,
                created TEXT,
                updated TEXT,
                body_hash TEXT NOT NULL,
                modified_ticks INTEGER NOT NULL,
                file_size INTEGER NOT NULL,
                has_frontmatter INTEGER NOT NULL DEFAULT 0,
                parse_error TEXT);
            CREATE INDEX IF NOT EXISTS idx_notes_title ON notes(title COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS idx_notes_stem ON notes(stem COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS idx_notes_slug ON notes(slug);
            CREATE INDEX IF NOT EXISTS idx_notes_type ON notes(type);
            CREATE INDEX IF NOT EXISTS idx_notes_project ON notes(project COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS note_tags(note_id INTEGER NOT NULL, tag TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_note_tags ON note_tags(note_id);
            CREATE TABLE IF NOT EXISTS note_links(note_id INTEGER NOT NULL, target TEXT NOT NULL, target_norm TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_note_links ON note_links(note_id);
            CREATE INDEX IF NOT EXISTS idx_note_links_target ON note_links(target_norm);
            CREATE TABLE IF NOT EXISTS note_headings(note_id INTEGER NOT NULL, level INTEGER NOT NULL, heading TEXT NOT NULL, line INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_note_headings ON note_headings(note_id);
            CREATE TABLE IF NOT EXISTS note_frontmatter(note_id INTEGER NOT NULL, key TEXT NOT NULL, value TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_note_frontmatter ON note_frontmatter(note_id);
            CREATE INDEX IF NOT EXISTS idx_note_frontmatter_key ON note_frontmatter(key);
            CREATE VIRTUAL TABLE IF NOT EXISTS fts_notes
                USING fts5(title, body, note_id UNINDEXED, tokenize = 'porter unicode61');
            """);
    }

    public void Clear()
    {
        lock (_lock)
        {
            Exec("DELETE FROM notes; DELETE FROM note_tags; DELETE FROM note_links; " +
                 "DELETE FROM note_headings; DELETE FROM note_frontmatter; DELETE FROM fts_notes;");
        }
    }

    /// <summary>
    /// Groups many upserts/deletes into ONE SQLite transaction (one commit instead of one per
    /// note — the difference between a scan being CPU-bound and being fsync-bound). Commands
    /// issued while the scope is open automatically join the transaction; disposing commits.
    /// Partial commit on failure is safe: per-note writes are atomic (each note's rows are
    /// written under the same lock hold), matching the old per-note-commit semantics.
    /// </summary>
    public IDisposable BeginBulk()
    {
        lock (_lock)
        {
            if (_bulk is not null)
                throw new InvalidOperationException("A bulk scope is already open.");
            _bulk = _conn.BeginTransaction();
        }
        return new BulkScope(this);
    }

    private void EndBulk()
    {
        lock (_lock)
        {
            if (_bulk is null) return;
            try
            {
                _bulk.Commit();
            }
            finally
            {
                _bulk.Dispose();
                _bulk = null;
            }
        }
    }

    private sealed class BulkScope(IndexDatabase owner) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            owner.EndBulk();
        }
    }

    public long UpsertNote(ParsedNote note)
    {
        lock (_lock)
        {
            // Join an open bulk scope instead of nesting a transaction inside it.
            var ownTx = _bulk is null ? _conn.BeginTransaction() : null;
            var tx = ownTx ?? _bulk!;
            try
            {
                var id = UpsertNoteCore(note, tx);
                ownTx?.Commit();
                return id;
            }
            finally
            {
                ownTx?.Dispose();
            }
        }
    }

    private long UpsertNoteCore(ParsedNote note, SqliteTransaction tx)
    {
        long id;
        using (var find = Cmd("SELECT id FROM notes WHERE path = $path", tx))
        {
            find.Parameters.AddWithValue("$path", note.RelativePath);
            var existing = find.ExecuteScalar();
            id = existing is null ? 0 : (long)existing;
        }

        if (id == 0)
        {
            using var insert = Cmd("""
                INSERT INTO notes(path, title, stem, slug, type, status, project, created, updated,
                                  body_hash, modified_ticks, file_size, has_frontmatter, parse_error)
                VALUES($path, $title, $stem, $slug, $type, $status, $project, $created, $updated,
                       $hash, $ticks, $size, $hasfm, $err);
                SELECT last_insert_rowid();
                """, tx);
            AddNoteParams(insert, note);
            id = (long)insert.ExecuteScalar()!;
        }
        else
        {
            using var update = Cmd("""
                UPDATE notes SET title=$title, stem=$stem, slug=$slug, type=$type, status=$status,
                       project=$project, created=$created, updated=$updated, body_hash=$hash,
                       modified_ticks=$ticks, file_size=$size, has_frontmatter=$hasfm, parse_error=$err
                WHERE path=$path
                """, tx);
            AddNoteParams(update, note);
            update.ExecuteNonQuery();
            DeleteChildren(id, tx);
        }

        foreach (var tag in note.Tags)
            ExecTx(tx, "INSERT INTO note_tags(note_id, tag) VALUES($id, $tag)",
                ("$id", id), ("$tag", tag));
        foreach (var link in note.Links)
            ExecTx(tx, "INSERT INTO note_links(note_id, target, target_norm) VALUES($id, $t, $n)",
                ("$id", id), ("$t", link.Target), ("$n", link.TargetNorm));
        foreach (var heading in note.Headings)
            ExecTx(tx, "INSERT INTO note_headings(note_id, level, heading, line) VALUES($id, $l, $h, $line)",
                ("$id", id), ("$l", heading.Level), ("$h", heading.Text), ("$line", heading.Line));
        foreach (var entry in note.FrontmatterEntries)
            ExecTx(tx, "INSERT INTO note_frontmatter(note_id, key, value) VALUES($id, $k, $v)",
                ("$id", id), ("$k", entry.Key.ToLowerInvariant()),
                ("$v", entry.IsList ? Json.Serialize(entry.Items) : entry.Scalar ?? ""));
        ExecTx(tx, "INSERT INTO fts_notes(title, body, note_id) VALUES($t, $b, $id)",
            ("$t", note.Title), ("$b", note.Body), ("$id", id.ToString()));

        return id;
    }

    public void DeleteNoteByPath(string relativePath)
    {
        lock (_lock)
        {
            var ownTx = _bulk is null ? _conn.BeginTransaction() : null;
            var tx = ownTx ?? _bulk!;
            try
            {
                using var find = Cmd("SELECT id FROM notes WHERE path = $path", tx);
                find.Parameters.AddWithValue("$path", relativePath);
                if (find.ExecuteScalar() is long id)
                {
                    DeleteChildren(id, tx);
                    ExecTx(tx, "DELETE FROM notes WHERE id = $id", ("$id", id));
                }
                ownTx?.Commit();
            }
            finally
            {
                ownTx?.Dispose();
            }
        }
    }

    private void DeleteChildren(long id, SqliteTransaction tx)
    {
        ExecTx(tx, "DELETE FROM note_tags WHERE note_id = $id", ("$id", id));
        ExecTx(tx, "DELETE FROM note_links WHERE note_id = $id", ("$id", id));
        ExecTx(tx, "DELETE FROM note_headings WHERE note_id = $id", ("$id", id));
        ExecTx(tx, "DELETE FROM note_frontmatter WHERE note_id = $id", ("$id", id));
        ExecTx(tx, "DELETE FROM fts_notes WHERE note_id = $id", ("$id", id.ToString()));
    }

    public Dictionary<string, FileState> GetFileStates()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
            using var cmd = Cmd("SELECT path, modified_ticks, file_size, body_hash FROM notes");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result[reader.GetString(0)] =
                    new FileState(reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3));
            return result;
        }
    }

    public int CountNotes()
    {
        lock (_lock)
        {
            return Convert.ToInt32(Scalar("SELECT COUNT(*) FROM notes"));
        }
    }

    public int CountFtsRows()
    {
        lock (_lock)
        {
            return Convert.ToInt32(Scalar("SELECT COUNT(*) FROM fts_notes"));
        }
    }

    /// <summary>The schema version stored in the file (PRAGMA user_version).</summary>
    public int UserVersion
    {
        get
        {
            lock (_lock)
            {
                return Convert.ToInt32(Scalar("PRAGMA user_version;"));
            }
        }
    }

    public NoteSummary? FindByPath(string relativePath) =>
        QueryOne("SELECT * FROM notes WHERE path = $v COLLATE NOCASE", relativePath);

    public List<NoteSummary> FindByTitle(string title) =>
        QueryMany("SELECT * FROM notes WHERE title = $v COLLATE NOCASE ORDER BY path", title);

    public List<NoteSummary> FindByStem(string stem) =>
        QueryMany("SELECT * FROM notes WHERE stem = $v COLLATE NOCASE ORDER BY path", stem);

    public List<NoteSummary> FindBySlug(string slug) =>
        string.IsNullOrEmpty(slug)
            ? []
            : QueryMany("SELECT * FROM notes WHERE slug = $v ORDER BY path", slug);

    /// <summary>Project notes matching a name by title or stem, template notes excluded.</summary>
    public List<NoteSummary> FindProjects(string name) =>
        QueryMany("""
            SELECT * FROM notes
            WHERE type = 'project'
              AND (title = $v COLLATE NOCASE OR stem = $v COLLATE NOCASE)
              AND path NOT LIKE '08!_Templates/%' ESCAPE '!'
            ORDER BY path
            """, name);

    public List<NoteSummary> GetAllNotes()
    {
        lock (_lock)
        {
            using var cmd = Cmd("SELECT * FROM notes ORDER BY path");
            return ReadSummaries(cmd);
        }
    }

    /// <summary>Filtered note listing. All filters are optional and combined with AND.</summary>
    public List<NoteSummary> Query(
        string? type = null, string[]? projectNames = null, string[]? statusIn = null,
        string? statusNot = null, string? tag = null, long? excludeId = null, int limit = 100)
    {
        lock (_lock)
        {
            // Predicates are written to stay sargable: `type` is stored lowercased (compare to a
            // lowered constant) and `project` compares under COLLATE NOCASE, so the planner can
            // use idx_notes_type / idx_notes_project instead of scanning the whole table.
            var sql = "SELECT * FROM notes WHERE 1=1";
            using var cmd = Cmd("");
            if (type is not null)
            {
                sql += " AND type = lower($type)";
                cmd.Parameters.AddWithValue("$type", type);
            }
            if (projectNames is { Length: > 0 })
            {
                var names = projectNames.Select((p, i) => $"$proj{i}").ToArray();
                sql += $" AND project COLLATE NOCASE IN ({string.Join(", ", names)})";
                for (var i = 0; i < projectNames.Length; i++)
                    cmd.Parameters.AddWithValue($"$proj{i}", projectNames[i]);
            }
            if (statusIn is { Length: > 0 })
            {
                var names = statusIn.Select((s, i) => $"$st{i}").ToArray();
                sql += $" AND status COLLATE NOCASE IN ({string.Join(", ", names)})";
                for (var i = 0; i < statusIn.Length; i++)
                    cmd.Parameters.AddWithValue($"$st{i}", statusIn[i]);
            }
            if (statusNot is not null)
            {
                sql += " AND (status IS NULL OR status COLLATE NOCASE != $stnot)";
                cmd.Parameters.AddWithValue("$stnot", statusNot);
            }
            if (tag is not null)
            {
                sql += " AND EXISTS (SELECT 1 FROM note_tags t WHERE t.note_id = notes.id AND t.tag COLLATE NOCASE = $tag)";
                cmd.Parameters.AddWithValue("$tag", tag);
            }
            if (excludeId is not null)
            {
                sql += " AND id != $exid";
                cmd.Parameters.AddWithValue("$exid", excludeId.Value);
            }
            sql += " ORDER BY COALESCE(updated, created) DESC, title COLLATE NOCASE LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.CommandText = sql;
            return ReadSummaries(cmd);
        }
    }

    /// <summary>
    /// FTS candidate fetch with title-weighted bm25 (title x4, body x1). Ranking policy
    /// (recency, archived penalty, title/exact boosts) lives in SearchService; this returns
    /// the raw candidate pool WITHOUT snippets — fetch those for the surviving page via
    /// <see cref="GetSnippets"/> using the returned effective match string.
    /// </summary>
    public (List<SearchCandidate> Candidates, string Match) SearchCandidates(string query,
        string? type = null, string? project = null, string? tag = null, string? status = null,
        string? updatedAfter = null, string? updatedBefore = null,
        bool includeArchived = false, string archiveFolder = "99_Archive", int limit = 40)
    {
        lock (_lock)
        {
            try
            {
                return (SearchCandidatesFts(query, type, project, tag, status,
                    updatedAfter, updatedBefore, includeArchived, archiveFolder, limit), query);
            }
            catch (SqliteException)
            {
                // User text was not valid FTS5 syntax; retry as a quoted phrase.
                var quoted = "\"" + query.Replace("\"", "\"\"") + "\"";
                return (SearchCandidatesFts(quoted, type, project, tag, status,
                    updatedAfter, updatedBefore, includeArchived, archiveFolder, limit), quoted);
            }
        }
    }

    /// <summary>Highlighted snippets for specific notes under an FTS match — page-sized, not pool-sized.</summary>
    public Dictionary<long, string> GetSnippets(string match, IReadOnlyCollection<long> noteIds)
    {
        lock (_lock)
        {
            var result = new Dictionary<long, string>();
            if (noteIds.Count == 0) return result;
            using var cmd = Cmd("");
            var names = noteIds.Select((_, i) => $"$id{i}").ToArray();
            cmd.CommandText =
                "SELECT CAST(note_id AS INTEGER), snippet(fts_notes, 1, '**', '**', ' … ', 12) " +
                $"FROM fts_notes WHERE fts_notes MATCH $q AND note_id IN ({string.Join(", ", names)})";
            cmd.Parameters.AddWithValue("$q", match);
            var i = 0;
            foreach (var id in noteIds)
                cmd.Parameters.AddWithValue($"$id{i++}", id.ToString());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result[reader.GetInt64(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
            return result;
        }
    }

    private List<SearchCandidate> SearchCandidatesFts(string match, string? type, string? project,
        string? tag, string? status, string? updatedAfter, string? updatedBefore,
        bool includeArchived, string archiveFolder, int limit)
    {
        var sql = """
            SELECT n.id, n.title, n.path, n.type, n.project, n.status, n.updated,
                   bm25(fts_notes, 4.0, 1.0, 0.0) AS score
            FROM fts_notes
            JOIN notes n ON n.id = CAST(fts_notes.note_id AS INTEGER)
            WHERE fts_notes MATCH $q
              AND n.path NOT LIKE '08!_Templates/%' ESCAPE '!'
            """;
        using var cmd = Cmd("");
        cmd.Parameters.AddWithValue("$q", match);
        if (type is not null) { sql += " AND n.type = lower($type)"; cmd.Parameters.AddWithValue("$type", type); }
        if (project is not null) { sql += " AND n.project COLLATE NOCASE = $proj"; cmd.Parameters.AddWithValue("$proj", project); }
        if (status is not null) { sql += " AND n.status COLLATE NOCASE = $status"; cmd.Parameters.AddWithValue("$status", status); }
        if (tag is not null)
        {
            sql += " AND EXISTS (SELECT 1 FROM note_tags t WHERE t.note_id = n.id AND t.tag COLLATE NOCASE = $tag)";
            cmd.Parameters.AddWithValue("$tag", tag);
        }
        if (updatedAfter is not null) { sql += " AND n.updated >= $ua"; cmd.Parameters.AddWithValue("$ua", updatedAfter); }
        if (updatedBefore is not null) { sql += " AND n.updated <= $ub"; cmd.Parameters.AddWithValue("$ub", updatedBefore); }
        if (!includeArchived)
        {
            sql += " AND NOT (lower(COALESCE(n.status, '')) = 'archived' OR n.path LIKE $arch ESCAPE '!')";
            cmd.Parameters.AddWithValue("$arch", archiveFolder.Replace("_", "!_") + "/%");
        }
        sql += " ORDER BY score LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.CommandText = sql;

        var results = new List<SearchCandidate>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SearchCandidate(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? 0 : reader.GetDouble(7)));
        }
        return results;
    }

    public List<HeadingInfo> GetHeadings(long noteId)
    {
        lock (_lock)
        {
            var result = new List<HeadingInfo>();
            using var cmd = Cmd("SELECT level, heading, line FROM note_headings WHERE note_id = $id ORDER BY line");
            cmd.Parameters.AddWithValue("$id", noteId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new HeadingInfo(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
            return result;
        }
    }

    /// <summary>Body text as indexed (frontmatter stripped) — used for matched-section lookup.</summary>
    public string? GetFtsBody(long noteId)
    {
        lock (_lock)
        {
            using var cmd = Cmd("SELECT body FROM fts_notes WHERE note_id = $id");
            cmd.Parameters.AddWithValue("$id", noteId.ToString());
            return cmd.ExecuteScalar() as string;
        }
    }

    /// <summary>All (note, value) pairs for a frontmatter key. List values are JSON arrays.</summary>
    public List<FrontmatterValueRow> GetFrontmatterValues(string key)
    {
        lock (_lock)
        {
            var result = new List<FrontmatterValueRow>();
            using var cmd = Cmd("""
                SELECT f.note_id, n.path, f.value FROM note_frontmatter f
                JOIN notes n ON n.id = f.note_id
                WHERE f.key = $k
                """);
            cmd.Parameters.AddWithValue("$k", key.ToLowerInvariant());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new FrontmatterValueRow(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            return result;
        }
    }

    /// <summary>
    /// (note_id, key, value) rows for alias-bearing frontmatter keys of project notes.
    /// Values are raw as indexed: JSON arrays for YAML lists, plain text for scalars.
    /// </summary>
    public List<(long NoteId, string Key, string Value)> GetProjectAliasRows()
    {
        lock (_lock)
        {
            var result = new List<(long, string, string)>();
            using var cmd = Cmd("""
                SELECT f.note_id, f.key, f.value FROM note_frontmatter f
                JOIN notes n ON n.id = f.note_id
                WHERE n.type = 'project'
                  AND f.key IN ('aliases', 'reponames')
                  AND n.path NOT LIKE '08!_Templates/%' ESCAPE '!'
                """);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            return result;
        }
    }

    public List<(string Path, long Size)> GetLargeNotes(long minBytes)
    {
        lock (_lock)
        {
            var result = new List<(string, long)>();
            using var cmd = Cmd("SELECT path, file_size FROM notes WHERE file_size >= $min ORDER BY file_size DESC");
            cmd.Parameters.AddWithValue("$min", minBytes);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add((reader.GetString(0), reader.GetInt64(1)));
            return result;
        }
    }

    public HashSet<long> GetNoteIdsWithFrontmatterKey(string key)
    {
        lock (_lock)
        {
            var result = new HashSet<long>();
            using var cmd = Cmd("SELECT note_id FROM note_frontmatter WHERE key = $k");
            cmd.Parameters.AddWithValue("$k", key.ToLowerInvariant());
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetInt64(0));
            return result;
        }
    }

    /// <summary>Presence sets for several frontmatter keys in one round trip (validation hot path).</summary>
    public Dictionary<string, HashSet<long>> GetFrontmatterKeyPresence(IReadOnlyList<string> keys)
    {
        lock (_lock)
        {
            var result = keys.ToDictionary(k => k, _ => new HashSet<long>(), StringComparer.OrdinalIgnoreCase);
            if (keys.Count == 0) return result;
            using var cmd = Cmd("");
            var names = keys.Select((_, i) => $"$k{i}").ToArray();
            cmd.CommandText = $"SELECT key, note_id FROM note_frontmatter WHERE key IN ({string.Join(", ", names)})";
            for (var i = 0; i < keys.Count; i++)
                cmd.Parameters.AddWithValue($"$k{i}", keys[i].ToLowerInvariant());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (result.TryGetValue(reader.GetString(0), out var set))
                    set.Add(reader.GetInt64(1));
            }
            return result;
        }
    }

    /// <summary>Outgoing links of one note (indexed by note_id) — avoids loading the whole link table.</summary>
    public List<NoteLinkRow> GetLinksFor(long noteId)
    {
        lock (_lock)
        {
            var result = new List<NoteLinkRow>();
            using var cmd = Cmd("""
                SELECT l.note_id, n.path, l.target, l.target_norm FROM note_links l
                JOIN notes n ON n.id = l.note_id
                WHERE l.note_id = $id
                """);
            cmd.Parameters.AddWithValue("$id", noteId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new NoteLinkRow(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            return result;
        }
    }

    /// <summary>Just (title, stem) for every note — link-resolution needs nothing heavier.</summary>
    public List<(string Title, string Stem)> GetAllTitleStems()
    {
        lock (_lock)
        {
            var result = new List<(string, string)>();
            using var cmd = Cmd("SELECT title, stem FROM notes");
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add((reader.GetString(0), reader.GetString(1)));
            return result;
        }
    }

    public List<NoteLinkRow> GetAllLinks()
    {
        lock (_lock)
        {
            var result = new List<NoteLinkRow>();
            using var cmd = Cmd("SELECT l.note_id, n.path, l.target, l.target_norm FROM note_links l JOIN notes n ON n.id = l.note_id");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new NoteLinkRow(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            return result;
        }
    }

    /// <summary>Tags of one note (indexed by note_id), sorted for determinism.</summary>
    public List<string> GetTagsFor(long noteId)
    {
        lock (_lock)
        {
            var result = new List<string>();
            using var cmd = Cmd("SELECT tag FROM note_tags WHERE note_id = $id ORDER BY tag");
            cmd.Parameters.AddWithValue("$id", noteId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }
    }

    public List<string> GetBacklinkPaths(string titleNorm, string stemNorm, long selfId)
    {
        lock (_lock)
        {
            var result = new List<string>();
            using var cmd = Cmd("""
                SELECT DISTINCT n.path FROM note_links l
                JOIN notes n ON n.id = l.note_id
                WHERE l.target_norm IN ($a, $b) AND n.id != $self
                ORDER BY n.path
                """);
            cmd.Parameters.AddWithValue("$a", titleNorm);
            cmd.Parameters.AddWithValue("$b", stemNorm);
            cmd.Parameters.AddWithValue("$self", selfId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }
    }

    private static NoteSummary Map(SqliteDataReader r) => new(
        r.GetInt64(r.GetOrdinal("id")),
        r.GetString(r.GetOrdinal("path")),
        r.GetString(r.GetOrdinal("title")),
        r.GetString(r.GetOrdinal("stem")),
        r.GetString(r.GetOrdinal("slug")),
        r.IsDBNull(r.GetOrdinal("type")) ? null : r.GetString(r.GetOrdinal("type")),
        r.IsDBNull(r.GetOrdinal("status")) ? null : r.GetString(r.GetOrdinal("status")),
        r.IsDBNull(r.GetOrdinal("project")) ? null : r.GetString(r.GetOrdinal("project")),
        r.IsDBNull(r.GetOrdinal("created")) ? null : r.GetString(r.GetOrdinal("created")),
        r.IsDBNull(r.GetOrdinal("updated")) ? null : r.GetString(r.GetOrdinal("updated")),
        r.GetInt64(r.GetOrdinal("has_frontmatter")) != 0,
        r.IsDBNull(r.GetOrdinal("parse_error")) ? null : r.GetString(r.GetOrdinal("parse_error")));

    private static void AddNoteParams(SqliteCommand cmd, ParsedNote note)
    {
        cmd.Parameters.AddWithValue("$path", note.RelativePath);
        cmd.Parameters.AddWithValue("$title", note.Title);
        cmd.Parameters.AddWithValue("$stem", note.Stem);
        cmd.Parameters.AddWithValue("$slug", note.Slug);
        cmd.Parameters.AddWithValue("$type", (object?)note.Type ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (object?)note.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$project", (object?)note.Project ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", (object?)note.Created ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$updated", (object?)note.Updated ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hash", note.BodyHash);
        cmd.Parameters.AddWithValue("$ticks", note.ModifiedTicks);
        cmd.Parameters.AddWithValue("$size", note.FileSize);
        cmd.Parameters.AddWithValue("$hasfm", note.HasFrontmatter ? 1 : 0);
        cmd.Parameters.AddWithValue("$err", (object?)note.ParseError ?? DBNull.Value);
    }

    private NoteSummary? QueryOne(string sql, string value)
    {
        lock (_lock)
        {
            using var cmd = Cmd(sql);
            cmd.Parameters.AddWithValue("$v", value);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }
    }

    private List<NoteSummary> QueryMany(string sql, string value)
    {
        lock (_lock)
        {
            using var cmd = Cmd(sql);
            cmd.Parameters.AddWithValue("$v", value);
            return ReadSummaries(cmd);
        }
    }

    private static List<NoteSummary> ReadSummaries(SqliteCommand cmd)
    {
        var result = new List<NoteSummary>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(Map(reader));
        return result;
    }

    private SqliteCommand Cmd(string sql, SqliteTransaction? tx = null)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        // While a bulk scope is open, every command on this connection must join its
        // transaction (Microsoft.Data.Sqlite refuses transaction-less commands otherwise).
        cmd.Transaction = tx ?? _bulk;
        return cmd;
    }

    private void Exec(string sql)
    {
        using var cmd = Cmd(sql);
        cmd.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var cmd = Cmd(sql);
        return cmd.ExecuteScalar();
    }

    private void ExecTx(SqliteTransaction tx, string sql, params (string Name, object Value)[] args)
    {
        using var cmd = Cmd(sql, tx);
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _conn.Dispose();
        }
    }
}
