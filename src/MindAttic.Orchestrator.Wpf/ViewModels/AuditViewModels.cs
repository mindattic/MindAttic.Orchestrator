using System.IO;
using System.Windows.Input;
using MindAttic.Orchestrator.Audit;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

public sealed class AuditFileViewModel(DuplicateFile file, string root)
{
    public string Repo => file.Repo;
    public string RelativePath { get; } = Path.GetRelativePath(Path.Combine(root, file.Repo), file.Path);
    public ICommand OpenCommand { get; } = new RelayCommand(() => Opener.Open(file.Path));
}

public sealed class DuplicateGroupViewModel(DuplicateGroup group, string root)
{
    public string Title => group.Identical
        ? $"Identical in {group.RepoCount} repos"
        : $"{group.Similarity:P0} alike across {group.RepoCount} repos";
    public string Detail => $"~{group.SharedLines} lines · belongs in {group.SuggestedHome}";
    public bool Identical => group.Identical;
    private const int Shown = 6;

    // A fixed short list instead of a nested scroller, which would swallow the page's mouse wheel.
    public IReadOnlyList<AuditFileViewModel> Files { get; } = [.. group.Files.Take(Shown).Select(f => new AuditFileViewModel(f, root))];
    public string More => group.Files.Count > Shown ? $"+{group.Files.Count - Shown} more" : "";
    public string AllFiles => string.Join("\n", group.Files.Select(f => $"{f.Repo}  {Path.GetRelativePath(Path.Combine(root, f.Repo), f.Path)}"));
    public ICommand CompareCommand { get; } = new RelayCommand(
        () => Opener.Diff(group.Files[0].Path, group.Files[1].Path), () => group.Files.Count > 1);
}

public sealed class DriftViewModel(DriftFinding finding)
{
    public string Repo => finding.Repo;
    public string File => finding.File;
    public string Similarity => $"{finding.Similarity:P0} alike";
    public string Kind => finding.IsCustom ? "Custom" : "Drifted";
    public bool IsCustom => finding.IsCustom;
    public string Detail => $"{finding.CanonicalCount} of {finding.Total} repos share one version · {finding.Variants} variants";
    public ICommand CompareCommand { get; } = new RelayCommand(() => Opener.Diff(finding.CanonicalPath, finding.Path));
}

public sealed class MissingViewModel(string file, IReadOnlyList<MissingFinding> rows)
{
    public string File => file;
    public string Detail => $"{rows[0].HaveIt} of {rows[0].Eligible} agent-enabled repos have it";
    public string Repos => string.Join(", ", rows.Select(r => r.Repo));
    public ICommand ExampleCommand { get; } = new RelayCommand(() => Opener.Open(rows[0].ExamplePath));
}
