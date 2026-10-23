using Godot;

namespace Gwota.Game.Ui;

/// <summary>
/// The game-wide <see cref="Theme"/>, set once on the root so every control inherits it. Buttons get a visible
/// background and border so they read as clickable without hovering.
/// </summary>
public static class UiTheme
{
    private const int DefaultFontSize = 18;

    public static Theme Create()
    {
        // Layers outside the root (card preview, overlays, tooltips) do not inherit the root theme, so the body font
        // also replaces the engine fallback.
        var body = Fonts.Body;
        ThemeDB.FallbackFont = body;
        var theme = new Theme { DefaultFont = body, DefaultFontSize = DefaultFontSize };
        StyleButtons(theme, nameof(Button));
        StyleButtons(theme, nameof(OptionButton));
        StyleSlider(theme);
        StyleTabs(theme);
        StyleTooltip(theme);
        return theme;
    }

    /// <summary>
    /// Applies the button look to one button, based on a custom background (e.g. a faction color);
    /// <paramref name="padding"/> overrides the content margins (small for icon buttons).
    /// </summary>
    public static void TintButton(Button button, Color background, int? padding = null)
    {
        ArgumentNullException.ThrowIfNull(button);

        button.AddThemeStyleboxOverride("normal", ButtonBox(background, Palette.ButtonBorder, padding));
        button.AddThemeStyleboxOverride("hover", ButtonBox(background.Lightened(0.15f), Palette.Highlight, padding));
        button.AddThemeStyleboxOverride("pressed", ButtonBox(background.Darkened(0.2f), Palette.Highlight, padding));
        button.AddThemeStyleboxOverride("disabled", ButtonBox(Palette.ButtonDisabled, Palette.ButtonDisabledBorder, padding));
    }

    private static void StyleButtons(Theme theme, string type)
    {
        theme.SetStylebox("normal", type, ButtonBox(Palette.Button, Palette.ButtonBorder));
        theme.SetStylebox("hover", type, ButtonBox(Palette.ButtonHover, Palette.Highlight));
        theme.SetStylebox("pressed", type, ButtonBox(Palette.ButtonPressed, Palette.Highlight));
        theme.SetStylebox("hover_pressed", type, ButtonBox(Palette.ButtonPressed, Palette.Highlight));
        theme.SetStylebox("disabled", type, ButtonBox(Palette.ButtonDisabled, Palette.ButtonDisabledBorder));
        theme.SetStylebox("focus", type, new StyleBoxEmpty());

        theme.SetColor("font_color", type, Palette.Text);
        theme.SetColor("font_hover_color", type, Colors.White);
        theme.SetColor("font_pressed_color", type, Palette.Highlight);
        theme.SetColor("font_hover_pressed_color", type, Palette.Highlight);
        theme.SetColor("font_disabled_color", type, Palette.TextDisabled);
        theme.SetColor("icon_normal_color", type, Palette.Text);
        theme.SetColor("icon_hover_color", type, Colors.White);
        theme.SetColor("icon_pressed_color", type, Palette.Highlight);
        theme.SetColor("icon_disabled_color", type, Palette.TextDisabled);
    }

    private static StyleBoxFlat ButtonBox(Color background, Color border, int? padding = null)
    {
        var box = Widgets.Box(background, border, 1, radius: 6);
        box.ContentMarginLeft = box.ContentMarginRight = padding ?? 14;
        box.ContentMarginTop = box.ContentMarginBottom = padding ?? 6;
        return box;
    }

    private static void StyleSlider(Theme theme)
    {
        var track = Widgets.Box(Palette.Button, Palette.ButtonBorder, 1, radius: 4, margin: 0);
        track.ContentMarginTop = track.ContentMarginBottom = 4;
        var filled = Widgets.Box(Palette.Highlight.Darkened(0.25f), radius: 4, margin: 0);
        filled.ContentMarginTop = filled.ContentMarginBottom = 4;

        theme.SetStylebox("slider", nameof(HSlider), track);
        theme.SetStylebox("grabber_area", nameof(HSlider), filled);
        theme.SetStylebox("grabber_area_highlight", nameof(HSlider), filled);
    }

    private static void StyleTabs(Theme theme)
    {
        const string type = nameof(TabContainer);
        var selected = Widgets.Box(Palette.Panel, Palette.Highlight, 0, radius: 0, margin: 10);
        selected.BorderWidthBottom = 2;
        theme.SetStylebox("tab_selected", type, selected);
        theme.SetStylebox("tab_unselected", type, Widgets.Box(Palette.Background, radius: 0, margin: 10));
        theme.SetStylebox("tab_hovered", type, Widgets.Box(Palette.ButtonHover, radius: 0, margin: 10));
        theme.SetStylebox("panel", type, Widgets.Box(Palette.Panel, Palette.PanelBorder, 1, radius: 0, margin: 16));
        theme.SetColor("font_selected_color", type, Palette.Highlight);
        theme.SetColor("font_unselected_color", type, Palette.Muted);
        theme.SetColor("font_hovered_color", type, Palette.Text);
    }

    private static void StyleTooltip(Theme theme)
    {
        theme.SetStylebox("panel", "TooltipPanel", Widgets.Box(Palette.Panel, Palette.PanelBorder, 1, radius: 4, margin: 6));
        theme.SetColor("font_color", "TooltipLabel", Palette.Text);
    }
}
