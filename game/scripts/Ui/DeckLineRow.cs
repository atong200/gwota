using Godot;
using Gwota.Core.Cards;
using Gwota.Core.DeckBuilding;

namespace Gwota.Game.Ui;

/// <summary>
/// One line of the deck list: strength (or a star for specials), name and copies. Hovering shows the card preview;
/// the deck builder removes a copy when it is pressed.
/// </summary>
public sealed partial class DeckLineRow : Button
{
    private const float Height = 34;

    private readonly CardInstance _card;

    public DeckLineRow(DeckLine line, CardInstance card)
    {
        Line = line ?? throw new ArgumentNullException(nameof(line));
        _card = card ?? throw new ArgumentNullException(nameof(card));
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(0, Height);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var definition = line.Card;
        var nameColor = definition.IsHero ? Palette.Hero : Palette.Text;

        Control strength = definition.IsUnit
            ? Widgets.Label(definition.Strength.ToString(System.Globalization.CultureInfo.InvariantCulture), 0, nameColor, HorizontalAlignment.Center).WithDisplayFont(16)
            : Icons.Rect(Icons.SpecialIcon, 14, Palette.Muted);
        strength.CustomMinimumSize = new Vector2(28, 0);

        var name = Widgets.Label(definition.Name, 17, nameColor);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (definition.IsHero)
        {
            name.AddThemeFontOverride("font", Fonts.Bold);
        }

        CountLabel = Widgets.Label(line.Count > 1 ? $"x{line.Count}" : string.Empty, 17, Palette.Highlight, HorizontalAlignment.Right);
        CountLabel.CustomMinimumSize = new Vector2(30, 0);

        var content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 8);
        content.AddChild(strength);
        content.AddChild(name);
        content.AddChild(CountLabel);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddChild(content);
        AddChild(margin);

        var faction = Palette.ForFaction(definition.Faction);
        UiTheme.TintButton(this, Palette.Button.Lerp(faction, 0.35f));

        MouseEntered += () => CardPreview.Find(this)?.ShowFor(this, _card);
        MouseExited += () => CardPreview.Find(this)?.HideFor(this);
    }

    public DeckLine Line { get; }

    public Label CountLabel { get; }

    /// <summary>Set while the row fades out after a click, so repeated clicks are ignored.</summary>
    public bool IsLeaving { get; set; }

    public override void _ExitTree() => CardPreview.Find(this)?.HideFor(this);

    /// <summary>Briefly brightens the row (a card was just added to it).</summary>
    public void Pulse(double duration)
    {
        Modulate = new Color(1.6f, 1.5f, 1.1f);
        CreateTween().TweenProperty(this, "modulate", Colors.White, duration);
    }
}
