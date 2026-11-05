using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// Picks a deck's faction and leader: factions with their perk on the left, the selected faction's leaders with their
/// ability on the right. Clicking a leader confirms. When changing a leader, the deck's faction is locked.
/// </summary>
public sealed partial class FactionLeaderDialog : Modal
{
    private static readonly Vector2 CardSize = CardView.OfHeight(312);
    private const int MaxColumns = 4;
    private const int Gap = 26;
    private const int FactionWidth = 440;
    private const int ColumnGap = 32;
    private static readonly int BookmarkRoom = SelectionFrame.Overhang(CardSize.Y);

    private readonly GameData _data;
    private readonly string? _currentLeaderId;
    private readonly bool _factionLocked;
    private readonly Action<Faction, string> _onChosen;
    private readonly Dictionary<Faction, PanelContainer> _tiles = [];
    private readonly GridContainer _leaders = new() { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
    private Faction _faction;

    private FactionLeaderDialog(GameData data, Faction faction, string? currentLeaderId, bool factionLocked, Action<Faction, string> onChosen)
        : base(Width(data), ModalStyle.Game)
    {
        _data = data;
        _faction = faction;
        _currentLeaderId = currentLeaderId;
        _factionLocked = factionLocked;
        _onChosen = onChosen;

        Content.AddChild(Title(factionLocked ? "Choose a leader" : "New deck", 30));
        if (!factionLocked)
        {
            Content.AddChild(Widgets.Label("Choose a faction and a leader.", 20, Palette.Muted, HorizontalAlignment.Center));
        }

        var factions = new VBoxContainer { CustomMinimumSize = new Vector2(FactionWidth, 0) }.WithGap(14);
        factions.AddChild(Heading("Faction"));
        foreach (var playable in FactionInfo.Playable)
        {
            var tile = FactionTile(playable);
            _tiles[playable] = tile;
            factions.AddChild(tile);
        }

        _leaders.AddThemeConstantOverride("h_separation", Gap);
        _leaders.AddThemeConstantOverride("v_separation", Gap + BookmarkRoom);
        var leaders = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }.WithGap(12);
        leaders.AddChild(Heading("Leader"));
        leaders.AddChild(CenteredBelowBookmark(_leaders));

        var columns = new HBoxContainer().WithGap(ColumnGap);
        columns.AddChild(factions);
        columns.AddChild(new VSeparator());
        columns.AddChild(leaders);
        Content.AddChild(columns);

        var cancel = MenuButton("Cancel", Cancel);
        cancel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        cancel.CustomMinimumSize = new Vector2(280, cancel.CustomMinimumSize.Y);
        Content.AddChild(cancel);

        Select(faction);
    }

    /// <summary>Creates a deck: <paramref name="onChosen"/> receives the faction and leader picked.</summary>
    public static void ShowNew(Node parent, GameData data, Action<Faction, string> onChosen)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(onChosen);
        parent.AddChild(new FactionLeaderDialog(data, FactionInfo.Playable[0], null, factionLocked: false, onChosen));
    }

    /// <summary>Changes the leader of a <paramref name="faction"/> deck; Escape or Cancel keeps the current one.</summary>
    public static void ShowChangeLeader(Node parent, GameData data, Faction faction, string currentLeaderId, Action<string> onChosen)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(onChosen);
        parent.AddChild(new FactionLeaderDialog(data, faction, currentLeaderId, factionLocked: true, (_, leaderId) => onChosen(leaderId)));
    }

    /// <summary>Wide enough for the faction with the most leaders, so switching faction keeps the size.</summary>
    private static int Width(GameData data)
    {
        var columns = Math.Clamp(FactionInfo.Playable.Max(f => DeckEditor.LeadersFor(data, f).Count), 1, MaxColumns);
        return (int)(FactionWidth + (2 * ColumnGap) + 4 + (columns * (CardSize.X + Gap)) - Gap);
    }

    private static Label Heading(string text) => Widgets.Label(text, 0, Palette.Muted).AsDisplay(21);

    /// <summary>The leader grid, centered vertically in its column, with room above for the chosen leader's bookmark.</summary>
    private static CenterContainer CenteredBelowBookmark(Control grid)
    {
        var room = new MarginContainer();
        room.AddThemeConstantOverride("margin_top", BookmarkRoom);
        room.AddChild(grid);
        var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        center.AddChild(room);
        return center;
    }

    private void Select(Faction faction)
    {
        _faction = faction;
        foreach (var (tileFaction, tile) in _tiles)
        {
            StyleTile(tile, tileFaction, hovered: false);
        }

        var leaders = DeckEditor.LeadersFor(_data, faction);
        _leaders.ClearChildren();
        _leaders.Columns = Math.Clamp(leaders.Count, 1, MaxColumns);
        foreach (var leader in leaders)
        {
            _leaders.AddChild(LeaderCell(leader, leader.Id == _currentLeaderId));
        }
    }

    private PanelContainer FactionTile(Faction faction)
    {
        var color = Palette.ForFaction(faction).Lightened(0.35f);
        var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }.WithGap(10);
        body.AddChild(Widgets.Label(FactionInfo.Name(faction), 0, color).AsDisplay(24));
        body.AddChild(FactionText.Describe(faction, FactionWidth - 32, size: 18));

        var tile = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        tile.AddChild(body);
        var locked = _factionLocked && faction != _faction;
        if (locked)
        {
            tile.Modulate = new Color(1, 1, 1, 0.4f);
            tile.TooltipText = "A deck's faction cannot change.";
            return tile;
        }

        if (!_factionLocked)
        {
            tile.MouseDefaultCursorShape = CursorShape.PointingHand;
            tile.MouseEntered += () => StyleTile(tile, faction, hovered: true);
            tile.MouseExited += () => StyleTile(tile, faction, hovered: false);
            tile.GuiInput += input =>
            {
                if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && faction != _faction)
                {
                    Select(faction);
                }
            };
        }

        return tile;
    }

    private void StyleTile(PanelContainer tile, Faction faction, bool hovered)
    {
        var color = Palette.ForFaction(faction);
        var selected = faction == _faction;
        var fill = selected ? new Color(color, 0.3f) : hovered ? new Color(color, 0.16f) : new Color(color, 0.08f);
        var border = selected ? color.Lightened(0.35f) : hovered ? color.Lightened(0.15f) : Palette.PanelBorder;
        tile.AddThemeStyleboxOverride("panel", Widgets.Box(fill, border, selected ? 3 : 1, radius: 8, margin: 16));
    }

    private VBoxContainer LeaderCell(LeaderDefinition leader, bool isCurrent)
    {
        var view = new LeaderView(leader, CardSize) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        view.SetMarked(isCurrent);
        view.Clicked += _ =>
        {
            Close();
            _onChosen(_faction, leader.Id);
        };

        var ability = Widgets.Label(CardText.LeaderLine(leader).Plain, 18, Palette.Text, HorizontalAlignment.Center);
        ability.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ability.CustomMinimumSize = new Vector2(CardSize.X, 0);

        // Room below the portrait for the selection band.
        var cell = new VBoxContainer().WithGap(18);
        cell.AddChild(view);
        cell.AddChild(ability);
        return cell;
    }
}
