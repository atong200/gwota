using Gwota.Core.DeckBuilding;
using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class BookLayoutTests
{
    private const float Counter = 28;

    [Theory]
    [InlineData(1100, 680)]
    [InlineData(1400, 860)]
    [InlineData(900, 900)]
    [InlineData(1600, 400)]
    public void The_grid_fits_the_page_and_keeps_the_card_aspect(float width, float height)
    {
        var grid = BookLayout.Fit(width, height, Counter);

        BookLayout.GridWidth(grid).ShouldBeLessThanOrEqualTo(width);
        BookLayout.GridHeight(grid).ShouldBeLessThanOrEqualTo(height);
        (grid.CardWidth / grid.CardHeight).ShouldBe(BookLayout.CardAspect, 0.01f);
    }

    [Fact]
    public void The_limiting_side_is_nearly_filled()
    {
        var wide = BookLayout.Fit(2000, 700, Counter);
        var tall = BookLayout.Fit(1000, 2000, Counter);

        BookLayout.GridHeight(wide).ShouldBeGreaterThan(700 - 12);
        BookLayout.GridWidth(tall).ShouldBeGreaterThan(1000 - 12);
    }

    [Fact]
    public void A_wide_page_centers_the_grid_horizontally()
    {
        var grid = BookLayout.Fit(2000, 700, Counter);

        grid.Left.ShouldBeGreaterThan(0);
        (grid.Left + BookLayout.GridWidth(grid)).ShouldBe(2000 - grid.Left, 1f);
    }

    [Fact]
    public void Slots_fill_rows_from_the_top_left()
    {
        var grid = BookLayout.Fit(1100, 680, Counter);

        grid.SlotPosition(0).ShouldBe((grid.Left, 0f));
        grid.SlotPosition(4).Y.ShouldBe(0f);
        grid.SlotPosition(5).ShouldBe((grid.Left, grid.SlotHeight + grid.GapY));
        grid.SlotPosition(1).X.ShouldBe(grid.Left + grid.SlotWidth + grid.GapX);
    }

    [Fact]
    public void A_tiny_page_keeps_a_readable_card() =>
        BookLayout.Fit(50, 50, Counter).CardWidth.ShouldBe(40);

    [Fact]
    public void The_stack_offset_grows_with_the_card_but_stays_visible()
    {
        BookLayout.StackOffsetFor(40).ShouldBe(3);
        BookLayout.StackOffsetFor(200).ShouldBe(7);
    }

    [Fact]
    public void A_page_of_the_book_is_exactly_one_grid() =>
        (BookLayout.Columns * BookLayout.Rows).ShouldBe(CardBook.DefaultPageSize);
}