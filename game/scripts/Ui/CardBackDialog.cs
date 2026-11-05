using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;
using Gwota.Core.Presentation;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// Picks a deck's card back from <see cref="CardArt.CardBacks"/>, shown like the card book: pages of two rows of four
/// with arrows when they do not fit. Opens on the page of the current back; clicking a back confirms.
/// </summary>
public sealed partial class CardBackDialog : Modal
{
    private const int Columns = 4;
    private const int Rows = 2;
    private const int Gap = 26;
    private static readonly Vector2 BackSize = CardView.OfHeight(228);

    private readonly string _currentId;
    private readonly Faction _faction;
    private readonly Action<string> _onChosen;
    private readonly Pager<CardBackEntry> _pager;
    private readonly GridContainer _grid = new() { Columns = Columns, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
    private readonly PagerBar _pages;

    private CardBackDialog(CardBackCatalog catalog, CardBackStyle current, Action<string> onChosen)
        : base((int)((Columns * (BackSize.X + Gap)) - Gap), ModalStyle.Game)
    {
        _currentId = catalog.Resolve(current.Id).Id;
        _faction = current.Faction;
        _onChosen = onChosen;
        _pager = new Pager<CardBackEntry>(catalog.Entries, Columns * Rows);
        _pager.ShowFirst(e => e.Id == _currentId);

        Content.AddChild(Title("Choose a card back", 30));
        _grid.AddThemeConstantOverride("h_separation", Gap);
        // Room above each row for the chosen back's bookmark.
        var bookmark = SelectionFrame.Overhang(BackSize.Y);
        _grid.AddThemeConstantOverride("v_separation", Gap + bookmark);
        var room = new MarginContainer();
        room.AddThemeConstantOverride("margin_top", bookmark);
        room.AddChild(_grid);
        Content.AddChild(room);

        _pages = new PagerBar(() => Turn(_pager.Previous()), () => Turn(_pager.Next()), 140, Palette.Muted).WithGap(12);
        _pages.Alignment = BoxContainer.AlignmentMode.Center;
        _pages.Visible = _pager.HasPages;
        Content.AddChild(_pages);

        var cancel = MenuButton("Cancel", Cancel);
        cancel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        cancel.CustomMinimumSize = new Vector2(280, cancel.CustomMinimumSize.Y);
        Content.AddChild(cancel);

        Render();
    }

    /// <summary>
    /// Changes a deck's card back (shown in the deck's faction colours); Escape or Cancel keeps <paramref name="current"/>.
    /// </summary>
    public static void Show(Node parent, CardBackStyle current, Action<string> onChosen)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(onChosen);
        parent.AddChild(new CardBackDialog(CardArt.CardBacks(), current, onChosen));
    }

    private void Turn(bool turned)
    {
        if (turned)
        {
            Render();
        }
    }

    private void Render()
    {
        _grid.ClearChildren();
        var entries = _pager.CurrentPage;
        foreach (var entry in entries)
        {
            _grid.AddChild(Cell(entry));
        }

        // Pad the last page so the dialog keeps its size while paging.
        for (var i = _pager.HasPages ? entries.Count : _pager.PageSize; i < _pager.PageSize; i++)
        {
            _grid.AddChild(Stack(new Control { CustomMinimumSize = BackSize, MouseFilter = MouseFilterEnum.Ignore }, " ", false));
        }

        _pages.Show($"Page {_pager.Page + 1} / {_pager.PageCount}", _pager.HasPrevious, _pager.HasNext);

    }

    private VBoxContainer Cell(CardBackEntry entry)
    {
        var tile = new CardBackTile(new CardBackStyle(entry.Id, _faction), BackSize) { SizeFlagsHorizontal = SizeFlags.ShrinkCenter, TooltipText = entry.Name };
        tile.SetSelected(entry.Id == _currentId);
        tile.Clicked += _ =>
        {
            Close();
            _onChosen(entry.Id);
        };

        return Stack(tile, entry.Name, true);
    }

    // Every cell, filler included, reserves the name line so the page height never changes.
    private static VBoxContainer Stack(Control back, string name, bool visible)
    {
        var title = Widgets.Label(name, 0, Palette.Text, HorizontalAlignment.Center).AsDisplay();
        title.AddThemeFontSizeOverride("font_size", Fonts.FittingSize(Fonts.Display, title.Text, 17, BackSize.X, 12));
        title.CustomMinimumSize = new Vector2(BackSize.X, 0);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.Modulate = visible ? Colors.White : Colors.Transparent;

        // Room below the back for the selection band.
        var cell = new VBoxContainer().WithGap(16);
        cell.AddChild(back);
        cell.AddChild(title);
        return cell;
    }
}
