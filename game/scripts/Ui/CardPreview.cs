using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Presentation;

namespace Gwota.Game.Ui;

/// <summary>
/// Floating card details that follow the mouse while a <see cref="CardView"/> is hovered.
/// One instance lives on a top canvas layer; card views find it through a group.
/// </summary>
public sealed partial class CardPreview : CanvasLayer
{
    public const string Group = "card_preview";

    private static readonly Vector2 CardSize = new(200, 282);
    private static readonly Vector2 CursorOffset = new(22, 22);
    private const int TextWidth = 300;

    private readonly PanelContainer _panel;
    private readonly StyleBoxFlat _style;
    private readonly Control _cardSlot;
    private readonly Label _name;
    private readonly HBoxContainer _rowIcons;
    private readonly ColorRect _separator;
    private readonly RichTextLabel _rules;
    private readonly RichTextLabel _flavor;
    private object? _source;

    public CardPreview()
    {
        Layer = 100;
        AddToGroup(Group);

        _cardSlot = new Control { CustomMinimumSize = CardSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        _name = Widgets.Label("", 0).WithDisplayFont(22);
        _name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _rowIcons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _rowIcons.AddThemeConstantOverride("separation", 4);
        _separator = new ColorRect { CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore };
        _rules = CreateRules();
        _flavor = CreateRules();

        var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 10);
        header.AddChild(_name);
        header.AddChild(_rowIcons);

        var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(TextWidth, 0) };
        text.AddThemeConstantOverride("separation", 8);
        text.AddChild(header);
        text.AddChild(_separator);
        text.AddChild(_rules);
        text.AddChild(new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        text.AddChild(_flavor);

        var layout = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 16);
        layout.AddChild(_cardSlot);
        layout.AddChild(text);

        _style = Widgets.Box(Palette.Panel, Palette.Common, 2, radius: 10, margin: 14);
        _style.AntiAliasing = true;
        _panel = Widgets.Panel(layout, _style);
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.Visible = false;
        AddChild(_panel);
    }
    /// <summary>The preview of the scene <paramref name="from"/> belongs to, or null when none is hosted.</summary>
    public static CardPreview? Find(Node from) =>
        from.IsInsideTree() ? from.GetTree().GetFirstNodeInGroup(Group) as CardPreview : null;

    public bool IsShowing => _panel.Visible;

    public void ShowFor(CardView source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ShowFor(source, source.Card, source.CurrentStrength);
    }

    /// <summary>Shows <paramref name="card"/> on behalf of any hovered element (e.g. a deck list row).</summary>
    public void ShowFor(object source, CardInstance card, int? strength = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(card);

        _source = source;
        var definition = card.Definition;
        var rarity = definition.IsHero ? Palette.Hero : Palette.Common;

        _name.Text = definition.Name;
        _name.AddThemeColorOverride("font_color", definition.IsHero ? Palette.Hero : Palette.Text);
        _rowIcons.ClearChildren();
        foreach (var row in definition.IsUnit ? definition.Rows : [])
        {
            var icon = Icons.Rect(Icons.ForRow(row), 22, rarity);
            _rowIcons.AddChild(icon);
        }

        _separator.Color = new Color(rarity, 0.6f);
        _rules.Text = RulesBbCode(definition);
        _flavor.Text = FlavorBbCode(definition);
        _flavor.Visible = _flavor.Text.Length > 0;
        _style.BorderColor = rarity;
        _style.SetBorderWidthAll(definition.IsHero ? 3 : 2);
        _style.ShadowColor = new Color(Palette.Hero, definition.IsHero ? 0.45f : 0f);
        _style.ShadowSize = definition.IsHero ? 10 : 0;

        _cardSlot.ClearChildren();
        _cardSlot.AddChild(new CardView(card, CardSize, strength, showName: false)
        {
            ShowsPreview = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _panel.Visible = true;
        _panel.ResetSize();
        FollowMouse();
    }

    /// <summary>Hides the preview if <paramref name="source"/> is the card it currently shows.</summary>
    public void HideFor(object source)
    {
        if (_source != source)
        {
            return;
        }

        _source = null;
        _panel.Visible = false;
        _cardSlot.ClearChildren();
    }

    public override void _Process(double delta)
    {
        if (_panel.Visible)
        {
            FollowMouse();
        }
    }

    /// <summary>Rules lines as BBCode, keywords in bold (flavor excluded, see <see cref="FlavorBbCode"/>).</summary>
    public static string RulesBbCode(CardDefinition definition)
    {
        var lines = CardText.Rules(definition).Where(line => !line.IsFlavor).Select(line =>
            line.Keyword is null ? Escape(line.Text) : $"[b]{Escape(line.Keyword)}:[/b] {Escape(line.Text)}");
        return string.Join("\n", lines);
    }

    /// <summary>Flavor text in muted italics, shown at the bottom of the preview; empty when the card has none.</summary>
    public static string FlavorBbCode(CardDefinition definition)
    {
        var lines = CardText.Rules(definition).Where(line => line.IsFlavor)
            .Select(line => $"[i][color=#{Palette.Muted.ToHtml(false)}]{Escape(line.Text)}[/color][/i]");
        return string.Join("\n", lines);
    }

    private static string Escape(string text) => text.Replace("[", "[lb]", StringComparison.Ordinal);

    private static RichTextLabel CreateRules()
    {
        var rules = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(TextWidth, 0),
        };
        rules.AddThemeFontSizeOverride("normal_font_size", 18);
        rules.AddThemeFontSizeOverride("bold_font_size", 18);
        rules.AddThemeFontSizeOverride("italics_font_size", 17);
        rules.AddThemeFontOverride("bold_font", Fonts.Bold);
        rules.AddThemeFontOverride("italics_font", Fonts.Italic);
        rules.AddThemeColorOverride("default_color", Palette.Text);
        rules.AddThemeConstantOverride("line_separation", 6);
        return rules;
    }
    /// <summary>Sits at the bottom-right of the cursor, flipping to the other side near the screen edges.</summary>
    private void FollowMouse()
    {
        var mouse = _panel.GetViewport().GetMousePosition();
        var screen = _panel.GetViewportRect().Size;
        var size = _panel.Size;

        var x = mouse.X + CursorOffset.X + size.X > screen.X ? mouse.X - CursorOffset.X - size.X : mouse.X + CursorOffset.X;
        var y = mouse.Y + CursorOffset.Y + size.Y > screen.Y ? mouse.Y - CursorOffset.Y - size.Y : mouse.Y + CursorOffset.Y;
        _panel.Position = new Vector2(Mathf.Clamp(x, 0, Math.Max(0, screen.X - size.X)), Mathf.Clamp(y, 0, Math.Max(0, screen.Y - size.Y)));
    }
}
