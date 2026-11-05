using Godot;
using Gwota.Core.Data;
using Gwota.Game.Infrastructure;

namespace Gwota.Game.Ui;

/// <summary>
/// The back of a card: the image of its back id (see <see cref="CardArt.CardBack"/>), otherwise the default back drawn
/// in the deck's faction colours under the shared pewter frame.
/// Used for the draw piles, the opponent's hand, card flights and the card back picker.
/// </summary>
public sealed partial class CardBack : PanelContainer
{
    // Proportions of the default back, as fractions of the card width (the frame art is 768 px wide).
    private const float InnerBorderInset = 52f / 768;
    private const float InnerBorderWidth = 3f / 768;
    private const float DiagonalWidth = 4f / 768;
    private const float DiagonalHalfSide = 0.25f;
    private static readonly float DiagonalTilt = Mathf.DegToRad(30);

    private readonly Color _fill;
    private readonly Color _accent;

    public CardBack(Vector2 size, CardBackStyle style)
    {
        CustomMinimumSize = size;
        Size = size;
        MouseFilter = MouseFilterEnum.Ignore;

        var art = CardArt.CardBack(style.Id);
        if (art is not null)
        {
            AddThemeStyleboxOverride("panel", Widgets.Box(new Color(0, 0, 0, 0.6f), new Color(0, 0, 0, 0.7f), 1, radius: 2, margin: 1));
            AddChild(Picture(art, TextureRect.StretchModeEnum.KeepAspectCovered));
            return;
        }

        _fill = Palette.ForFaction(style.Faction);
        _accent = _fill.Darkened(0.4f);
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var emblem = Widgets.Label("G", Math.Max(10, (int)(size.X * 0.4f)), new Color(_fill.Lightened(0.55f), 0.85f), HorizontalAlignment.Center);
        emblem.VerticalAlignment = VerticalAlignment.Center;
        AddChild(emblem);
        if (CardArt.CardBackFrame() is { } frame)
        {
            AddChild(Picture(frame, TextureRect.StretchModeEnum.Scale));
        }
    }

    public override void _Draw()
    {
        if (_fill.A == 0)
        {
            return;
        }

        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), _fill.Darkened(0.15f));
        var inset = size.X * InnerBorderInset;
        var inner = new Rect2(new Vector2(inset, inset), size - new Vector2(2 * inset, 2 * inset));
        DrawRect(inner, _fill);

        var width = Math.Max(1f, size.X * DiagonalWidth);
        foreach (var (from, to) in Diagonals(size / 2, size.X * DiagonalHalfSide, inner))
        {
            DrawLine(from, to, _accent, width, true);
        }

        DrawRect(inner, _accent, false, Math.Max(1f, size.X * InnerBorderWidth));
    }

    /// <summary>The four sides of a square tilted around <paramref name="center"/>, extended to the edges of <paramref name="bounds"/>.</summary>
    internal static IEnumerable<(Vector2 From, Vector2 To)> Diagonals(Vector2 center, float halfSide, Rect2 bounds)
    {
        for (var side = 0; side < 4; side++)
        {
            var normal = Vector2.FromAngle(DiagonalTilt + (side * Mathf.Pi / 2));
            var point = center + (normal * halfSide);
            var direction = normal.Orthogonal();
            if (Clip(point, direction, bounds) is { } segment)
            {
                yield return segment;
            }
        }
    }

    // Liang-Barsky clipping of the infinite line point + t * direction to the rectangle.
    private static (Vector2, Vector2)? Clip(Vector2 point, Vector2 direction, Rect2 bounds)
    {
        var (low, high) = (float.NegativeInfinity, float.PositiveInfinity);
        var limits = new[]
        {
            (-direction.X, point.X - bounds.Position.X), (direction.X, bounds.End.X - point.X),
            (-direction.Y, point.Y - bounds.Position.Y), (direction.Y, bounds.End.Y - point.Y),
        };
        foreach (var (p, q) in limits)
        {
            if (Mathf.IsZeroApprox(p))
            {
                if (q < 0)
                {
                    return null;
                }

                continue;
            }

            var t = q / p;
            if (p < 0)
            {
                low = Math.Max(low, t);
            }
            else
            {
                high = Math.Min(high, t);
            }
        }

        return low < high ? (point + (direction * low), point + (direction * high)) : null;
    }

    private static TextureRect Picture(Texture2D texture, TextureRect.StretchModeEnum stretch) => new()
    {
        Texture = texture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = stretch,
        MouseFilter = MouseFilterEnum.Ignore,
    };
}
