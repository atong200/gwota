using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>What a book slot shows: copies on offer, and whether clicking can add one right now.</summary>
public readonly record struct SlotState(int Copies, bool CanAdd);

/// <summary>
/// One card of the deck builder's book, on a stack of outlines (one per extra copy, up to two) above an "xN" counter.
/// With no copy left it is an empty bordered spot showing the name; hovering shows the card preview.
/// </summary>
public sealed partial class BookSlot : Control
{
    private const int MaxOutlines = 2;
    private const float DimmedAlpha = 0.45f;

    private readonly CardInstance _card;
    private readonly BookGrid _grid;
    private readonly PanelContainer[] _outlines = new PanelContainer[MaxOutlines];
    private readonly PanelContainer _empty;
    private readonly Label _count;

    public BookSlot(CardInstance card, BookGrid grid)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _grid = grid;
        MouseFilter = MouseFilterEnum.Ignore;
        Size = CustomMinimumSize = new Vector2(grid.SlotWidth, grid.SlotHeight);

        var cardSize = new Vector2(grid.CardWidth, grid.CardHeight);
        var radius = Math.Max(2, (int)MathF.Round(6 * grid.CardWidth / CardView.HandSize.X));
        var rarity = card.Definition.IsHero ? Palette.Hero : Palette.Common;

        // Deepest copy first, so nearer copies draw over it.
        for (var i = MaxOutlines - 1; i >= 0; i--)
        {
            var outline = Widgets.Panel(new Control(), Widgets.Box(Palette.ForFaction(card.Definition.Faction).Darkened(0.5f), new Color(rarity, 0.7f), 1, radius: radius, margin: 0));
            outline.MouseFilter = MouseFilterEnum.Ignore;
            outline.Position = CardPosition + new Vector2(grid.StackOffset * (i + 1), -grid.StackOffset * (i + 1));
            outline.Size = cardSize;
            _outlines[i] = outline;
            AddChild(outline);
        }

        View = new CardView(card, cardSize) { Position = CardPosition };
        View.Clicked += _ => Clicked?.Invoke(this);
        AddChild(View);

        _empty = CreateEmpty(card.Definition.Name, cardSize, radius);
        AddChild(_empty);

        _count = Widgets.Label(string.Empty, 20, Palette.Highlight, HorizontalAlignment.Center);
        _count.AddThemeFontOverride("font", Fonts.Bold);
        _count.Position = new Vector2(0, CardPosition.Y + grid.CardHeight + 2);
        _count.Size = new Vector2(grid.SlotWidth, grid.CounterHeight - 2);
        _count.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_count);
    }

    public event Action<BookSlot>? Clicked;

    public CardDefinition Card => _card.Definition;

    /// <summary>The card face (the add animation flies a copy of it).</summary>
    public CardView View { get; }

    public SlotState State { get; private set; }

    /// <summary>The card sits low-left; the stack peeks out above and to the right.</summary>
    private Vector2 CardPosition => new(0, 2 * _grid.StackOffset);

    public void Show(SlotState state)
    {
        State = state;
        var copies = Math.Max(0, state.Copies);
        for (var i = 0; i < MaxOutlines; i++)
        {
            _outlines[i].Visible = copies > i + 1;
        }

        View.Visible = copies > 0;
        View.Modulate = state.CanAdd || copies == 0 ? Colors.White : new Color(1, 1, 1, DimmedAlpha);
        _empty.Visible = copies == 0;
        _count.Text = CardText.Copies(copies);
        _count.AddThemeColorOverride("font_color", copies > 0 ? Palette.Highlight : Palette.Muted);
    }

    public override void _ExitTree() => CardPreview.Find(this)?.HideFor(this);

    private PanelContainer CreateEmpty(string name, Vector2 size, int radius)
    {
        var label = Widgets.Label(name, 0, new Color(Palette.Muted, 0.6f), HorizontalAlignment.Center)
            .WithDisplayFont(Math.Max(12, (int)(size.X / 10)));
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.VerticalAlignment = VerticalAlignment.Center;

        var empty = Widgets.Panel(label, Widgets.Box(new Color(Palette.Background, 0.35f), new Color(Palette.Muted, 0.45f), 2, radius: radius, margin: 8));
        empty.Position = CardPosition;
        empty.Size = size;
        empty.MouseFilter = MouseFilterEnum.Stop;
        empty.MouseEntered += () => CardPreview.Find(this)?.ShowFor(this, _card);
        empty.MouseExited += () => CardPreview.Find(this)?.HideFor(this);
        empty.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Clicked?.Invoke(this);
                empty.AcceptEvent();
            }
        };
        return empty;
    }
}