using System.Net.Http.Headers;
using System.Text.Json;
using MindAttic.Vault.Credentials;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>A project on the Launcher's roster: its name, folder and GitHub remote.</summary>
public sealed record RosterProject(string Name, string Path, string? RepoUrl);

/// <summary>A row of the project tree menu: a branch of dot-named projects sharing a segment, or a project.</summary>
public abstract record ProjectNode(string Label);

public sealed record ProjectLeaf(string Label, RosterProject Project) : ProjectNode(Label);

public sealed record ProjectBranch(string Label, IReadOnlyList<ProjectNode> Children) : ProjectNode(Label);

/// <summary>
/// The Launcher's "Open Project Tab" roster, read in-process (M.ORCH-T1585) so the dashboard can list it without a
/// MindAttic.Launcher.exe per login: the projects in the Launcher's settings.json, kept to the repos tagged
/// <see cref="Topic"/> on GitHub the way the Launcher's own ProjectRoster/TopicRepoSync do, grouped on '.' the way its
/// ProjectTree does. With no GitHub token, or GitHub unreachable, the roster is shown unfiltered rather than emptied.
/// </summary>
public static class ProjectRoster
{
    public const string Topic = "wip";

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Launcher", "settings.json");

    /// <summary>The Launcher's projects, deduplicated and sorted by name; empty when its settings can't be read.</summary>
    public static IReadOnlyList<RosterProject> Load(string? settingsPath = null)
    {
        var file = settingsPath ?? SettingsPath;
        var projects = new List<RosterProject>();
        try
        {
            if (!File.Exists(file)) return projects;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (Property(doc.RootElement, "projects") is { ValueKind: JsonValueKind.Array } list)
                foreach (var p in list.EnumerateArray())
                    if (Property(p, "name")?.GetString() is { Length: > 0 } name && Property(p, "path")?.GetString() is { Length: > 0 } path)
                        projects.Add(new RosterProject(name, path, Property(p, "repoUrl")?.GetString()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        return Sorted(projects);
    }

    /// <summary><paramref name="projects"/> kept to those whose GitHub remote is in <paramref name="tagged"/>; all of them when it's null.</summary>
    public static IReadOnlyList<RosterProject> Filter(IReadOnlyList<RosterProject> projects, IReadOnlySet<string>? tagged) =>
        tagged is null ? projects : projects.Where(p => GitHubFullName(p.RepoUrl) is { } full && tagged.Contains(full)).ToList();

    /// <summary>The roster as the Launcher shows it: <see cref="Load"/> filtered by the repos tagged <see cref="Topic"/>.</summary>
    public static async Task<IReadOnlyList<RosterProject>> LoadTaggedAsync(CancellationToken ct = default)
    {
        var projects = Load();
        var token = TokenStore.ForBucket("GitHub").Get("github");
        if (string.IsNullOrWhiteSpace(token)) return projects;
        try { return Filter(projects, await FetchTaggedAsync(token, Http.Value, ct).ConfigureAwait(false)); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException) { return projects; }
    }

    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var client = new HttpClient { BaseAddress = new Uri("https://api.github.com/"), Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MindAttic.Orchestrator");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    });

    /// <summary>The lower-cased owner/repo of every unarchived repo the token sees that carries <see cref="Topic"/>.</summary>
    internal static async Task<IReadOnlySet<string>> FetchTaggedAsync(string token, HttpClient http, CancellationToken ct)
    {
        const int pageSize = 100;
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"user/repos?per_page={pageSize}&page={page}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"GitHub returned {(int)response.StatusCode} listing repos.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var count = 0;
            foreach (var repo in doc.RootElement.EnumerateArray())
            {
                count++;
                if (repo.TryGetProperty("archived", out var archived) && archived.ValueKind == JsonValueKind.True) continue;
                if (!repo.TryGetProperty("full_name", out var full) || full.GetString() is not { } name) continue;
                if (repo.TryGetProperty("topics", out var topics) && topics.ValueKind == JsonValueKind.Array
                    && topics.EnumerateArray().Any(t => string.Equals(t.GetString(), Topic, StringComparison.OrdinalIgnoreCase)))
                    result.Add(name.ToLowerInvariant());
            }
            if (count < pageSize) return result;
        }
    }

    /// <summary>The lower-cased owner/repo of a github.com remote (https or ssh), else null.</summary>
    public static string? GitHubFullName(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl)) return null;
        var s = remoteUrl.Trim();
        if (s.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) s = s[..^4];
        string? path = null;
        if (s.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase)) path = s["git@github.com:".Length..];
        else if (Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) path = uri.AbsolutePath;
        var parts = path?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: 2 } ? $"{parts[0]}/{parts[1]}".ToLowerInvariant() : null;
    }

    /// <summary>
    /// The tree of <paramref name="projects"/> split on '.': a segment more than one project shares is a branch, a segment one
    /// project has is a leaf labelled with the rest of its name, and a project named exactly a branch's path is a leaf beside
    /// that branch's children (the Launcher's ProjectTree, every level at once).
    /// </summary>
    public static IReadOnlyList<ProjectNode> Tree(IReadOnlyList<RosterProject> projects) => Level(projects.Select(p => (p, p.Name.Split('.'))).ToList(), []);

    private static List<ProjectNode> Level(List<(RosterProject Project, string[] Segments)> under, List<string> path)
    {
        var depth = path.Count;
        var nodes = new List<ProjectNode>();
        foreach (var group in under.GroupBy(m => depth < m.Segments.Length ? m.Segments[depth] : null, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Key is null)
            {
                nodes.AddRange(group.Select(m => new ProjectLeaf(depth > 0 ? path[^1] : m.Project.Name, m.Project)));
                continue;
            }
            var items = group.ToList();
            nodes.Add(items.Count == 1
                ? new ProjectLeaf(string.Join('.', items[0].Segments.Skip(depth)), items[0].Project)
                : new ProjectBranch(group.Key, Level(items, [.. path, group.Key])));
        }
        return nodes.OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<RosterProject> Sorted(IEnumerable<RosterProject> projects) =>
        projects.DistinctBy(p => (p.Name.ToUpperInvariant(), p.Path.ToUpperInvariant()))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private static JsonElement? Property(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in element.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }
}
