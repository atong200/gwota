using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class PagerTests
{
    private static Pager<int> Of(int count, int pageSize = 8) => new(Enumerable.Range(1, count).ToList(), pageSize);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 3)]
    public void Page_count_rounds_up_with_at_least_one_page(int count, int pages)
    {
        var pager = Of(count);

        pager.PageCount.ShouldBe(pages);
        pager.HasPages.ShouldBe(pages > 1);
    }

    [Fact]
    public void Turning_pages_stops_at_both_ends()
    {
        var pager = Of(10);

        pager.Previous().ShouldBeFalse();
        pager.CurrentPage.ShouldBe([1, 2, 3, 4, 5, 6, 7, 8]);
        pager.Next().ShouldBeTrue();
        pager.CurrentPage.ShouldBe([9, 10]);
        pager.HasNext.ShouldBeFalse();
        pager.Next().ShouldBeFalse();
        pager.Previous().ShouldBeTrue();
        pager.Page.ShouldBe(0);
    }

    [Fact]
    public void An_empty_list_shows_one_empty_page() =>
        Of(0).CurrentPage.ShouldBeEmpty();

    [Fact]
    public void Show_first_turns_to_the_page_of_the_item()
    {
        var pager = Of(20);

        pager.ShowFirst(i => i == 17).ShouldBeTrue();
        pager.Page.ShouldBe(2);
        pager.ShowFirst(i => i == 99).ShouldBeFalse();
        pager.Page.ShouldBe(2);
    }

    [Fact]
    public void Page_size_must_be_positive() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Of(3, 0));
}
