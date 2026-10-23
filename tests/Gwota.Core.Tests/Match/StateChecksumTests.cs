using Gwota.Core.Ai;
using Gwota.Core.Session;

namespace Gwota.Core.Tests.Match;

public class StateChecksumTests
{
    private static MatchSession NewSession(ulong seed = 7) =>
        new(MatchRunner.NewMatch(TestDecks.Mixed(Faction.Realms), TestDecks.Mixed(Faction.Hells), seed), new Dictionary<Seat, IPlayerController>());

    [Fact]
    public void Two_matches_with_the_same_seed_have_the_same_checksum() =>
        NewSession().Checksum().ShouldBe(NewSession().Checksum());

    [Fact]
    public void Different_seeds_give_different_checksums() =>
        NewSession(1).Checksum().ShouldNotBe(NewSession(2).Checksum());

    [Fact]
    public void A_command_changes_the_checksum()
    {
        var session = NewSession();
        var before = session.Checksum();

        session.Submit(FinishDraftCommand.KeepHand(Seat.One));

        session.Checksum().ShouldNotBe(before);
    }

    [Fact]
    public void Same_commands_on_two_copies_keep_checksums_equal()
    {
        var one = NewSession();
        var two = NewSession();
        var ai = new HeuristicAi();

        while (!one.IsFinished)
        {
            var seat = one.State.SeatToAct!.Value;
            var command = ai.Decide(one.Engine, seat);
            one.Submit(command).IsSuccess.ShouldBeTrue();
            two.Submit(command).IsSuccess.ShouldBeTrue();
            two.Checksum().ShouldBe(one.Checksum());
        }
    }

    [Fact]
    public void Deck_order_is_part_of_the_checksum()
    {
        var session = NewSession();
        var before = session.Checksum();
        var deck = session.State.Player(Seat.One).DeckList;
        (deck[0], deck[1]) = (deck[1], deck[0]);

        session.Checksum().ShouldNotBe(before);
    }
}