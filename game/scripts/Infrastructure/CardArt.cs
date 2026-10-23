using Godot;

namespace Gwota.Game.Infrastructure;

/// <summary>
/// Resolves art by convention: <c>res://assets/cards/&lt;card id&gt;.png</c>, <c>res://assets/leaders/&lt;leader id&gt;.png</c>
/// and <c>res://assets/card_back.png</c> (.webp/.jpg too). Missing art returns null and views draw a placeholder.
/// </summary>
public static class CardArt
{
    private static readonly string[] Extensions = [".png", ".webp", ".jpg"];
    private static readonly Dictionary<string, Texture2D?> Cache = [];

    public static Texture2D? ForCard(string cardId) => Find($"res://assets/cards/{cardId}");

    public static Texture2D? ForLeader(string leaderId) => Find($"res://assets/leaders/{leaderId}");

    /// <summary>The shared back of every card (<c>res://assets/card_back.png</c>), or null for the placeholder.</summary>
    public static Texture2D? CardBack() => Find("res://assets/card_back");

    /// <summary>Drops cached textures so no engine reference outlives the scene tree.</summary>
    public static void ClearCache() => Cache.Clear();

    private static Texture2D? Find(string basePath)
    {
        if (Cache.TryGetValue(basePath, out var cached))
        {
            return cached;
        }

        var path = Extensions.Select(ext => basePath + ext).FirstOrDefault(p => ResourceLoader.Exists(p));
        var texture = path is null ? null : ResourceLoader.Load<Texture2D>(path);
        Cache[basePath] = texture;
        return texture;
    }
}
