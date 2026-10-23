using Godot;

namespace Gwota.Game.Ui;

/// <summary>
/// A player's round crystals: red diamonds, with a dark empty slot left behind for each one lost. A lost crystal
/// flashes and fades out of its slot.
/// </summary>
public sealed partial class GemView : Control
{
    public const string Explanation =
        "Round crystals.\nLose a round and you lose one (a drawn round costs both players one).\nLose both and the match is over.";

    private static readonly Vector2 GemSize = new(18, 26);
    private const float Gap = 8;

    private int _max;
    private int _current;
    private int _losing = -1;
    private float _loss;
    private Tween? _tween;

    public GemView()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = Explanation;
    }

    /// <summary>Shows <paramref name="current"/> of <paramref name="max"/> crystals, animating any newly lost one.</summary>
    public void Show(int current, int max, float durationScale)
    {
        current = Math.Clamp(current, 0, max);
        var lost = _max == max && current < _current;
        _max = max;
        CustomMinimumSize = new Vector2((max * GemSize.X) + (Math.Max(0, max - 1) * Gap) + 4, GemSize.Y + 4);

        if (lost && durationScale > 0)
        {
            _tween?.Kill();
            _losing = current;
            _tween = CreateTween();
            _tween.TweenMethod(Callable.From<float>(SetLoss), 0f, 1f, 0.7 * durationScale);
            _tween.TweenCallback(Callable.From(() => _losing = -1));
        }
        else if (current != _current)
        {
            _tween?.Kill();
            _losing = -1;
        }

        _current = current;
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var i = 0; i < _max; i++)
        {
            var center = new Vector2(2 + (GemSize.X / 2) + (i * (GemSize.X + Gap)), 2 + (GemSize.Y / 2));
            if (i < _current)
            {
                DrawGem(center, 1f, 1f);
                continue;
            }

            DrawSlot(center);
            if (i == _losing)
            {
                DrawGem(center, 1f + (0.6f * _loss), 1f - _loss);
            }
        }
    }

    private void SetLoss(float value)
    {
        _loss = value;
        QueueRedraw();
    }

    private void DrawSlot(Vector2 center)
    {
        var points = Diamond(center, 1f);
        DrawColoredPolygon(points, Palette.GemSlot);
        DrawPolyline([.. points, points[0]], new Color(Palette.Gem, 0.25f), 1.2f, antialiased: true);
    }

    /// <summary>A faceted diamond: a lit upper half, a darker lower half and a bright rim.</summary>
    private void DrawGem(Vector2 center, float scale, float alpha)
    {
        var points = Diamond(center, scale);
        var (top, right, bottom, left) = (points[0], points[1], points[2], points[3]);
        var girdle = center + new Vector2(0, -GemSize.Y * 0.12f * scale);
        var leftGirdle = new Vector2(left.X, girdle.Y);
        var rightGirdle = new Vector2(right.X, girdle.Y);

        DrawColoredPolygon([top, rightGirdle, leftGirdle], new Color(Palette.Gem.Lightened(0.25f), alpha));
        DrawColoredPolygon([leftGirdle, rightGirdle, bottom], new Color(Palette.Gem.Darkened(0.3f), alpha));
        DrawColoredPolygon([top, center + new Vector2(-GemSize.X * 0.12f * scale, girdle.Y - center.Y), leftGirdle], new Color(1, 1, 1, 0.35f * alpha));
        DrawPolyline([top, rightGirdle, bottom, leftGirdle, top], new Color(Palette.Gem.Lightened(0.5f), alpha), 1.2f, antialiased: true);
    }

    private static Vector2[] Diamond(Vector2 center, float scale)
    {
        var half = GemSize * 0.5f * scale;
        var girdleY = center.Y - (GemSize.Y * 0.12f * scale);
        return
        [
            new Vector2(center.X, center.Y - half.Y),
            new Vector2(center.X + half.X, girdleY),
            new Vector2(center.X, center.Y + half.Y),
            new Vector2(center.X - half.X, girdleY),
        ];
    }
}
