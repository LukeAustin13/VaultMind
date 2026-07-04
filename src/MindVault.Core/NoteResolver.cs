namespace MindVault.Core;

/// <summary>
/// Resolves a note reference (exact relative path, title, filename stem, slug or [[Wiki Link]])
/// to exactly one indexed note. Multiple matches raise <see cref="AmbiguousNoteRefException"/>
/// instead of guessing.
/// </summary>
public sealed class NoteResolver(VaultContext ctx)
{
    public NoteSummary Resolve(string noteRef)
    {
        if (string.IsNullOrWhiteSpace(noteRef))
            throw new MindVaultException("Empty note reference.");
        ctx.Scanner.EnsureFresh();

        var r = noteRef.Trim();
        if (r.StartsWith("[[") && r.EndsWith("]]") && r.Length > 4) r = r[2..^2];
        r = r.Split('|')[0].Split('#')[0].Trim();
        if (r.Length == 0)
            throw new MindVaultException($"Note reference '{noteRef}' is empty after normalization.");

        // 1. Exact relative path (with or without .md).
        var pathRef = r.Replace('\\', '/').TrimStart('/');
        var pathCandidates = pathRef.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? new[] { pathRef }
            : [pathRef + ".md", pathRef];
        foreach (var candidate in pathCandidates)
        {
            string abs;
            try { abs = PathGuard.ResolveNotePath(ctx.VaultRoot, candidate); }
            catch (UnsafePathException) { continue; }
            if (!File.Exists(abs)) continue;
            var rel = PathGuard.ToRelative(ctx.VaultRoot, abs);
            return ctx.Db.FindByPath(rel) ?? ctx.Scanner.IndexFile(abs);
        }

        // 2. Title, 3. filename stem, 4. slug. Template notes in 08_Templates are only
        // reachable by exact path so placeholder titles never shadow real notes.
        var slug = SlugHelper.Slugify(r);
        foreach (var candidates in new[]
                 {
                     ctx.Db.FindByTitle(r),
                     ctx.Db.FindByStem(r),
                     slug.Length == 0 ? new List<NoteSummary>() : ctx.Db.FindBySlug(slug),
                 })
        {
            var matches = candidates.Where(n => !IsTemplate(n)).ToList();
            if (matches.Count == 1) return matches[0];
            if (matches.Count > 1) throw Ambiguous(noteRef, matches);
        }

        // 5. Wiki-normalized title/stem (whitespace collapsed, case-insensitive).
        var norm = SlugHelper.NormalizeWiki(r);
        var normMatches = ctx.Db.GetAllNotes()
            .Where(n => !IsTemplate(n) &&
                        (SlugHelper.NormalizeWiki(n.Title) == norm || SlugHelper.NormalizeWiki(n.Stem) == norm))
            .ToList();
        if (normMatches.Count == 1) return normMatches[0];
        if (normMatches.Count > 1) throw Ambiguous(noteRef, normMatches);

        throw new NoteNotFoundException(noteRef);
    }

    private static bool IsTemplate(NoteSummary note) =>
        note.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase);

    public string AbsolutePathOf(NoteSummary note) =>
        PathGuard.ResolveNotePath(ctx.VaultRoot, note.Path);

    private static AmbiguousNoteRefException Ambiguous(string noteRef, List<NoteSummary> matches) =>
        new(noteRef, matches.Select(m => m.Path).ToList());
}
