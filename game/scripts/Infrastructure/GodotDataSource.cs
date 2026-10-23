using Godot;
using Gwota.Core.Data;
using FileAccess = Godot.FileAccess;

namespace Gwota.Game.Infrastructure;

/// <summary>Reads game data from Godot's virtual file system (res://), which works both in the editor and in exports.</summary>
public sealed class GodotDataSource(string root) : IDataSource
{
    private const string RemapSuffix = ".remap";

    public IEnumerable<string> ListFiles(string directory, string extension)
    {
        var files = new List<string>();
        Collect(directory, extension, files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    public string ReadAllText(string path)
    {
        var fullPath = $"{root}/{path}";
        using var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read)
            ?? throw new IOException($"Cannot open '{fullPath}': {FileAccess.GetOpenError()}");
        return file.GetAsText();
    }

    private void Collect(string directory, string extension, List<string> files)
    {
        using var dir = DirAccess.Open($"{root}/{directory}");
        if (dir is null)
        {
            return;
        }

        foreach (var entry in dir.GetFiles())
        {
            // Exported builds may list remapped resources as "<name>.remap".
            var name = entry.EndsWith(RemapSuffix, StringComparison.Ordinal) ? entry[..^RemapSuffix.Length] : entry;
            if (name.EndsWith(extension, StringComparison.Ordinal))
            {
                files.Add($"{directory}/{name}");
            }
        }

        foreach (var subdirectory in dir.GetDirectories())
        {
            Collect($"{directory}/{subdirectory}", extension, files);
        }
    }
}
