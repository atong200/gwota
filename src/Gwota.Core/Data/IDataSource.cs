namespace Gwota.Core.Data;

/// <summary>
/// Read-only access to the game data folder. Paths are relative to the data root and use '/'.
/// The game implements this over Godot's res:// file system; tests use <see cref="FileSystemDataSource"/>.
/// </summary>
public interface IDataSource
{
    /// <summary>Recursively lists files under <paramref name="directory"/> ending with <paramref name="extension"/>.</summary>
    IEnumerable<string> ListFiles(string directory, string extension);

    string ReadAllText(string path);
}

public sealed class FileSystemDataSource(string rootDirectory) : IDataSource
{
    public string RootDirectory { get; } = Path.GetFullPath(rootDirectory);

    public IEnumerable<string> ListFiles(string directory, string extension)
    {
        var fullDirectory = Path.Combine(RootDirectory, directory);
        if (!Directory.Exists(fullDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(fullDirectory, "*" + extension, SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(RootDirectory, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
    }

    public string ReadAllText(string path) => File.ReadAllText(Path.Combine(RootDirectory, path));
}
