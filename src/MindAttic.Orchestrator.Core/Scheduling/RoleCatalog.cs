using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>One role file's frontmatter, as the <c>roles</c> table keeps it (M.ORCH-T2066).</summary>
/// <param name="Name">Its name, lowercase-hyphenated, equal to the file name.</param>
/// <param name="Brief">Its description.</param>
/// <param name="Model">Its model; null for none or <c>inherit</c>.</param>
/// <param name="Effort">Its effort; null for none.</param>
/// <param name="Tools">Its tools, comma-separated; null for every tool.</param>
/// <param name="Takes">What it takes (its optional <c>takes</c> field), comma-separated; null for any unit.</param>
/// <param name="Source">The file it was read from, relative to the workspace.</param>
public sealed record RoleFile(string Name, string Brief, string? Model, string? Effort, string? Tools, string? Takes, string Source);

/// <summary>
/// Keeps the <c>roles</c> table (M.ORCH-T1742) in step with the role files under
/// <c>&lt;workspace&gt;\mindattic-agent-standard\shared\roles\*.md</c> (M.ORCH-T2066): each file's frontmatter (name, description,
/// tools, model, effort; the format in that folder's README, the parser <c>render-abilities.ps1</c>'s Read-Role) upserted by name
/// with its source, and a row read from a file that's gone dropped. Rows with no source (made by hand) are left alone, and a
/// missing or unreadable folder changes nothing. The daemon runs it at start and whenever the folder's <see cref="Fingerprint"/>
/// changes, so <see cref="Roles.ModelOf"/> and <c>orchestrator agents new</c> see the catalog.
/// </summary>
public static class RoleCatalog
{
    /// <summary>What a <see cref="Sync"/> did: the roles added, changed and dropped, and each file skipped with why.</summary>
    public sealed record Result(IReadOnlyList<string> Added, IReadOnlyList<string> Changed, IReadOnlyList<string> Dropped, IReadOnlyList<string> Skipped)
    {
        /// <summary>Whether the sync added, changed or dropped any role (skipped files don't count).</summary>
        public bool Any => Added.Count + Changed.Count + Dropped.Count > 0;

        /// <summary>One line for the log naming the roles added, updated, dropped and skipped; "no change" when there are none.</summary>
        public override string ToString()
        {
            var parts = new List<string>();
            if (Added.Count > 0) parts.Add("added " + string.Join(", ", Added));
            if (Changed.Count > 0) parts.Add("updated " + string.Join(", ", Changed));
            if (Dropped.Count > 0) parts.Add("dropped " + string.Join(", ", Dropped));
            if (Skipped.Count > 0) parts.Add("skipped " + string.Join("; ", Skipped));
            return parts.Count == 0 ? "no change" : string.Join("; ", parts);
        }
    }

    private static readonly string[] Models = ["inherit", "sonnet", "opus", "haiku", "fable"];
    private static readonly string[] Efforts = ["low", "medium", "high", "xhigh", "max"];
    private static readonly Regex Frontmatter = new(@"^---\r?\n(.*?)\r?\n---\r?\n(.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Field = new(@"^([A-Za-z0-9_-]+):\s*(.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex RoleName = new(@"^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant);

    /// <summary>The role files in <paramref name="folder"/>: every <c>*.md</c> but the README, by name.</summary>
    public static IReadOnlyList<string> Files(string folder) =>
        [.. Directory.EnumerateFiles(folder, "*.md")
            .Where(f => !Path.GetFileNameWithoutExtension(f).Equals("README", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// What changes when the role files do: each file's name, size and last write time. Null when the folder is missing or
    /// can't be read.
    /// </summary>
    public static string? Fingerprint(string workspaceRoot)
    {
        try
        {
            var folder = AgentBook.RolesFolder(workspaceRoot);
            if (!Directory.Exists(folder)) return null;
            var text = new StringBuilder(folder).Append('|');
            foreach (var file in Files(folder))
            {
                var info = new FileInfo(file);
                text.Append(info.Name).Append(':').Append(info.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('|');
            }
            return text.ToString();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// Reads role file <paramref name="text"/> (named <paramref name="fileName"/>, <paramref name="source"/> relative to the
    /// workspace): the role, or null and why it breaks the format's rules.
    /// </summary>
    public static (RoleFile? Role, string? Problem) Parse(string fileName, string text, string source)
    {
        var match = Frontmatter.Match(text);
        if (!match.Success) return (null, "no frontmatter");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in match.Groups[1].Value.Split('\n'))
        {
            var field = Field.Match(line.TrimEnd('\r'));
            if (!field.Success) continue;
            var value = field.Groups[2].Value.Trim();
            if (value.Length >= 2 && value[0] == '[' && value[^1] == ']') value = value[1..^1];
            else if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
            fields[field.Groups[1].Value] = value;
        }
        var name = fields.GetValueOrDefault("name", "");
        var file = Path.GetFileNameWithoutExtension(fileName);
        if (!RoleName.IsMatch(name)) return (null, $"name '{name}' must be lowercase-hyphenated");
        if (name != file) return (null, $"name '{name}' must equal the file name '{file}'");
        var brief = fields.GetValueOrDefault("description", "").Trim();
        if (brief.Length == 0) return (null, "description is required");
        string? tools = null;
        if (fields.TryGetValue("tools", out var toolText))
        {
            tools = List(toolText);
            if (tools is null) return (null, "tools is present but empty (omit it to allow every tool)");
        }
        var model = fields.TryGetValue("model", out var m) && m.Length > 0 ? m.ToLowerInvariant() : null;
        if (model is not null && !Models.Contains(model)) return (null, $"model '{model}' is not one of {string.Join("|", Models)}");
        if (model == "inherit") model = null;
        var effort = fields.TryGetValue("effort", out var e) && e.Length > 0 ? e.ToLowerInvariant() : null;
        if (effort is not null && !Efforts.Contains(effort)) return (null, $"effort '{effort}' is not one of {string.Join("|", Efforts)}");
        var takes = fields.TryGetValue("takes", out var t) ? List(t) : null;
        return (new RoleFile(name, brief, model, effort, tools, takes, source), null);
    }

    /// <summary>
    /// Brings <paramref name="db"/>'s <c>roles</c> table in step with the role files under <paramref name="workspaceRoot"/>; the
    /// dump is rewritten only when a row changed. Null when the folder is missing or can't be read (nothing is changed then).
    /// </summary>
    public static Result? Sync(OrchestratorDb db, string workspaceRoot, DateTime nowUtc)
    {
        var folder = AgentBook.RolesFolder(workspaceRoot);
        var read = new List<RoleFile>();
        var skipped = new List<string>();
        try
        {
            if (!Directory.Exists(folder)) return null;
            foreach (var file in Files(folder))
            {
                var source = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
                var (role, problem) = Parse(Path.GetFileName(file), File.ReadAllText(file), source);
                if (role is not null) read.Add(role);
                else skipped.Add($"{Path.GetFileName(file)}: {problem}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        var stamp = nowUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        List<string> added = [], changed = [], dropped = [];
        db.WriteDataIf((connection, tx) =>
        {
            var rows = Sql.Read(connection, tx, "SELECT name, brief, model, effort, tools, takes, source FROM roles;", Row)
                .ToDictionary(r => r.Name, StringComparer.Ordinal);
            foreach (var role in read)
            {
                if (rows.TryGetValue(role.Name, out var row) && row == role) continue;
                (row is null ? added : changed).Add(role.Name);
                Sql.Upsert(connection, tx, "roles", ["name"],
                    ("name", role.Name), ("brief", role.Brief), ("model", role.Model), ("effort", role.Effort), ("tools", role.Tools),
                    ("takes", role.Takes), ("source", role.Source), ("updated_utc", stamp));
            }
            var names = read.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
            // A file broken now is skipped, not gone: its row stays until the file is fixed or removed.
            var broken = skipped.Select(s => Path.GetFileNameWithoutExtension(s[..s.IndexOf(':')])).ToHashSet(StringComparer.Ordinal);
            foreach (var row in rows.Values)
            {
                if (row.Source.Length == 0 || names.Contains(row.Name) || broken.Contains(row.Name)) continue;
                Sql.Run(connection, tx, "DELETE FROM roles WHERE name = @n;", ("@n", row.Name));
                dropped.Add(row.Name);
            }
            return added.Count + changed.Count + dropped.Count > 0;
        });
        return new Result(added, changed, dropped, skipped);
    }

    private static string? List(string text)
    {
        var items = text.Split(',').Select(i => i.Trim().Trim('\'', '"').Trim()).Where(i => i.Length > 0).ToList();
        return items.Count == 0 ? null : string.Join(",", items);
    }

    private static RoleFile Row(DbDataReader r) => new(
        r.GetString(0), r.GetString(1), Sql.OptionalText(r, 2), Sql.OptionalText(r, 3), Sql.OptionalText(r, 4),
        Sql.OptionalText(r, 5), Sql.OptionalText(r, 6) ?? "");
}
