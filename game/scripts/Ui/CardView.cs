using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Presentation;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

public enum CardHighlight
{
    None,
    Selected,
    Target,

    /// <summary>Marked to be sent back to the deck during the draft phase.</summary>
    Redraw,
}

/// <summary>
/// One card face: art (or a faction-colored placeholder with initials), strength badge top-left (star for specials),
/// row icons above the name banner. Heroes get a gold, glowing frame, others silver; all scales with the card width.
/// </summary>
public sealed partial class CardView : PanelContainer
{
    public static readonly Vector2 HandSize = new(86, 122);
    public static readonly Vector2 BoardSize = new(64, 90);

    private readonly StyleBoxFlat _frame;
    private readonly float _scale;
    private ColorRect? _redrawMark;

    public CardView(CardInstance card, Vector2 size, int? currentStrength = null, bool showName = true)
    {
        ArgumentNullException.ThrowIfNull(card);

        Card = card;
        CurrentStrength = currentStrength;
        var definition = card.Definition;
        CustomMinimumSize = size;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _scale = size.X / HandSize.X;

        _frame = Widgets.Box(Palette.ForFaction(definition.Faction).Darkened(0.25f), radius: Px(6), margin: 0);
        _frame.AntiAliasing = true;
        ApplyFrame(CardHighlight.None);
        AddThemeStyleboxOverride("panel", _frame);

        AddChild(CardFace.Art(CardArt.ForCard(definition.Id), definition.Name, Px(28, min: 16)));

        var overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        overlay.AddChild(Badge(definition, currentStrength));
        if (definition.IsUnit)
        {
            overlay.AddChild(RowIcons(definition, showName));
        }

        if (showName)
        {
            overlay.AddChild(NameBanner(definition.Name));
        }

        AddChild(overlay);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }
    public event Action<CardView>? Clicked;

    public event Action<CardView>? Hovered;

    public event Action<CardView>? Unhovered;

    public CardInstance Card { get; }

    /// <summary>The strength displayed on the badge (null shows the base strength).</summary>
    public int? CurrentStrength { get; }

    /// <summary>Whether hovering shows the floating <see cref="CardPreview"/> (off for the preview's own card).</summary>
    public bool ShowsPreview { get; set; } = true;

    private bool IsHero => Card.Definition.IsHero;

    private Color RarityColor => IsHero ? Palette.Hero : Palette.Common;

    public void SetHighlight(CardHighlight highlight)
    {
        ApplyFrame(highlight);

        if (highlight == CardHighlight.Redraw)
        {
            _redrawMark ??= CreateRedrawMark();
        }

        if (_redrawMark is not null)
        {
            _redrawMark.Visible = highlight == CardHighlight.Redraw;
        }
    }

    /// <summary>Frame border and glow: rarity colored at rest, replaced by the highlight color when highlighted.</summary>
    private void ApplyFrame(CardHighlight highlight)
    {
        var (color, width, glow) = highlight switch
        {
            CardHighlight.Selected => (Palette.Highlight, 3f, 6f),
            CardHighlight.Target => (Palette.Target, 3f, 6f),
            CardHighlight.Redraw => (Palette.Debuff, 3f, 0f),
            _ => (RarityColor, IsHero ? 2.5f : 1.5f, IsHero ? 4f : 0f),
        };

        _frame.BorderColor = color;
        _frame.SetBorderWidthAll(Math.Max(1, (int)MathF.Round(width * Math.Min(_scale, 1.6f))));
        _frame.ShadowColor = new Color(color, 0.55f);
        _frame.ShadowSize = (int)MathF.Round(glow * Math.Min(_scale, 1.6f));
    }
    /// <summary>A dimming layer with a centered "REDRAW" tag, stacked over the card by the PanelContainer.</summary>
    private ColorRect CreateRedrawMark()
    {
        var label = Widgets.Label("REDRAW", 14, Colors.White, HorizontalAlignment.Center).Outlined();
        var tag = Widgets.Panel(label, Widgets.Box(new Color(Palette.Debuff, 0.85f), radius: 3, margin: 2));
        tag.MouseFilter = MouseFilterEnum.Ignore;

        var mark = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        mark.AddChild(tag);

        var layer = new ColorRect { Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Ignore };
        layer.AddChild(mark);
        mark.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(layer);
        return layer;
    }

    public override void _ExitTree() => CardPreview.Find(this)?.HideFor(this);

    private void OnMouseEntered()
    {
        if (ShowsPreview)
        {
            CardPreview.Find(this)?.ShowFor(this);
        }

        Hovered?.Invoke(this);
    }

    private void OnMouseExited()
    {
        CardPreview.Find(this)?.HideFor(this);
        Unhovered?.Invoke(this);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Clicked?.Invoke(this);
            AcceptEvent();
        }
    }

    /// <summary>A length designed for <see cref="HandSize"/>, scaled to this card's width.</summary>
    private int Px(float baseSize, int min = 1) => Math.Max(min, (int)MathF.Round(baseSize * _scale));

    private PanelContainer Badge(CardDefinition definition, int? currentStrength)
    {
        var diameter = Px(30, min: 18);
        var ring = Widgets.Box(new Color(Palette.BadgeBackground, 0.94f), RarityColor, Px(2), radius: diameter, margin: 0);
        ring.AntiAliasing = true;

        Control content;
        if (definition.IsUnit)
        {
            var strength = currentStrength ?? definition.Strength;
            var color = strength > definition.Strength ? Palette.Buff
                : strength < definition.Strength ? Palette.Debuff
                : IsHero ? Palette.Hero : Palette.Text;
            var label = Widgets.Label(strength.ToString(System.Globalization.CultureInfo.InvariantCulture), 0, color, HorizontalAlignment.Center)
                .WithDisplayFont(Px(17, min: 11));
            label.VerticalAlignment = VerticalAlignment.Center;
            content = label;
        }
        else
        {
            content = Icons.Rect(Icons.SpecialIcon, diameter * 0.55f, Palette.Highlight);
        }

        var badge = Widgets.Panel(content, ring);
        badge.MouseFilter = MouseFilterEnum.Ignore;
        badge.Position = new Vector2(Px(3), Px(3));
        badge.Size = badge.CustomMinimumSize = new Vector2(diameter, diameter);
        return badge;
    }

    /// <summary>The playable rows' icons on a dark pill, in the bottom-right corner just above the name banner.</summary>
    private PanelContainer RowIcons(CardDefinition definition, bool aboveBanner)
    {
        var icons = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        icons.AddThemeConstantOverride("separation", Px(1));
        foreach (var row in definition.Rows)
        {
            icons.AddChild(Icons.Rect(Icons.ForRow(row), Px(13, min: 9), Palette.Text));
        }

        var pill = Widgets.Panel(icons, Widgets.Box(new Color(Palette.BadgeBackground, 0.8f), radius: Px(8), margin: Px(2)));
        pill.MouseFilter = MouseFilterEnum.Ignore;
        pill.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        pill.GrowHorizontal = GrowDirection.Begin;
        pill.GrowVertical = GrowDirection.Begin;
        var bottom = aboveBanner ? BannerHeight + Px(2) : Px(3);
        pill.OffsetLeft = pill.OffsetRight = -Px(3);
        pill.OffsetTop = pill.OffsetBottom = -bottom;
        return pill;
    }

    private int BannerFontSize => Px(10, min: 8);

    /// <summary>Height of the name banner: one line of its font plus its margins.</summary>
    private int BannerHeight => (int)MathF.Ceiling(BannerFontSize * 1.45f) + (2 * Px(2));

    private PanelContainer NameBanner(string name) => CardFace.NameBanner(name, BannerFontSize, Px(2));

}