using Godot;
using Gwota.Core.Cards;

namespace Gwota.Game.Ui;

/// <summary>Central colors so the placeholder look can be restyled in one place.</summary>
public static class Palette
{
    public static readonly Color Background = new("14161b");
    public static readonly Color Panel = new("1f232b");
    public static readonly Color PanelBorder = new("353b47");
    public static readonly Color Row = new("272b34");
    public static readonly Color RowWeather = new("24364f");
    public static readonly Color Text = new("e8e6e3");
    public static readonly Color Muted = new("9aa0a6");
    public static readonly Color Highlight = new("f0c419");
    public static readonly Color Target = new("4fc3f7");
    public static readonly Color Hero = new("d4af37");
    public static readonly Color Common = new("c3c9d2");
    public static readonly Color BadgeBackground = new("101216");
    public static readonly Color Gem = new("e0313f");
    public static readonly Color GemSlot = new("08090b");
    public static readonly Color Buff = new("7ee07e");
    public static readonly Color Debuff = new("ff7070");
    public static readonly Color Dim = new(0, 0, 0, 0.7f);
    public static readonly Color TextDisabled = new("6b7078");
    public static readonly Color Button = new("2c3341");
    public static readonly Color ButtonHover = new("3a4356");
    public static readonly Color ButtonPressed = new("232833");
    public static readonly Color ButtonBorder = new("5a6478");
    public static readonly Color ButtonDisabled = new("1d2027");
    public static readonly Color ButtonDisabledBorder = new("2e333d");
    public static readonly Color Danger = new("8a2c2c");

    /// <summary>Book page background: a light tint of the faction color, or the plain panel for Neutral.</summary>
    public static Color PageTint(Faction faction) =>
        faction == Faction.Neutral ? Panel : Panel.Lerp(ForFaction(faction).Lightened(0.35f), 0.45f);

    public static Color ForFaction(Faction faction) => faction switch
    {
        Faction.Realms => new Color("2f4f8a"),
        Faction.Hells => new Color("7a2a2a"),
        _ => new Color("4a4f5a"),
    };
}
