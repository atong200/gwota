using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

public enum PileKind
{
    Draw,
    Discard,
}

/// <summary>
/// A draw pile (up to three stacked card backs) or a discard pile (a bordered slot, then the top card face).
/// The exact count shows over the pile while it is hovered, since a stack cannot be counted by eye.
/// </summary>
public sealed partial class PileView : Control
{
    private const int MaxStacked = 3;
    private const float StackOffset = 3;

    private readonly PileKind _kind;
    private readonly Vector2 _cardSize;
    private readonly Control _cards = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _countLabel;

    public PileView(PileKind kind, Vector2 cardSize)
    {
        _kind = kind;
        _cardSize = cardSize;
        CustomMinimumSize = cardSize + (new Vector2(StackOffset, StackOffset) * (MaxStacked - 1));
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_cards);

        _countLabel = Widgets.Label(string.Empty, 14, Palette.Text, HorizontalAlignment.Center).Outlined();
        _countLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _countLabel.CustomMinimumSize = new Vector2(Math.Max(96, cardSize.X + 24), 0);
        var count = Widgets.Panel(_countLabel, Widgets.Box(new Color(Palette.Background, 0.85f), Palette.Highlight, 1, radius: 4, margin: 4));
        count.MouseFilter = MouseFilterEnum.Ignore;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false, Size = CustomMinimumSize };
        center.AddChild(count);
        AddChild(center);

        MouseEntered += () => center.Visible = true;
        MouseExited += () => center.Visible = false;
    }

    public int Count { get; private set; }

    /// <summary>Global position of the top card (where drawn cards come from); the stack grows down-right under it.</summary>
    public Vector2 TopGlobalPosition => GlobalPosition;

    public void Show(int count, CardInstance? topCard = null)
    {
        Count = count;
        _cards.ClearChildren();

        if (count == 0)
        {
            _cards.AddChild(EmptySlot());
        }
        else if (_kind == PileKind.Discard && topCard is not null)
        {
            _cards.AddChild(new CardView(topCard, _cardSize) { ShowsPreview = false, MouseFilter = MouseFilterEnum.Ignore, Size = _cardSize });
        }
        else
        {
            var stacked = Math.Min(count, MaxStacked);
            for (var i = 0; i < stacked; i++)
            {
                _cards.AddChild(new CardBack(_cardSize) { Position = new Vector2(StackOffset, StackOffset) * (stacked - 1 - i) });
            }
        }

        _countLabel.Text = CardText.PileCount(count, _kind == PileKind.Draw ? "draw pile" : "discard pile");
    }

    private PanelContainer EmptySlot()
    {
        var text = _kind == PileKind.Draw ? "Draw\npile" : "Discard\npile";
        var label = Widgets.Label(text, Math.Max(10, (int)(_cardSize.X / 6)), Palette.Muted, HorizontalAlignment.Center);
        label.VerticalAlignment = VerticalAlignment.Center;
        var slot = Widgets.Panel(label, Widgets.Box(new Color(0, 0, 0, 0.2f), Palette.PanelBorder, 2, radius: 6, margin: 2));
        slot.MouseFilter = MouseFilterEnum.Ignore;
        slot.CustomMinimumSize = _cardSize;
        slot.Size = _cardSize;
        return slot;
    }
}
