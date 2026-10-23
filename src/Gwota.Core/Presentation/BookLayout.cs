namespace Gwota.Core.Presentation;

/// <summary>
/// Layout of the deck builder's book: a fixed <see cref="BookLayout.Columns"/> x <see cref="BookLayout.Rows"/> grid,
/// centered horizontally and top-anchored. A slot is the card, its copy-stack outlines and the "xN" counter.
/// </summary>
public readonly record struct BookGrid(float CardWidth, float CardHeight, float StackOffset, float CounterHeight, float Left, float GapX, float GapY)
{
    /// <summary>Card plus the stack peeking out on the right.</summary>
    public float SlotWidth => CardWidth + (2 * StackOffset);

    /// <summary>Card plus the stack peeking out on top, plus the counter.</summary>
    public float SlotHeight => CardHeight + (2 * StackOffset) + CounterHeight;

    /// <summary>Top-left corner of slot <paramref name="index"/> (row-major) inside the page area.</summary>
    public (float X, float Y) SlotPosition(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var column = index % BookLayout.Columns;
        var row = index / BookLayout.Columns;
        return (Left + (column * (SlotWidth + GapX)), row * (SlotHeight + GapY));
    }
}

public static class BookLayout
{
    public const int Columns = 5;
    public const int Rows = 2;

    /// <summary>Card width over height (the hand card is 86 x 122).</summary>
    public const float CardAspect = 86f / 122f;

    /// <summary>The copy stack's offset per extra copy, relative to the card width.</summary>
    public const float StackRatio = 0.035f;

    private const float MinCardWidth = 40;

    /// <summary>
    /// The largest cards that fit <paramref name="width"/> x <paramref name="height"/> with the given gaps, keeping the
    /// card aspect; never smaller than a readable minimum (the page then overflows rather than showing specks).
    /// </summary>
    public static BookGrid Fit(float width, float height, float counterHeight, float gapX = 18, float gapY = 12)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(counterHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(gapX);
        ArgumentOutOfRangeException.ThrowIfNegative(gapY);

        var slotWidth = (width - (gapX * (Columns - 1))) / Columns;
        var byWidth = slotWidth / (1 + (2 * StackRatio));

        var slotHeight = ((height - (gapY * (Rows - 1))) / Rows) - counterHeight;
        var byHeight = slotHeight / ((1 / CardAspect) + (2 * StackRatio));

        // Rounding the height and the stack offset can overshoot by a pixel: shrink until it truly fits.
        var cardWidth = MathF.Floor(Math.Max(MinCardWidth, Math.Min(byWidth, byHeight)));
        var grid = Grid(cardWidth, counterHeight, gapX, gapY);
        while (cardWidth > MinCardWidth && (GridWidth(grid) > width || GridHeight(grid) > height))
        {
            grid = Grid(--cardWidth, counterHeight, gapX, gapY);
        }

        return grid with { Left = MathF.Floor(Math.Max(0, (width - GridWidth(grid)) / 2)) };
    }

    public static float GridWidth(BookGrid grid) => (Columns * grid.SlotWidth) + ((Columns - 1) * grid.GapX);

    public static float GridHeight(BookGrid grid) => (Rows * grid.SlotHeight) + ((Rows - 1) * grid.GapY);

    private static BookGrid Grid(float cardWidth, float counterHeight, float gapX, float gapY) =>
        new(cardWidth, MathF.Round(cardWidth / CardAspect), StackOffsetFor(cardWidth), counterHeight, 0, gapX, gapY);

    /// <summary>How far each extra copy peeks out behind the card.</summary>
    public static float StackOffsetFor(float cardWidth) => MathF.Round(Math.Max(3, cardWidth * StackRatio));
}
