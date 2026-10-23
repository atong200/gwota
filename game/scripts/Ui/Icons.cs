using Godot;
using Gwota.Core.Cards;

namespace Gwota.Game.Ui;

/// <summary>
/// Small UI icons kept as inline SVG and rasterized at runtime, so they need no editor import step.
/// Drawn in white; buttons tint them through the theme's icon colors.
/// </summary>
public static class Icons
{
    private const string Copy = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="#ffffff" stroke-width="1.6" stroke-linejoin="round">
          <rect x="7" y="7" width="10" height="11" rx="1.5"/>
          <path d="M13 4.5V3.5A1.5 1.5 0 0 0 11.5 2h-7A1.5 1.5 0 0 0 3 3.5v8A1.5 1.5 0 0 0 4.5 13H5"/>
        </svg>
        """;

    private const string Check = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="#ffffff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M4 10.5l4 4 8-9"/>
        </svg>
        """;

    /// <summary>Settings: an eight-tooth gear.</summary>
    private const string Gear = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
          <g fill="#ffffff">
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8"/>
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8" transform="rotate(45 12 12)"/>
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8" transform="rotate(90 12 12)"/>
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8" transform="rotate(135 12 12)"/>
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8" transform="rotate(180 12 12)"/>
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8" transform="rotate(225 12 12)"/>
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8" transform="rotate(270 12 12)"/>
            <rect x="10.2" y="1.5" width="3.6" height="5" rx="0.8" transform="rotate(315 12 12)"/>
          </g>
          <circle cx="12" cy="12" r="6.6" fill="none" stroke="#ffffff" stroke-width="3.4"/>
        </svg>
        """;

    /// <summary>Melee row: a sword.</summary>
    private const string Sword = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
          <g fill="#ffffff" transform="rotate(45 12 12)">
            <path d="M12 1.5l2 3v10.5h-4V4.5z"/>
            <rect x="6.5" y="15" width="11" height="2" rx="1"/>
            <rect x="11" y="17" width="2" height="4"/>
            <circle cx="12" cy="22" r="1.4"/>
          </g>
        </svg>
        """;

    /// <summary>Ranged row: a drawn bow with an arrow.</summary>
    private const string Bow = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
          <g fill="none" stroke="#ffffff" stroke-linecap="round">
            <path d="M8 2Q20 12 8 22" stroke-width="2.2"/>
            <path d="M8 2L5 12L8 22" stroke-width="1"/>
            <path d="M4 12H19" stroke-width="1.8"/>
            <path d="M4 12L2 10M4 12L2 14" stroke-width="1.4"/>
          </g>
          <path d="M23 12l-4.5-3v6z" fill="#ffffff"/>
        </svg>
        """;

    /// <summary>Siege row: a catapult.</summary>
    private const string Catapult = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
          <g fill="#ffffff">
            <rect x="2" y="15.5" width="18" height="2.6" rx="1"/>
            <path d="M9.5 15.5L13 8.5L16.5 15.5h-2L13 12.2L11.5 15.5z"/>
            <circle cx="20" cy="4.6" r="2.6"/>
          </g>
          <g fill="none" stroke="#ffffff" stroke-linecap="round">
            <path d="M4.5 15L19 5.5" stroke-width="2.2"/>
            <circle cx="5.5" cy="20" r="2.2" stroke-width="1.6"/>
            <circle cx="16.5" cy="20" r="2.2" stroke-width="1.6"/>
          </g>
        </svg>
        """;

    /// <summary>Special cards: a four-pointed star.</summary>
    private const string Star = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">
          <path d="M12 1.5L14.6 9.4L22.5 12L14.6 14.6L12 22.5L9.4 14.6L1.5 12L9.4 9.4z" fill="#ffffff"/>
        </svg>
        """;

    /// <summary>Rename: a pencil.</summary>
    private const string Pen = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="#ffffff" stroke-width="1.7" stroke-linejoin="round" stroke-linecap="round">
          <path d="M3 17l1-4.2L13.6 3.2a1.6 1.6 0 0 1 2.2 0l1 1a1.6 1.6 0 0 1 0 2.2L7.2 16z"/>
          <path d="M12 4.8l3.2 3.2"/>
        </svg>
        """;

    /// <summary>Delete: a cross.</summary>
    private const string Cross = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="#ffffff" stroke-width="2.4" stroke-linecap="round">
          <path d="M5 5l10 10M15 5L5 15"/>
        </svg>
        """;

    /// <summary>Previous page: a left chevron with a faint inner echo.</summary>
    private const string ChevronLeft = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="#ffffff" stroke-linecap="round" stroke-linejoin="round">
          <path d="M12.5 3.5L6 10l6.5 6.5" stroke-width="2.6"/>
          <path d="M16 6.5L12.5 10L16 13.5" stroke-width="1.6" stroke-opacity="0.55"/>
        </svg>
        """;

    /// <summary>Next page: a right chevron with a faint inner echo.</summary>
    private const string ChevronRight = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="#ffffff" stroke-linecap="round" stroke-linejoin="round">
          <path d="M7.5 3.5L14 10l-6.5 6.5" stroke-width="2.6"/>
          <path d="M4 6.5L7.5 10L4 13.5" stroke-width="1.6" stroke-opacity="0.55"/>
        </svg>
        """;

    /// <summary>Local network: three linked computers.</summary>
    private const string Network = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round">
          <rect x="8.5" y="2.5" width="7" height="5" rx="1"/>
          <rect x="2" y="16.5" width="7" height="5" rx="1"/>
          <rect x="15" y="16.5" width="7" height="5" rx="1"/>
          <path d="M12 7.5v5M5.5 16.5v-4h13v4"/>
        </svg>
        """;

    /// <summary>Online: a globe with meridians.</summary>
    private const string Globe = """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="1.6" stroke-linecap="round">
          <circle cx="12" cy="12" r="9.5"/>
          <ellipse cx="12" cy="12" rx="4.2" ry="9.5"/>
          <path d="M2.5 12h19M4.2 6.8h15.6M4.2 17.2h15.6"/>
        </svg>
        """;

    /// <summary>Refresh: a circular arrow.</summary>
    private const string Refresh = """
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20">
          <path d="M16.2 7.2A6.8 6.8 0 1 0 16.8 12" fill="none" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round"/>
          <path d="M17.6 2.6v5.6H12z" fill="#ffffff"/>
        </svg>
        """;

    /// <summary>Rasterization scale: icons are drawn at 3x their nominal size so they stay crisp when enlarged.</summary>
    private const float RasterScale = 3f;

    private static readonly Dictionary<string, Texture2D> Cache = [];

    public static Texture2D CopyIcon => Load(Copy);

    public static Texture2D CheckIcon => Load(Check);

    public static Texture2D GearIcon => Load(Gear, 1.5f);

    public static Texture2D PenIcon => Load(Pen, 1.5f);

    public static Texture2D CrossIcon => Load(Cross, 1.5f);

    public static Texture2D PreviousIcon => Load(ChevronLeft, 1.5f);

    public static Texture2D NextIcon => Load(ChevronRight, 1.5f);

    public static Texture2D SpecialIcon => Load(Star, RasterScale);

    public static Texture2D NetworkIcon => Load(Network, RasterScale);

    public static Texture2D GlobeIcon => Load(Globe, RasterScale);

    public static Texture2D RefreshIcon => Load(Refresh, 1.5f);

    /// <summary>The row symbol: sword (melee), bow (ranged) or catapult (siege).</summary>
    public static Texture2D ForRow(RowType row) => Load(
        row switch
        {
            RowType.Melee => Sword,
            RowType.Ranged => Bow,
            RowType.Siege => Catapult,
            _ => throw new ArgumentOutOfRangeException(nameof(row), row, null),
        },
        RasterScale);

    /// <summary>A small icon control (aspect kept, scaled to <paramref name="size"/>), tinted with <paramref name="color"/>.</summary>
    public static TextureRect Rect(Texture2D texture, float size, Color? color = null) => new()
    {
        Texture = texture,
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SelfModulate = color ?? Colors.White,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static Texture2D Load(string svg, float scale = 1f)
    {
        if (!Cache.TryGetValue(svg, out var texture))
        {
            var image = new Image();
            image.LoadSvgFromString(svg, scale);
            texture = ImageTexture.CreateFromImage(image);
            Cache[svg] = texture;
        }

        return texture;
    }
}
