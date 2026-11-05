using Godot;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// Read-only view of a discard pile: its cards in rows, most recent first, scrolling (mouse wheel or scrollbar) when
/// they do not fit. Hovering a card shows its preview; Escape, Close or a click outside the panel closes it.
/// </summary>
public sealed partial class DiscardPileDialog : Modal
{
    private const int Columns = 7;
    private const int Gap = 10;
    private const int MaxVisibleRows = 3;
    private const float ReservedHeight = 280;
    private const float ScrollbarRoom = 18;
    private static readonly Vector2 CardSize = CardView.OfHeight(168);

    private DiscardPileDialog(DiscardPileText pile, float viewportHeight)
        : base((int)((Columns * (CardSize.X + Gap)) - Gap + ScrollbarRoom), ModalStyle.Game)
    {
        Content.AddChild(Title(pile.Title, 30));
        Content.AddChild(pile.Cards.Count == 0 ? EmptyText(pile.EmptyText) : Cards(pile, viewportHeight));

        var close = MenuButton("Close", Close);
        close.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        close.CustomMinimumSize = new Vector2(280, close.CustomMinimumSize.Y);
        Content.AddChild(close);
    }

    public static void Show(Node parent, DiscardPileText pile)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(pile);
        var viewport = parent.GetViewport()?.GetVisibleRect().Size.Y ?? 900;
        parent.AddChild(new DiscardPileDialog(pile, viewport));
    }

    public override void _GuiInput(InputEvent @event)
    {
        // Only clicks outside the panel reach the modal itself (the panel stops the others).
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            AcceptEvent();
            Close();
        }
    }

    private static Label EmptyText(string text)
    {
        var label = Widgets.Label(text, 18, Palette.Muted, HorizontalAlignment.Center);
        label.CustomMinimumSize = new Vector2(0, CardSize.Y / 2);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private static ScrollContainer Cards(DiscardPileText pile, float viewportHeight)
    {
        // A single short row is centered; full rows leave room on the right for the scrollbar.
        var grid = new GridContainer { Columns = Math.Min(Columns, pile.Cards.Count) };
        if (pile.Cards.Count < Columns)
        {
            grid.SizeFlagsHorizontal = SizeFlags.ShrinkCenter | SizeFlags.Expand;
        }

        grid.AddThemeConstantOverride("h_separation", Gap);
        grid.AddThemeConstantOverride("v_separation", Gap);
        foreach (var card in pile.Cards)
        {
            grid.AddChild(new CardView(card, CardSize));
        }

        var rows = (pile.Cards.Count + Columns - 1) / Columns;
        var rowHeight = CardSize.Y + Gap;
        var fitting = Math.Clamp((int)((viewportHeight - ReservedHeight) / rowHeight), 1, MaxVisibleRows);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = rows > fitting ? ScrollContainer.ScrollMode.ShowAlways : ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, (Math.Min(rows, fitting) * rowHeight) - Gap),
        };
        scroll.AddChild(grid);
        return scroll;
    }
}
