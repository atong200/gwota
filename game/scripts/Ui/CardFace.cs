using Godot;

namespace Gwota.Game.Ui;

/// <summary>Pieces shared by card-shaped faces (<see cref="CardView"/>, <see cref="LeaderView"/>).</summary>
public static class CardFace
{
    /// <summary>The name on a dark strip along the bottom edge of the face (anchored, so add it to a plain Control).</summary>
    public static PanelContainer NameBanner(string name, int fontSize, int margin)
    {
        var label = Widgets.Label(name, fontSize, Palette.Text, HorizontalAlignment.Center).Outlined(3);
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;

        var banner = Widgets.Panel(label, Widgets.Box(new Color(Palette.BadgeBackground, 0.82f), radius: 0, margin: margin));
        banner.MouseFilter = Control.MouseFilterEnum.Ignore;
        banner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        banner.GrowVertical = Control.GrowDirection.Begin;
        return banner;
    }

    /// <summary>Art placeholder: up to two faint initials, centered (e.g. "WR" for Windranger).</summary>
    public static Label Initials(string name, int fontSize)
    {
        var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
        var label = Widgets.Label(initials, 0, new Color(1, 1, 1, 0.22f), HorizontalAlignment.Center).WithDisplayFont(fontSize);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    /// <summary>The art when the image exists, the initials otherwise.</summary>
    public static Control Art(Texture2D? art, string name, int initialsSize) => art is null
        ? Initials(name, initialsSize)
        : new TextureRect
        {
            Texture = art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
}