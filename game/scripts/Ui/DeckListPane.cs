using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// The collection's right pane when no deck is open: the player's decks (click one to edit it) and a New deck button.
/// Incomplete decks are tagged, the tag's tooltip says why.
/// </summary>
public sealed partial class DeckListPane : VBoxContainer
{
    private const float RowHeight = 66;

    private readonly GameData _data;
    private readonly DeckStore _store;
    private readonly VBoxContainer _list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    public DeckListPane(GameData data, DeckStore store)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        AddThemeConstantOverride("separation", 12);

        AddChild(Widgets.Label("My decks", 0, Palette.Highlight, HorizontalAlignment.Center).AsDisplay(26));

        _list.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.AddChild(_list);
        AddChild(scroll);

        var create = Widgets.Button("New deck", () => NewDeckRequested?.Invoke()).AsDisplay(20);
        create.CustomMinimumSize = new Vector2(0, 50);
        AddChild(create);

        Rebuild();
    }

    /// <summary>The player picked a deck to edit (its id).</summary>
    public event Action<string>? EditRequested;

    public event Action? NewDeckRequested;

    public void Rebuild()
    {
        _list.ClearChildren();
        foreach (var deck in _store.Collection.Decks)
        {
            _list.AddChild(Row(deck));
        }
    }

    private Button Row(DeckDefinition deck)
    {
        var editor = new DeckEditor(deck, _data);
        var color = Palette.ForFaction(deck.Faction);

        var name = Widgets.Label(deck.Name, 20, Palette.Text);
        name.AddThemeFontOverride("font", Fonts.Bold);
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var title = new HBoxContainer();
        title.AddThemeConstantOverride("separation", 8);
        title.AddChild(name);
        if (!editor.IsPlayable)
        {
            title.AddChild(Widgets.Tag("Incomplete", Palette.Debuff, string.Join('\n', editor.Problems), 13));
        }

        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = AlignmentMode.Center };
        details.AddThemeConstantOverride("separation", 0);
        details.AddChild(title);
        details.AddChild(Widgets.Label($"{FactionInfo.Name(deck.Faction)} - {editor.Leader.Name}", 16, Palette.Muted));

        var content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.OffsetRight = -10;
        content.AddThemeConstantOverride("separation", 10);
        content.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(8, 0), MouseFilter = MouseFilterEnum.Ignore });
        content.AddChild(details);
        IgnoreMouse(content);

        var row = new Button
        {
            CustomMinimumSize = new Vector2(0, RowHeight),
            FocusMode = FocusModeEnum.None,
            TooltipText = $"Edit \"{deck.Name}\"",
            ClipContents = true,
        };
        UiTheme.TintButton(row, Palette.Panel.Lightened(0.04f), padding: 0);
        row.AddChild(content);
        row.Pressed += () => EditRequested?.Invoke(deck.Id);
        return row;
    }

    /// <summary>
    /// The row is one big button: its contents let clicks through. The Incomplete tag passes them on instead, so it
    /// keeps its tooltip and still opens the deck.
    /// </summary>
    private static void IgnoreMouse(Control control)
    {
        foreach (var child in control.GetChildren())
        {
            if (child is PanelContainer tag)
            {
                tag.MouseFilter = MouseFilterEnum.Pass;
            }
            else if (child is Control childControl)
            {
                childControl.MouseFilter = MouseFilterEnum.Ignore;
                IgnoreMouse(childControl);
            }
        }
    }
}