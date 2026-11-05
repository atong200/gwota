namespace Gwota.Core.Presentation;

/// <summary>
/// Horizontal layout of the player's hand. Cards that fit sit side by side, centered. Otherwise they overlap evenly
/// (each card covers the left one) so the hand spans the full width. While the pointer is over an overlapping hand,
/// a focus window of <see cref="FocusCount"/> cards around the pointed card is laid out without overlap and the
/// cards outside it squeeze together on either side.
/// </summary>
public static class HandLayout
{
    /// <summary>Cards shown without overlap around the pointer.</summary>
    public const int FocusCount = 5;

    /// <summary>Least visible width of a squeezed card.</summary>
    public const float MinSliver = 8;

    /// <summary>
    /// The left edge of each card, in hand order, relative to the hand's left edge. <paramref name="pointerX"/> is the
    /// pointer's x relative to the same edge, or null when the pointer is not over the hand.
    /// </summary>
    public static float[] Lefts(int count, float cardWidth, float gap, float width, float? pointerX = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var lefts = new float[count];
        Fill(lefts, cardWidth, gap, width, pointerX);
        return lefts;
    }

    /// <summary>Writes the left edge of each of <c>lefts.Length</c> cards, as <see cref="Lefts"/> returns them.</summary>
    public static void Fill(Span<float> lefts, float cardWidth, float gap, float width, float? pointerX = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cardWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);

        var count = lefts.Length;
        if (count == 0)
        {
            return;
        }

        var spread = (count * cardWidth) + ((count - 1) * gap);
        if (count == 1 || spread <= width)
        {
            Spaced(lefts, (width - spread) / 2, cardWidth + gap);
            return;
        }

        var step = Math.Max(0, (width - cardWidth) / (count - 1));
        if (pointerX is not { } pointer || count <= FocusCount || step == 0
            || !TryFocused(lefts, cardWidth, gap, width, step, Math.Clamp(pointer, 0, width)))
        {
            Spaced(lefts, 0, step);
        }
    }

    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/> with exponential easing: fast at first, then
    /// slowing, never overshooting. Snaps once within half a pixel, or at once when <paramref name="timeConstant"/> is 0.
    /// </summary>
    public static float Ease(float current, float target, float delta, float timeConstant)
    {
        if (timeConstant <= 0)
        {
            return target;
        }

        var next = current + ((target - current) * (1 - MathF.Exp(-Math.Max(0, delta) / timeConstant)));
        return MathF.Abs(target - next) < 0.5f ? target : next;
    }

    private static void Spaced(Span<float> lefts, float start, float step)
    {
        for (var i = 0; i < lefts.Length; i++)
        {
            lefts[i] = start + (i * step);
        }
    }

    /// <summary>
    /// The pointed card is the one whose visible part of the resting layout is under the pointer; it keeps the pointer
    /// at the same fraction of its width. False, with <paramref name="lefts"/> untouched, when the width cannot hold the
    /// window and the squeezed cards.
    /// </summary>
    private static bool TryFocused(Span<float> lefts, float cardWidth, float gap, float width, float step, float pointer)
    {
        var count = lefts.Length;
        var pointed = Math.Min((int)(pointer / step), count - 1);
        var visibleWidth = pointed == count - 1 ? cardWidth : step;
        var fraction = Math.Clamp((pointer - (pointed * step)) / visibleWidth, 0, 1);

        var first = Math.Clamp(pointed - (FocusCount / 2), 0, count - FocusCount);
        var before = first;
        var after = count - first - FocusCount;
        var pitch = cardWidth + gap;
        var windowWidth = (FocusCount * cardWidth) + ((FocusCount - 1) * gap);

        var minLeft = before * MinSliver;
        var maxLeft = width - windowWidth - (after > 0 ? gap + cardWidth + ((after - 1) * MinSliver) : 0);
        if (minLeft > maxLeft)
        {
            return false;
        }

        var desired = pointer - (fraction * cardWidth) - ((pointed - first) * pitch);
        var windowLeft = before == 0 ? 0 : after == 0 ? maxLeft : Math.Clamp(desired, minLeft, maxLeft);

        for (var i = 0; i < before; i++)
        {
            lefts[i] = i * windowLeft / before;
        }

        for (var i = 0; i < FocusCount; i++)
        {
            lefts[first + i] = windowLeft + (i * pitch);
        }

        if (after > 0)
        {
            var afterStart = windowLeft + windowWidth + gap;
            var afterStep = after == 1 ? 0 : (width - cardWidth - afterStart) / (after - 1);
            for (var i = 0; i < after; i++)
            {
                lefts[first + FocusCount + i] = after == 1 ? width - cardWidth : afterStart + (i * afterStep);
            }
        }

        return true;
    }
}
