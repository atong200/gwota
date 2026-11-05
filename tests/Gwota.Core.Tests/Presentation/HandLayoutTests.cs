using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class HandLayoutTests
{
    private const float Card = 92;
    private const float Gap = 4;
    private const float Width = 980;
    private const float Pitch = Card + Gap;

    private static float[] Lefts(int count, float? pointer = null, float width = Width) =>
        HandLayout.Lefts(count, Card, Gap, width, pointer);

    private static float RestStep(int count) => (Width - Card) / (count - 1);

    /// <summary>The pointer x in the middle of card <paramref name="index"/>'s visible part of the resting layout.</summary>
    private static float PointerOver(int count, int index) =>
        (index * RestStep(count)) + ((index == count - 1 ? Card : RestStep(count)) / 2);

    private static void ShouldHaveFocusWindow(float[] lefts, int first)
    {
        for (var i = 1; i < HandLayout.FocusCount; i++)
        {
            (lefts[first + i] - lefts[first + i - 1]).ShouldBe(Pitch, 0.01f);
        }
    }

    [Fact]
    public void An_empty_hand_has_no_cards() => Lefts(0).ShouldBeEmpty();

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(10)]
    public void Cards_that_fit_are_centered_side_by_side_and_ignore_the_pointer(int count)
    {
        var lefts = Lefts(count, pointer: 30);

        var spread = (count * Card) + ((count - 1) * Gap);
        lefts[0].ShouldBe((Width - spread) / 2, 0.01f);
        for (var i = 1; i < count; i++)
        {
            (lefts[i] - lefts[i - 1]).ShouldBe(Pitch, 0.01f);
        }

        lefts.ShouldBe(Lefts(count));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(20)]
    public void Cards_that_do_not_fit_overlap_evenly_over_the_full_width(int count)
    {
        var lefts = Lefts(count);

        lefts[0].ShouldBe(0);
        (lefts[^1] + Card).ShouldBe(Width, 0.01f);
        for (var i = 1; i < count; i++)
        {
            (lefts[i] - lefts[i - 1]).ShouldBe(RestStep(count), 0.01f);
        }
    }

    [Theory]
    [InlineData(12, 6)]
    [InlineData(16, 8)]
    [InlineData(20, 10)]
    public void Pointing_a_middle_card_shows_it_third_in_a_window_under_the_pointer(int count, int pointed)
    {
        var pointer = PointerOver(count, pointed);

        var lefts = Lefts(count, pointer);

        ShouldHaveFocusWindow(lefts, pointed - 2);
        pointer.ShouldBeInRange(lefts[pointed], lefts[pointed] + Card);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Pointing_near_the_left_edge_pins_the_window_to_the_first_cards(int pointed)
    {
        var lefts = Lefts(16, PointerOver(16, pointed));

        lefts[0].ShouldBe(0);
        ShouldHaveFocusWindow(lefts, 0);
        (lefts[5] - lefts[4]).ShouldBe(Pitch, 0.01f);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(14)]
    [InlineData(13)]
    public void Pointing_near_the_right_edge_pins_the_window_to_the_last_cards(int pointed)
    {
        var lefts = Lefts(16, PointerOver(16, pointed));

        (lefts[^1] + Card).ShouldBe(Width, 0.01f);
        ShouldHaveFocusWindow(lefts, 11);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(20)]
    public void Focused_layout_stays_ordered_within_the_hand_and_keeps_every_card_visible(int count)
    {
        for (float pointer = 0; pointer <= Width; pointer += 7)
        {
            var lefts = Lefts(count, pointer);

            lefts[0].ShouldBeGreaterThanOrEqualTo(0);
            (lefts[^1] + Card).ShouldBeLessThanOrEqualTo(Width + 0.01f);
            for (var i = 1; i < count; i++)
            {
                (lefts[i] - lefts[i - 1]).ShouldBeGreaterThanOrEqualTo(HandLayout.MinSliver - 0.01f);
            }
        }
    }

    [Fact]
    public void Pointer_outside_the_hand_is_clamped_to_its_edges()
    {
        Lefts(16, -50).ShouldBe(Lefts(16, 0));
        Lefts(16, Width + 50).ShouldBe(Lefts(16, Width));
    }

    [Fact]
    public void A_hand_too_narrow_for_the_window_keeps_the_resting_layout()
    {
        const float narrow = 500;

        Lefts(12, 250, narrow).ShouldBe(Lefts(12, width: narrow));
    }

    [Fact]
    public void A_short_overlapping_hand_has_no_focus_window()
    {
        const float narrow = 300;

        Lefts(5, 150, narrow).ShouldBe(Lefts(5, width: narrow));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(50f)]
    public void A_hand_narrower_than_a_card_stacks_every_card_at_the_left(float width) =>
        Lefts(12, 10, width).ShouldAllBe(left => left == 0);

    [Theory]
    [InlineData(0, null, Width)]
    [InlineData(4, 30f, Width)]
    [InlineData(15, null, Width)]
    [InlineData(15, 0f, Width)]
    [InlineData(15, 500f, Width)]
    [InlineData(15, Width, Width)]
    [InlineData(20, 200f, 400f)]
    public void Fill_writes_the_same_lefts_as_Lefts_over_stale_values(int count, float? pointerX, float width)
    {
        var buffer = Enumerable.Repeat(-123f, count).ToArray();

        HandLayout.Fill(buffer, Card, Gap, width, pointerX);

        buffer.ShouldBe(Lefts(count, pointerX, width));
    }

    [Fact]
    public void Without_room_for_the_focus_window_the_hand_stays_evenly_overlapped()
    {
        const float narrow = 400;
        var count = 20;

        Lefts(count, 200, narrow).ShouldBe(Lefts(count, width: narrow));
    }

    [Theory]
    [InlineData(0f, 4f)]
    [InlineData(92f, -1f)]
    public void Fill_rejects_an_empty_card_or_a_negative_gap(float cardWidth, float gap) =>
        Should.Throw<ArgumentOutOfRangeException>(() => HandLayout.Fill(new float[3], cardWidth, gap, Width));

    [Fact]
    public void Ease_moves_part_of_the_way_without_overshooting_and_converges()
    {
        var x = 0f;
        var previous = 0f;
        for (var frame = 0; frame < 60; frame++)
        {
            x = HandLayout.Ease(x, 100, 1 / 60f, 0.05f);
            x.ShouldBeGreaterThanOrEqualTo(previous);
            x.ShouldBeLessThanOrEqualTo(100);
            previous = x;
        }

        HandLayout.Ease(0, 100, 1 / 60f, 0.05f).ShouldBeInRange(1, 99);
        x.ShouldBe(100);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Ease_without_a_time_constant_jumps_to_the_target(float timeConstant) =>
        HandLayout.Ease(10, 90, 1 / 60f, timeConstant).ShouldBe(90);
}
