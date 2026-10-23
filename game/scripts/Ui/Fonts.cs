using Godot;

namespace Gwota.Game.Ui;

/// <summary>
/// The game's two OFL typefaces: body (<see cref="BodyFamily"/>, Alegreya Sans) for information and display
/// (Cinzel caps) for titles and names. Missing files fall back to the engine's built-in font.
/// </summary>
public static class Fonts
{
    /// <summary>File prefix of the body font in assets/fonts: "-Medium", "-Bold" and "-MediumItalic" .ttf files.</summary>
    public const string BodyFamily = "AlegreyaSans";

    private const string FontsDirectory = "res://assets/fonts/";
    private const string CinzelPath = FontsDirectory + "Cinzel.ttf";

    private static Font? _body;
    private static Font? _display;
    private static Font? _bold;
    private static Font? _italic;

    /// <summary>Regular body font, the theme default.</summary>
    public static Font Body => _body ??= LoadBody("Medium") ?? ThemeDB.FallbackFont;

    /// <summary>Bold Cinzel, or the body font if the asset is missing.</summary>
    public static Font Display => _display ??= CreateDisplay();

    /// <summary>Bold body font, used for keywords in rich text.</summary>
    public static Font Bold => _bold ??= LoadBody("Bold") ?? new FontVariation { BaseFont = Body, VariationEmbolden = 0.9f };

    /// <summary>Italic body font, used for flavor text.</summary>
    public static Font Italic => _italic ??= LoadBody("MediumItalic") ?? new FontVariation
    {
        BaseFont = Body,
        VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(0.22f, 1), Vector2.Zero),
    };

    /// <summary>Applies the display font (and an optional size) to a control.</summary>
    public static T WithDisplayFont<T>(this T control, int? size = null)
        where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);

        control.AddThemeFontOverride("font", Display);
        if (size is { } fontSize)
        {
            control.AddThemeFontSizeOverride("font_size", fontSize);
        }

        return control;
    }

    /// <summary>Applies the bold body font (and an optional size), for headings on technical screens.</summary>
    public static T WithBoldFont<T>(this T control, int? size = null)
        where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);

        control.AddThemeFontOverride("font", Bold);
        if (size is { } fontSize)
        {
            control.AddThemeFontSizeOverride("font_size", fontSize);
        }

        return control;
    }

    /// <summary>
    /// Display text for a button or label in the display font, in capitals. Game elements (match, cards, deck builder)
    /// use it; menus, settings and network screens stay in the body font.
    /// </summary>
    public static T AsDisplay<T>(this T control, int? size = null)
        where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);

        switch (control)
        {
            case Label label:
                label.Text = label.Text.ToUpperInvariant();
                break;
            case Button button:
                button.Text = button.Text.ToUpperInvariant();
                break;
        }

        return control.WithDisplayFont(size);
    }

    /// <summary>
    /// Loads a body style with lining figures: Alegreya defaults to old-style digits, which bob up and down in
    /// counters and scores.
    /// </summary>
    private static FontVariation? LoadBody(string style)
    {
        var path = $"{FontsDirectory}{BodyFamily}-{style}.ttf";
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        var liningFigures = TextServerManager.GetPrimaryInterface().NameToTag("lnum");
        return new FontVariation
        {
            BaseFont = GD.Load<Font>(path),
            OpentypeFeatures = new Godot.Collections.Dictionary { [liningFigures] = 1 },
        };
    }

    private static Font CreateDisplay()
    {
        if (!ResourceLoader.Exists(CinzelPath))
        {
            return Body;
        }

        var weight = TextServerManager.GetPrimaryInterface().NameToTag("weight");
        return new FontVariation
        {
            BaseFont = GD.Load<Font>(CinzelPath),
            VariationOpentype = new Godot.Collections.Dictionary { [weight] = 700 },
        };
    }
}
