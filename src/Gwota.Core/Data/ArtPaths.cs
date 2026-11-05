namespace Gwota.Core.Data;

/// <summary>
/// Where art lives, relative to the art root and without extension: <c>cards/&lt;faction&gt;/&lt;card id&gt;</c> (the same
/// faction folders as the card data) and <c>leaders/&lt;leader id&gt;</c>. The extension is one of <see cref="ImageExtensions"/>.
/// A non-hero card may have up to <see cref="MaxCardVariants"/> images: <c>&lt;id&gt;</c>, then <c>&lt;id&gt;_2</c> and
/// <c>&lt;id&gt;_3</c>; each copy in a match shows one of them (<see cref="PickVariant"/>).
/// </summary>
public static class ArtPaths
{
    public const string CardsFolder = "cards";
    public const string LeadersFolder = "leaders";
    public const int MaxCardVariants = 3;

    public static IReadOnlyList<string> ImageExtensions { get; } = [".png", ".webp", ".jpg"];

    public static string Card(CardDefinition card) => CardVariant(card, 1);

    /// <summary>The path of image <paramref name="variant"/> (1-based) of <paramref name="card"/>.</summary>
    public static string CardVariant(CardDefinition card, int variant)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentOutOfRangeException.ThrowIfLessThan(variant, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(variant, MaxCardVariants);
        var path = $"{CardsFolder}/{FactionFolder(card.Faction)}/{card.Id}";
        return variant == 1 ? path : $"{path}_{variant}";
    }

    /// <summary>
    /// The image (1-based) shown by copy <paramref name="copy"/> (0-based) when <paramref name="available"/> images
    /// exist: copies take images in turn and wrap around. Heroes always show the first image.
    /// </summary>
    public static int PickVariant(CardDefinition card, int copy, int available)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentOutOfRangeException.ThrowIfNegative(copy);
        var images = Math.Clamp(available, 1, MaxCardVariants);
        return card.IsHero ? 1 : (copy % images) + 1;
    }

    public static string Leader(string leaderId) => $"{LeadersFolder}/{leaderId}";

    public static string FactionFolder(Faction faction) => GameDataLoader.ToDataName(faction);

    /// <summary>
    /// The card art that would not show: <paramref name="files"/> are paths relative to the cards art folder
    /// (<c>realms/sven.png</c>). Lists images outside their card's faction folder, images named after no card, images
    /// with an upper-case extension, variants of heroes and variants missing the image before them. Other files
    /// (READMEs, import files) are ignored.
    /// </summary>
    public static IReadOnlyList<string> FindMisplacedCardArt(GameData data, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(files);
        var images = files.Where(IsImage).Order(StringComparer.Ordinal).ToList();
        var present = images.Select(file => Path.ChangeExtension(file, null)).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var file in images)
        {
            var problem = Problem(data, file, present);
            if (problem is not null)
            {
                problems.Add($"'{CardsFolder}/{file}' {problem}");
            }
        }

        return problems;
    }

    private static bool IsImage(string file) => ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    private static string? Problem(GameData data, string file, HashSet<string> present)
    {
        if (!ImageExtensions.Contains(Path.GetExtension(file), StringComparer.Ordinal))
        {
            return $"needs a lower-case extension ({string.Join(", ", ImageExtensions)}).";
        }

        var slash = file.LastIndexOf('/');
        var folder = slash < 0 ? string.Empty : file[..slash];
        var name = Path.GetFileNameWithoutExtension(file);
        var (card, variant) = Resolve(data, name);
        if (card is null)
        {
            return "is named after no card id.";
        }

        var expected = FactionFolder(card.Faction);
        if (folder != expected)
        {
            return $"must be in '{CardsFolder}/{expected}/'.";
        }

        if (variant > 1 && card.IsHero)
        {
            return $"is a second image of the hero '{card.Id}'; heroes have a single image.";
        }

        var previous = variant > 1 ? CardVariant(card, variant - 1)[(CardsFolder.Length + 1)..] : null;
        return previous is not null && !present.Contains(previous)
            ? $"needs '{CardsFolder}/{previous}' first."
            : null;
    }

    /// <summary>The card an image is named after and its variant; an exact card id wins over a <c>_2</c>/<c>_3</c> suffix.</summary>
    private static (CardDefinition? Card, int Variant) Resolve(GameData data, string name)
    {
        if (data.Cards.TryGetValue(name, out var card))
        {
            return (card, 1);
        }

        var underscore = name.LastIndexOf('_');
        return underscore > 0
            && int.TryParse(name[(underscore + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var variant)
            && variant is >= 2 and <= MaxCardVariants
            && name[(underscore + 1)..] == variant.ToString(System.Globalization.CultureInfo.InvariantCulture)
            && data.Cards.TryGetValue(name[..underscore], out var baseCard)
            ? (baseCard, variant)
            : (null, 0);
    }
}
