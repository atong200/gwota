using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;

namespace Gwota.Game.Ui;

/// <summary>One combat row of one side: score badge and row icon, horn slot and the unit cards. Clickable when it is a play target.</summary>
public sealed partial class RowView : PanelContainer
{
    private readonly StyleBoxFlat _style;
    private readonly ScoreBadge _score = new(36, 17);
    private readonly PanelContainer _hornSlot;
    private readonly HBoxContainer _cards;

    public RowView(Seat side, RowType row)
    {
        Side = side;
        Row = row;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        _style = Widgets.Box(Palette.Row, Palette.PanelBorder, 1, radius: 4, margin: 3);
        AddThemeStyleboxOverride("panel", _style);

        var name = $"{row} row";
        _score.TooltipText = $"{name} total";
        var icon = Icons.Rect(Icons.ForRow(row), 22, Palette.Muted);
        icon.MouseFilter = MouseFilterEnum.Pass;
        icon.TooltipText = name;
        var scoreColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(56, 0),
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        scoreColumn.AddThemeConstantOverride("separation", 4);
        scoreColumn.AddChild(_score);
        scoreColumn.AddChild(icon);

        _hornSlot = new PanelContainer { CustomMinimumSize = CardView.BoardSize, MouseFilter = MouseFilterEnum.Ignore };
        _hornSlot.AddThemeStyleboxOverride("panel", Widgets.Box(new Color(0, 0, 0, 0.25f), radius: 4, margin: 0));

        _cards = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _cards.AddThemeConstantOverride("separation", 3);

        var layout = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        layout.AddChild(scoreColumn);
        layout.AddChild(_hornSlot);
        layout.AddChild(_cards);
        AddChild(layout);
    }

    public event Action<RowView>? Clicked;

    public Seat Side { get; }

    public RowType Row { get; }

    public void SetScore(int score) => _score.SetScore(score, Palette.PanelBorder);

    /// <summary>Replaces the row content (not the score, see <see cref="SetScore"/>). <paramref name="horn"/> is the horn card view, if any.</summary>
    public void Show(bool weather, bool targetable, IEnumerable<CardView> cards, CardView? horn, bool leaderHorn)
    {
        ArgumentNullException.ThrowIfNull(cards);

        _style.BgColor = weather ? Palette.RowWeather : Palette.Row;
        _style.BorderColor = targetable ? Palette.Target : Palette.PanelBorder;
        _style.SetBorderWidthAll(targetable ? 3 : 1);
        MouseDefaultCursorShape = targetable ? CursorShape.PointingHand : CursorShape.Arrow;

        _hornSlot.ClearChildren();
        if (horn is not null)
        {
            _hornSlot.AddChild(horn);
        }
        else if (leaderHorn)
        {
            var label = Widgets.Label("LEADER\nHORN", 11, Palette.Highlight, HorizontalAlignment.Center);
            label.VerticalAlignment = VerticalAlignment.Center;
            _hornSlot.AddChild(label);
        }

        _cards.ClearChildren();
        foreach (var card in cards)
        {
            _cards.AddChild(card);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Clicked?.Invoke(this);
            AcceptEvent();
        }
    }
}
