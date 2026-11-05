using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Common;
using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;
using Gwota.Core.Presentation;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// The collection's right pane for an open deck: leader and card back, name, rename/delete, faction, status, counts and
/// cards by row.
/// Clicking a line removes one copy; every change is saved immediately.
/// </summary>
public sealed partial class DeckEditPane : VBoxContainer
{
    private const double RemoveFadeDuration = 0.2;
    private const int HeaderGap = 16;
    private static readonly Vector2 LeaderSize = CardView.OfHeight(156);

    private readonly GameData _data;
    private readonly DeckStore _store;
    private readonly Func<double> _durationScale;
    private readonly HBoxContainer _header = new() { Alignment = AlignmentMode.Center };
    private readonly Label _name = Widgets.Label(string.Empty, 0, Palette.Highlight).WithDisplayFont(22);
    private readonly HBoxContainer _tags = new() { Alignment = AlignmentMode.Center };
    private readonly Label _units = Widgets.Label(string.Empty, 18);
    private readonly Label _specials = Widgets.Label(string.Empty, 18, Palette.Text, HorizontalAlignment.Right);
    private readonly VBoxContainer _lines = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Dictionary<string, DeckLineRow> _rows = new(StringComparer.Ordinal);
    private int _nextInstanceId = 1;

    public DeckEditPane(GameData data, DeckStore store, DeckDefinition deck, Func<double> durationScale)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _durationScale = durationScale ?? throw new ArgumentNullException(nameof(durationScale));
        Editor = new DeckEditor(deck ?? throw new ArgumentNullException(nameof(deck)), data);
        this.WithGap(10);

        _header.WithGap(HeaderGap);
        AddChild(_header);
        AddChild(BuildNameRow());
        _tags.WithGap(8);
        AddChild(_tags);
        AddChild(BuildCounts());
        AddChild(new HSeparator());

        _lines.WithGap(4);
        LinesScroll.AddChild(_lines);
        AddChild(LinesScroll);
        AddChild(Widgets.Label("Click a card to remove one copy.", 16, Palette.Muted, HorizontalAlignment.Center));

        var done = Widgets.Button("Done", () => DoneRequested?.Invoke()).AsDisplay(20);
        done.CustomMinimumSize = new Vector2(0, 50);
        done.TooltipText = "Close the deck (Esc). Changes are saved automatically.";
        UiTheme.TintButton(done, Palette.ForFaction(Editor.Faction));
        AddChild(done);

        Render();
    }

    /// <summary>The player closed the deck.</summary>
    public event Action? DoneRequested;

    /// <summary>The deck was deleted (the pane is now stale).</summary>
    public event Action? Deleted;

    /// <summary>Copies left the deck: the book's counters must be refreshed.</summary>
    public event Action? CardsChanged;

    public DeckEditor Editor { get; }

    /// <summary>The deck's lines (the add animation scrolls them and aims at a row).</summary>
    public ScrollContainer LinesScroll { get; } = new()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
    };

    public DeckLineRow? RowFor(string cardId) => _rows.GetValueOrDefault(cardId);

    public void Save() => _store.Save(Editor.Deck);

    public void Render()
    {
        RenderHeader();
        _name.Text = Editor.Name;
        _name.TooltipText = Editor.Name;
        RenderTags();
        RenderCounts();
        RenderLines();
    }

    private HBoxContainer BuildNameRow()
    {
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _name.MouseFilter = MouseFilterEnum.Stop;

        var delete = Widgets.IconButton(Icons.CrossIcon, "Delete this deck", ConfirmDelete, Palette.Danger);
        if (!_store.Collection.CanDelete)
        {
            delete.Disabled = true;
            delete.TooltipText = "You need at least one deck.";
        }

        var row = new HBoxContainer().WithGap(6);
        row.AddChild(_name);
        row.AddChild(Widgets.IconButton(Icons.PenIcon, "Rename this deck", Rename));
        row.AddChild(delete);
        return row;
    }

    private HBoxContainer BuildCounts()
    {
        _units.SizeFlagsHorizontal = _specials.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _units.MouseFilter = _specials.MouseFilter = MouseFilterEnum.Stop;
        _units.TooltipText = $"A deck needs at least {Editor.Rules.MinUnits} unit cards.";
        _specials.TooltipText = $"A deck holds at most {Editor.Rules.MaxSpecials} special cards.";
        var counts = new HBoxContainer();
        counts.AddChild(_units);
        counts.AddChild(_specials);
        return counts;
    }

    private void RenderHeader()
    {
        _header.ClearChildren();
        var leader = new LeaderView(Editor.Leader, LeaderSize)
        {
            TooltipText = $"{Editor.Leader.Name}\n{CardText.LeaderLine(Editor.Leader).Plain}\nClick to change the leader.",
        };
        leader.Clicked += _ => FactionLeaderDialog.ShowChangeLeader(this, _data, Editor.Faction, Editor.Leader.Id, ChangeLeader);
        _header.AddChild(leader);

        var back = CardArt.CardBacks().Resolve(Editor.CardBackId);
        var style = new CardBackStyle(back.Id, Editor.Faction);
        var tile = new CardBackTile(style, LeaderSize) { TooltipText = $"{back.Name}\nClick to change the card back." };
        tile.Clicked += _ => CardBackDialog.Show(this, style, ChangeCardBack);
        _header.AddChild(tile);
    }

    private void RenderTags()
    {
        _tags.ClearChildren();
        var faction = Editor.Faction;
        _tags.AddChild(Widgets.Tag(
            FactionInfo.Name(faction),
            Palette.ForFaction(faction).Lightened(0.35f),
            FactionText.Plain(faction),
            richTooltip: () => FactionText.Describe(faction, 300)));
        var problems = Editor.Problems;
        _tags.AddChild(problems.Count == 0
            ? Widgets.Tag("Ready", Palette.Buff, "This deck can be played.")
            : Widgets.Tag("Incomplete", Palette.Debuff, string.Join('\n', problems)));
    }

    private void RenderCounts()
    {
        var stats = Editor.Stats;
        var rules = Editor.Rules;
        _units.Text = $"Units  {stats.Units} / {rules.MinUnits}";
        _units.AddThemeColorOverride("font_color", stats.Units >= rules.MinUnits ? Palette.Buff : Palette.Debuff);
        _specials.Text = $"Specials  {stats.Specials} / {rules.MaxSpecials}";
    }

    private void RenderLines()
    {
        _lines.ClearChildren();
        _rows.Clear();
        foreach (var group in Editor.Lines.GroupBy(l => l.Group))
        {
            _lines.AddChild(GroupHeader(group.Key, group.Sum(l => l.Count)));
            foreach (var line in group)
            {
                var row = new DeckLineRow(line, new CardInstance(_nextInstanceId++, line.Card, Seat.One));
                row.Pressed += () => Remove(row);
                _rows[line.Card.Id] = row;
                _lines.AddChild(row);
            }
        }

        if (_rows.Count == 0)
        {
            var empty = Widgets.Label("This deck is empty. Click cards in the book to add them.", 17, Palette.Muted, HorizontalAlignment.Center);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _lines.AddChild(empty);
        }
    }

    private static HBoxContainer GroupHeader(BookFilter group, int count)
    {
        var icon = group switch
        {
            BookFilter.Melee => Icons.ForRow(RowType.Melee),
            BookFilter.Ranged => Icons.ForRow(RowType.Ranged),
            BookFilter.Siege => Icons.ForRow(RowType.Siege),
            _ => Icons.SpecialIcon,
        };
        var header = new HBoxContainer().WithGap(8);
        header.AddChild(Icons.Rect(icon, 18, Palette.Muted));
        header.AddChild(Widgets.Label(group == BookFilter.Special ? "Special" : group.ToString(), 0, Palette.Muted).AsDisplay(16));
        header.AddChild(Widgets.Label($"({count})", 16, Palette.Muted));
        return header;
    }

    /// <summary>Removes one copy: the last copy's line fades out, otherwise only its count does.</summary>
    private void Remove(DeckLineRow row)
    {
        if (row.IsLeaving)
        {
            return;
        }

        var cardId = row.Line.Card.Id;
        var scale = _durationScale();
        if (scale <= 0)
        {
            CommitRemove(cardId);
            return;
        }

        row.IsLeaving = true;
        var fading = row.Line.Count > 1 ? (CanvasItem)row.CountLabel : row;
        var tween = row.CreateTween();
        tween.TweenProperty(fading, "modulate:a", 0f, RemoveFadeDuration * scale);
        tween.TweenCallback(Callable.From(() => CommitRemove(cardId)));
    }

    private void CommitRemove(string cardId)
    {
        if (Editor.Remove(cardId))
        {
            Save();
            Render();
            CardsChanged?.Invoke();
        }
    }

    private void ChangeLeader(string leaderId)
    {
        if (Editor.SetLeader(leaderId))
        {
            Save();
            Render();
        }
    }

    private void ChangeCardBack(string backId)
    {
        if (Editor.SetCardBack(backId))
        {
            Save();
            Render();
        }
    }

    private void Rename() => TextInputDialog.Show(this, "Rename deck", Editor.Name, DeckEditor.MaxNameLength, text =>
    {
        if (!Editor.Rename(text))
        {
            return false;
        }

        Save();
        Render();
        return true;
    });

    private void ConfirmDelete() => ConfirmDialog.Show(
        this,
        "Delete deck",
        $"Delete \"{Editor.Name}\"? This cannot be undone.",
        "Delete",
        () =>
        {
            _store.Delete(Editor.Id);
            Deleted?.Invoke();
        },
        ModalStyle.Game);
}
