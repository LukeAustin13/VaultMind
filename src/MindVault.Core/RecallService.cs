using System.Globalization;
using System.Text.RegularExpressions;

namespace MindVault.Core;

public sealed record RecallItem(string Title, string Path, string? Type, string? Status, string Date, string Change);

public sealed record RecallResult(
    string? Project, string Window,
    IReadOnlyList<RecallItem> Decisions,
    IReadOnlyList<RecallItem> Tasks,
    IReadOnlyList<RecallItem> Risks,
    IReadOnlyList<RecallItem> Mistakes,
    IReadOnlyList<RecallItem> Sessions,
    IReadOnlyList<RecallItem> Reviews,
    IReadOnlyList<RecallItem> Notes,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Time-window recall: what changed since a date (or "7 days", or on this day in earlier
/// years), grouped by note type. Frontmatter created/updated dates are authoritative; file
/// mtime is the fallback for notes without them. Archived notes are excluded by default
/// (counted in a warning), templates always.
/// </summary>
public sealed class RecallService(VaultContext ctx)
{
    private const int GroupCap = 20;

    public RecallResult Recall(string? project = null, string? since = null, bool onThisDay = false)
    {
        ctx.Scanner.EnsureFresh();
        var today = DateTime.Today;
        var cutoff = onThisDay ? DateTime.MinValue : ParseSince(since, today);
        var window = onThisDay ? $"on this day ({today:MM-dd}) in earlier years" : $"since {cutoff:yyyy-MM-dd}";

        string? projectTitle = null;
        string[]? names = null;
        long projId = -1;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project.Trim());
            projectTitle = proj.Title;
            names = ctx.ProjectDetect.QueryNamesFor(proj);
            projId = proj.Id;
        }

        var states = ctx.Db.GetFileStates();
        var archiveFolder = ctx.Config.DefaultArchiveFolder;
        var archivedInWindow = 0;
        var groups = new Dictionary<string, List<RecallItem>>(StringComparer.Ordinal)
        {
            ["decisions"] = [], ["tasks"] = [], ["risks"] = [], ["mistakes"] = [],
            ["sessions"] = [], ["reviews"] = [], ["notes"] = [],
        };

        foreach (var note in ctx.Db.GetAllNotes())
        {
            if (note.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase)) continue;
            if (names is not null && note.Id != projId &&
                !(note.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            var date = NoteDate(note, states);
            if (date is null) continue;
            var inWindow = onThisDay
                ? date.Value.Month == today.Month && date.Value.Day == today.Day && date.Value.Year < today.Year
                : date.Value >= cutoff;
            if (!inWindow) continue;

            var archived = string.Equals(note.Status, "archived", StringComparison.OrdinalIgnoreCase) ||
                           note.Path.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase);
            if (archived)
            {
                archivedInWindow++;
                continue;
            }

            var change = ParseDate(note.Created) is { } created && (onThisDay || created >= cutoff)
                ? "created"
                : "updated";
            var item = new RecallItem(note.Title, note.Path, note.Type, note.Status,
                date.Value.ToString("yyyy-MM-dd"), change);
            groups[GroupKey(note)].Add(item);
        }

        var warnings = new List<string>();
        if (archivedInWindow > 0)
            warnings.Add($"{archivedInWindow} archived note(s) also changed in this window (excluded by default)");
        foreach (var (key, list) in groups)
        {
            list.Sort((a, b) =>
            {
                var byDate = string.CompareOrdinal(b.Date, a.Date);
                return byDate != 0 ? byDate : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });
            if (list.Count > GroupCap)
            {
                warnings.Add($"{key}: showing {GroupCap} of {list.Count}");
                list.RemoveRange(GroupCap, list.Count - GroupCap);
            }
        }

        return new RecallResult(projectTitle, window,
            groups["decisions"], groups["tasks"], groups["risks"], groups["mistakes"],
            groups["sessions"], groups["reviews"], groups["notes"], warnings);
    }

    private static string GroupKey(NoteSummary note) => note.Type?.ToLowerInvariant() switch
    {
        "decision" => "decisions",
        "task" or "bug" or "feature" => "tasks",
        "risk" => "risks",
        "mistake" => "mistakes",
        "review" => "reviews",
        "memory" when note.Stem.StartsWith("Log - ", StringComparison.OrdinalIgnoreCase) => "sessions",
        _ => "notes",
    };

    private static DateTime? NoteDate(NoteSummary note, IReadOnlyDictionary<string, FileState> states)
    {
        if (ParseDate(note.Updated) is { } updated) return updated;
        if (ParseDate(note.Created) is { } created) return created;
        // Fallback: filesystem mtime from the index (no per-note disk access).
        return states.TryGetValue(note.Path, out var state)
            ? new DateTime(state.ModifiedTicks, DateTimeKind.Utc).Date
            : null;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d)
            ? d
            : null;

    internal static DateTime ParseSince(string? since, DateTime today)
    {
        if (string.IsNullOrWhiteSpace(since)) return today.AddDays(-7);
        var s = since.Trim();
        var days = Regex.Match(s, @"^(\d{1,4})\s*(?:d|day|days)$", RegexOptions.IgnoreCase);
        if (days.Success) return today.AddDays(-int.Parse(days.Groups[1].Value));
        if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return date;
        }
        throw new MindVaultException($"Could not parse since '{s}'. Use '7 days' or a yyyy-MM-dd date.");
    }
}
