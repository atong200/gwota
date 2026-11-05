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
    private readonly Label _leaderHorn;
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
        scoreColumn.WithGap(4);
        scoreColumn.AddChild(_score);
        scoreColumn.AddChild(icon);

        _hornSlot = new PanelContainer { CustomMinimumSize = CardView.BoardSize, MouseFilter = MouseFilterEnum.Ignore };
        _hornSlot.AddThemeStyleboxOverride("panel", Widgets.Box(new Color(0, 0, 0, 0.25f), radius: 4, margin: 0));
        _leaderHorn = Widgets.Label("LEADER\nHORN", 11, Palette.Highlight, HorizontalAlignment.Center);
        _leaderHorn.VerticalAlignment = VerticalAlignment.Center;
        _hornSlot.AddChild(_leaderHorn);

        _cards = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _cards.WithGap(3);

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

    /// <summary>
    /// Shows the row content (not the score, see <see cref="SetScore"/>). <paramref name="horn"/> is the horn card view,
    /// if any. The views are owned by the caller: views dropped from the row are detached, not freed.
    /// </summary>
    public void SetContent(bool weather, IReadOnlyList<CardView> cards, CardView? horn, bool leaderHorn)
    {
        ArgumentNullException.ThrowIfNull(cards);

        _style.BgColor = weather ? Palette.RowWeather : Palette.Row;
        _leaderHorn.Visible = horn is null && leaderHorn;
        _hornSlot.SyncChildren(horn is null ? [_leaderHorn] : [_leaderHorn, horn]);
        _cards.SyncChildren(cards);
    }

    /// <summary>Outlines the row and shows a pointing cursor when it is a play target.</summary>
    public void SetTargetable(bool targetable)
    {
        _style.BorderColor = targetable ? Palette.Target : Palette.PanelBorder;
        _style.SetBorderWidthAll(targetable ? 3 : 1);
        MouseDefaultCursorShape = targetable ? CursorShape.PointingHand : CursorShape.Arrow;
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
