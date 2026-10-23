using Godot;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// The back of a card: <c>res://assets/card_back.png</c> when present, otherwise a styled placeholder.
/// Used for the draw piles, the opponent's hand and the draft redraw animation.
/// </summary>
public sealed partial class CardBack : PanelContainer
{
    private static readonly Color Fill = new("3a2f5c");
    private static readonly Color Border = new("8c7ad1");

    public CardBack(Vector2 size)
    {
        CustomMinimumSize = size;
        Size = size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", Widgets.Box(Fill, Border, 2, radius: Math.Max(3, (int)(size.X / 14)), margin: 3));

        var art = CardArt.CardBack();
        if (art is not null)
        {
            AddChild(new TextureRect
            {
                Texture = art,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            });
            return;
        }

        var emblem = Widgets.Label("G", Math.Max(10, (int)(size.X * 0.4f)), new Color(Border, 0.6f), HorizontalAlignment.Center);
        emblem.VerticalAlignment = VerticalAlignment.Center;
        AddChild(emblem);
    }
}
