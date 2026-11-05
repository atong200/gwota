using Gwota.Core.Data;

namespace Gwota.Core.Presentation;

/// <summary>A selectable card back: its id (the file name without extension), display name and image file, if any.</summary>
public sealed record CardBackEntry(string Id, string Name, string? File);

/// <summary>
/// The card backs found in the art folder. The default back always comes first (with or without an image of its own),
/// then the others by name. Unknown ids resolve to the default back.
/// </summary>
public sealed class CardBackCatalog
{
    public const string DefaultName = "Default";

    private static readonly string[] ImageExtensions = [".png", ".webp", ".jpg"];
    private static readonly string[] ImportSuffixes = [".import", ".remap"];

    private readonly Dictionary<string, CardBackEntry> _byId;

    /// <param name="fileNames">Entries of the art folder; non-images, folders and import metadata are ignored.</param>
    public CardBackCatalog(IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, file) in fileNames.Select(ImageOf).OfType<(string Id, string File)>()
                     .OrderBy(f => Array.IndexOf(ImageExtensions, Path.GetExtension(f.File).ToLowerInvariant())))
        {
            files.TryAdd(id, file);
        }

        var others = files
            .Where(f => !IsDefault(f.Key))
            .Select(f => new CardBackEntry(f.Key, DisplayName(f.Key), f.Value))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.Ordinal);
        var defaultFile = files.FirstOrDefault(f => IsDefault(f.Key)).Value;
        Entries = [new CardBackEntry(CardBacks.DefaultId, DefaultName, defaultFile), .. others];
        _byId = Entries.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every back, the default first.</summary>
    public IReadOnlyList<CardBackEntry> Entries { get; }

    public CardBackEntry Default => Entries[0];

    public bool Contains(string? id) => id is not null && _byId.ContainsKey(id.Trim());

    /// <summary>The back named <paramref name="id"/>, or the default back when it is unknown or blank.</summary>
    public CardBackEntry Resolve(string? id) =>
        id is not null && _byId.TryGetValue(id.Trim(), out var entry) ? entry : Default;

    /// <summary>"red_mist" becomes "Red Mist": separators become spaces and each word is capitalized.</summary>
    public static string DisplayName(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var words = id.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    private static bool IsDefault(string id) => string.Equals(id, CardBacks.DefaultId, StringComparison.OrdinalIgnoreCase);

    private static (string Id, string File)? ImageOf(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry) || entry.EndsWith('/'))
        {
            return null;
        }

        var file = Path.GetFileName(entry.Trim());
        foreach (var suffix in ImportSuffixes.Where(s => file.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
        {
            file = file[..^suffix.Length];
        }

        var id = Path.GetFileNameWithoutExtension(file);
        var isImage = ImageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant());
        return isImage && id.Length > 0 && !id.StartsWith('.') ? (id, file) : null;
    }
}
