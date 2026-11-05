using Godot;
using Gwota.Core.Cards;

namespace Gwota.Game.Ui;

/// <summary>
/// A fabric band drawn around its parent control to mark the chosen item of a picker: a flat band in a saturated faction
/// color with darker hems, set off from the parent by a dark gap, and a bookmark tab with a gold arrow centered on its
/// top edge. Its size scales with the parent's height. Drawn outside the parent's rect (see <see cref="Overhang"/>);
/// ignores the mouse.
/// </summary>
public sealed partial class SelectionFrame : Control
{
    private const float BandRatio = 0.026f;
    private const float GapRatio = 0.012f;
    private const float TabHeightRatio = 0.1f;
    private const float TabWidthRatio = 0.46f;
    private const float Saturation = 1.35f;
    private const float Brightness = 1.15f;
    private static readonly Color GapColor = new(0.03f, 0.03f, 0.04f, 0.92f);
    private static readonly Color ArrowColor = Palette.Hero.Lightened(0.1f);
    private static readonly Color ArrowShadow = new(0, 0, 0, 0.55f);

    private readonly Color _color;
    private readonly Color _hem;
    private readonly int _radius;

    public SelectionFrame(Faction faction, int radius = 8)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var tone = Palette.ForFaction(faction);
        _color = Color.FromHsv(tone.H, MathF.Min(1, tone.S * Saturation), MathF.Min(1, tone.V * Brightness));
        _hem = _color.Darkened(0.45f);
        _radius = radius;
    }

    /// <summary>How far above a parent of <paramref name="height"/> the frame draws (gap, band and bookmark).</summary>
    public static int Overhang(float height) => (int)(Gap(height) + Band(height) + TabHeight(height));

    public override void _Draw()
    {
        // The parent's full rect, in this control's coordinates (a container parent may inset its children).
        var parent = GetParentControl()?.Size ?? Size;
        var band = Band(parent.Y);
        var gap = Gap(parent.Y);
        var inner = new Rect2(-Position, parent);
        var bandInner = inner.Grow(gap);
        var outer = bandInner.Grow(band);

        DrawStyleBox(Outline(GapColor, gap + 1, _radius), bandInner);
        DrawStyleBox(Outline(_color, band, _radius + (int)(gap + band)), outer);
        DrawStyleBox(Outline(_hem, 1, _radius + (int)(gap + band)), outer);
        DrawStyleBox(Outline(_hem, 1, _radius + (int)gap), bandInner);
        DrawBookmark(outer, band, TabHeight(parent.Y), MathF.Round(parent.X * TabWidthRatio));
    }

    private static float Band(float height) => MathF.Max(5, MathF.Round(height * BandRatio));

    private static float Gap(float height) => MathF.Max(3, MathF.Round(height * GapRatio));

    private static float TabHeight(float height) => MathF.Max(14, MathF.Round(height * TabHeightRatio));

    /// <summary>A tab rising from the band's top edge (merged into the band, hemmed on its free sides), with a gold arrow.</summary>
    private void DrawBookmark(Rect2 outer, float band, float height, float width)
    {
        var tab = new Rect2(outer.GetCenter().X - (width / 2), outer.Position.Y - height, width, height);
        var corner = (int)MathF.Round(height * 0.3f);
        DrawStyleBox(Tab(_color, null, corner), tab with { Size = tab.Size + new Vector2(0, band) });
        DrawStyleBox(Tab(null, _hem, corner), tab);

        var center = new Vector2(tab.GetCenter().X, tab.GetCenter().Y + (band * 0.3f));
        var arrow = Chevron(center, halfWidth: height * 0.5f, depth: height * 0.46f, thickness: MathF.Max(2, height * 0.18f));
        DrawSmoothPolygon(Offset(arrow, new Vector2(0, MathF.Max(1, height * 0.07f))), ArrowShadow);
        DrawSmoothPolygon(arrow, ArrowColor);
    }

    /// <summary>A filled polygon with an antialiased rim (filled polygons alone draw jagged edges).</summary>
    private void DrawSmoothPolygon(Vector2[] points, Color color)
    {
        DrawColoredPolygon(points, color);
        DrawPolyline([.. points, points[0]], color, 1, antialiased: true);
    }

    /// <summary>A box with rounded top corners and no bottom edge: either a fill or a hem outline.</summary>
    private static StyleBoxFlat Tab(Color? fill, Color? hem, int corner)
    {
        var box = new StyleBoxFlat { DrawCenter = fill is not null, BgColor = fill ?? default, BorderColor = hem ?? default, AntiAliasing = true };
        box.SetBorderWidthAll(hem is null ? 0 : 1);
        box.SetBorderWidth(Side.Bottom, 0);
        box.CornerRadiusTopLeft = corner;
        box.CornerRadiusTopRight = corner;
        return box;
    }

    /// <summary>A downward "V" of uniform vertical <paramref name="thickness"/>, centered on <paramref name="center"/>.</summary>
    private static Vector2[] Chevron(Vector2 center, float halfWidth, float depth, float thickness)
    {
        var top = center.Y - (depth / 2);
        var tip = center.Y + (depth / 2);
        return
        [
            new(center.X - halfWidth, top),
            new(center.X, tip - thickness),
            new(center.X + halfWidth, top),
            new(center.X + halfWidth, top + thickness),
            new(center.X, tip),
            new(center.X - halfWidth, top + thickness),
        ];
    }

    private static Vector2[] Offset(Vector2[] points, Vector2 by) => [.. points.Select(p => p + by)];

    private static StyleBoxFlat Outline(Color color, float width, int radius)
    {
        var box = new StyleBoxFlat { DrawCenter = false, BorderColor = color, AntiAliasing = true };
        box.SetBorderWidthAll(Math.Max(1, (int)MathF.Round(width)));
        box.SetCornerRadiusAll(radius);
        return box;
    }
}
