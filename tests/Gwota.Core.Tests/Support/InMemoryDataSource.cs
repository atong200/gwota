using Gwota.Core.Data;

namespace Gwota.Core.Tests.Support;

/// <summary>Data source backed by a dictionary of path → JSON text.</summary>
internal sealed class InMemoryDataSource : IDataSource
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public InMemoryDataSource Add(string path, string json)
    {
        _files[path] = json;
        return this;
    }

    public IEnumerable<string> ListFiles(string directory, string extension) =>
        _files.Keys
            .Where(path => path.StartsWith(directory + "/", StringComparison.Ordinal) && path.EndsWith(extension, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    public string ReadAllText(string path) => _files[path];
}

internal static class RepoPaths
{
    /// <summary>Repository root, found by walking up from the test binaries to <c>Gwota.slnx</c>.</summary>
    public static string Root { get; } = FindRoot();

    public static string GameData => Path.Combine(Root, "game", "data");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Gwota.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root (Gwota.slnx).");
    }
}
