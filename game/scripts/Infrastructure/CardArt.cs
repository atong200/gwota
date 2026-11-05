using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;
using Gwota.Core.Presentation;

namespace Gwota.Game.Infrastructure;

/// <summary>
/// Resolves art by convention (<see cref="ArtPaths"/>): <c>res://assets/cards/&lt;faction&gt;/&lt;card id&gt;.png</c>
/// (plus optional <c>&lt;card id&gt;_2</c> / <c>_3</c> variants),
/// <c>res://assets/leaders/&lt;leader id&gt;.png</c> and <c>res://assets/card_backs/&lt;back id&gt;.png</c> (.webp/.jpg too).
/// Missing art returns null and views draw a placeholder.
/// </summary>
public static class CardArt
{
    private const string Root = "res://assets";
    private const string CardBackFolder = Root + "/card_backs";
    private static readonly Dictionary<string, Texture2D?> Cache = [];
    private static readonly Dictionary<string, int> VariantCounts = [];
    private static CardBackCatalog? _cardBacks;

    /// <summary>The image of this copy: copies cycle through the card's variants (<c>id</c>, <c>id_2</c>, <c>id_3</c>).</summary>
    public static Texture2D? ForCard(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var definition = card.Definition;
        var variant = ArtPaths.PickVariant(definition, card.Copy, VariantCount(definition));
        return Find($"{Root}/{ArtPaths.CardVariant(definition, variant)}");
    }

    public static Texture2D? ForLeader(string leaderId) => Find($"{Root}/{ArtPaths.Leader(leaderId)}");

    /// <summary>Every file under <c>res://assets/cards</c>, relative to it (<c>realms/sven.png</c>), for <see cref="ArtPaths.FindMisplacedCardArt"/>.</summary>
    public static IReadOnlyList<string> CardArtFiles()
    {
        var files = new List<string>();
        ListFiles($"{Root}/{ArtPaths.CardsFolder}", string.Empty, files);
        return files;
    }

    /// <summary>The card backs found in <c>res://assets/card_backs</c>; the default back is always listed first.</summary>
    public static CardBackCatalog CardBacks() => _cardBacks ??= new CardBackCatalog(
        DirAccess.DirExistsAbsolute(CardBackFolder) ? ResourceLoader.ListDirectory(CardBackFolder) : []);

    /// <summary>The image of the back <paramref name="backId"/> (unknown ids use the default back), or null for the placeholder.</summary>
    public static Texture2D? CardBack(string? backId)
    {
        var file = CardBacks().Resolve(backId).File;
        return file is null ? null : Load($"{CardBackFolder}/{file}");
    }

    /// <summary>The pewter frame drawn over the default back (transparent inside), or null when missing.</summary>
    public static Texture2D? CardBackFrame() => Load("res://assets/ui/card_back_frame.png");

    /// <summary>Drops cached textures so no engine reference outlives the scene tree.</summary>
    public static void ClearCache()
    {
        Cache.Clear();
        VariantCounts.Clear();
        _cardBacks = null;
    }

    /// <summary>The number of consecutive images found for the card, from 1 up to <see cref="ArtPaths.MaxCardVariants"/>.</summary>
    private static int VariantCount(CardDefinition card)
    {
        if (!VariantCounts.TryGetValue(card.Id, out var count))
        {
            count = 1;
            while (count < ArtPaths.MaxCardVariants && Find($"{Root}/{ArtPaths.CardVariant(card, count + 1)}") is not null)
            {
                count++;
            }

            VariantCounts[card.Id] = count;
        }

        return count;
    }
    private static Texture2D? Find(string basePath)
    {
        if (Cache.TryGetValue(basePath, out var cached))
        {
            return cached;
        }

        var path = ArtPaths.ImageExtensions.Select(ext => basePath + ext).FirstOrDefault(p => ResourceLoader.Exists(p));
        var texture = path is null ? null : ResourceLoader.Load<Texture2D>(path);
        Cache[basePath] = texture;
        return texture;
    }

    private static void ListFiles(string folder, string prefix, List<string> files)
    {
        if (!DirAccess.DirExistsAbsolute(folder))
        {
            return;
        }

        foreach (var entry in ResourceLoader.ListDirectory(folder))
        {
            if (entry.EndsWith('/'))
            {
                ListFiles($"{folder}/{entry.TrimEnd('/')}", prefix + entry, files);
            }
            else
            {
                files.Add(prefix + entry);
            }
        }
    }

    private static Texture2D? Load(string path)
    {
        if (!Cache.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
            Cache[path] = texture;
        }

        return texture;
    }
}
