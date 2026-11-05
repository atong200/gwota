using Godot;
using Gwota.Core.Abilities;
using Gwota.Core.Cards;

namespace Gwota.Game.Ui;

/// <summary>A faction's perk, a thin line, then its flavor text in the card preview's flavor style.</summary>
public static class FactionText
{
    public static VBoxContainer Describe(Faction faction, float width, Color? perkColor = null, int size = 17)
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }.WithGap(8);
        box.AddChild(Wrapped(Widgets.Label(FactionPerks.Describe(faction), size, perkColor ?? Palette.Text), width));

        var flavor = FactionInfo.Flavor(faction);
        if (flavor.Length == 0)
        {
            return box;
        }

        box.AddChild(new ColorRect
        {
            Color = new Color(Palette.ForFaction(faction).Lightened(0.35f), 0.6f),
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        var italic = Widgets.Label(flavor, size - 1, Palette.Muted);
        italic.AddThemeFontOverride("font", Fonts.Italic);
        box.AddChild(Wrapped(italic, width));
        return box;
    }

    /// <summary>Plain-text form, for places without rich content.</summary>
    public static string Plain(Faction faction) =>
        FactionInfo.Flavor(faction) is { Length: > 0 } flavor ? $"{FactionPerks.Describe(faction)}\n\n{flavor}" : FactionPerks.Describe(faction);

    private static Label Wrapped(Label label, float width)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }
}
