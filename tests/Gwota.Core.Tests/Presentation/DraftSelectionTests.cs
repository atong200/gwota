using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class DraftSelectionTests
{
    private static MatchEngine NewMatch(int deckSize = 25, MatchRules? rules = null)
    {
        PlayerSetup Setup(string name) => new(name, Faction.Neutral, TestCards.Leader(LeaderAbility.ClearWeather),
            Enumerable.Range(1, deckSize).Select(i => TestCards.Unit(i % 10, id: $"card_{i}")).ToList());
        return MatchEngine.Create(Setup("One"), Setup("Two"), new SeededRandom(1), rules);
    }

    private static (DraftSelection Selection, IReadOnlyList<CardInstance> Hand) NewSelection(int deckSize = 25, MatchRules? rules = null)
    {
        var player = NewMatch(deckSize, rules).State.Player(Seat.One);
        return (new DraftSelection(player), player.Hand);
    }

    [Fact]
    public void Starts_empty_and_keeps_the_hand()
    {
        var (selection, _) = NewSelection();

        selection.Selected.ShouldBeEmpty();
        selection.Limit.ShouldBe(2);
        selection.ConfirmText.ShouldBe("Keep hand");
        selection.ToCommand().ShouldBe(FinishDraftCommand.KeepHand(Seat.One));
    }

    [Fact]
    public void Clicking_a_card_selects_it_and_clicking_again_deselects_it()
    {
        var (selection, hand) = NewSelection();

        selection.Toggle(hand[0].Id).ShouldBeTrue();
        selection.IsSelected(hand[0].Id).ShouldBeTrue();
        selection.ConfirmText.ShouldBe("Redraw");

        selection.Toggle(hand[0].Id).ShouldBeTrue();
        selection.IsSelected(hand[0].Id).ShouldBeFalse();
        selection.ConfirmText.ShouldBe("Keep hand");
    }

    [Fact]
    public void At_most_the_limit_can_be_selected()
    {
        var (selection, hand) = NewSelection();

        selection.Toggle(hand[0].Id);
        selection.Toggle(hand[1].Id);

        selection.IsFull.ShouldBeTrue();
        selection.Toggle(hand[2].Id).ShouldBeFalse();
        selection.Selected.ShouldBe([hand[0].Id, hand[1].Id]);
        selection.ConfirmText.ShouldBe("Redraw");
    }

    [Fact]
    public void Deselecting_frees_a_slot()
    {
        var (selection, hand) = NewSelection();
        selection.Toggle(hand[0].Id);
        selection.Toggle(hand[1].Id);

        selection.Toggle(hand[0].Id);

        selection.Toggle(hand[2].Id).ShouldBeTrue();
        selection.Selected.ShouldBe([hand[1].Id, hand[2].Id]);
    }

    [Fact]
    public void The_command_is_accepted_by_the_engine()
    {
        var engine = NewMatch();
        var player = engine.State.Player(Seat.One);
        var selection = new DraftSelection(player);
        var (first, second) = (player.Hand[4], player.Hand[1]);
        selection.Toggle(first.Id);
        selection.Toggle(second.Id);

        engine.ApplyOk(selection.ToCommand());

        player.Hand.ShouldNotContain(first);
        player.Hand.ShouldNotContain(second);
    }

    [Fact]
    public void The_limit_follows_the_rules_and_the_deck_size()
    {
        NewSelection(rules: MatchRules.Classic with { MaxRedraws = 1 }).Selection.Limit.ShouldBe(1);
        NewSelection(rules: MatchRules.Classic with { MaxRedraws = 1 }).Selection.Instructions.ShouldBe("Select up to 1 card to redraw. Click again to deselect.");
        NewSelection(deckSize: 10).Selection.Limit.ShouldBe(0);
        NewSelection(deckSize: 10).Selection.Instructions.ShouldBe("No card can be redrawn.");
    }

    [Fact]
    public void Instructions_do_not_repeat_the_selection_count()
    {
        var (selection, hand) = NewSelection();
        selection.Toggle(hand[0].Id);

        selection.Instructions.ShouldBe("Select up to 2 cards to redraw. Click again to deselect.");
    }
}
