namespace Gwota.Core.Tests.Match;

/// <summary>
/// <see cref="LegalCommands.IsLegal"/> must agree with <see cref="LegalCommands.For"/> on every state reached by
/// seeded matches, for legal commands and for many near-miss illegal ones.
/// </summary>
public class LegalityPropertyTests
{
    private static readonly RowType?[] RowsOrNone = [null, RowType.Melee, RowType.Ranged, RowType.Siege];

    [Fact]
    public void IsLegal_matches_the_enumerated_legal_commands()
    {
        var checkedCommands = 0;
        for (ulong seed = 1; seed <= 9; seed++)
        {
            Replay.Fingerprint(seed, (engine, _) => checkedCommands += CheckState(engine.State));
        }

        checkedCommands.ShouldBeGreaterThan(10_000);
    }

    [Fact]
    public void Has_action_besides_pass_matches_the_enumerated_legal_commands()
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            Replay.Fingerprint(seed, (engine, _) =>
            {
                foreach (var seat in new[] { Seat.One, Seat.Two })
                {
                    var expected = engine.State.Phase == MatchPhase.Playing
                        && LegalCommands.For(engine.State, seat).Any(c => c is not PassCommand);
                    LegalCommands.HasActionBesidesPass(engine.State, seat).ShouldBe(expected);
                }
            });
        }
    }

    [Fact]
    public void Draft_selections_are_legal_in_any_order_but_not_with_duplicates_or_foreign_ids()
    {
        var state = MatchEngine.Create(TestDecks.Mixed(Faction.Realms), TestDecks.Mixed(Faction.Abyss), new SeededRandom(5)).State;
        var hand = state.Player(Seat.One).Hand.Select(c => c.Id).ToList();
        var deckCard = state.Player(Seat.One).Deck[0].Id;

        LegalCommands.IsLegal(state, new FinishDraftCommand(Seat.One, [hand[1], hand[0]])).ShouldBeTrue();
        LegalCommands.IsLegal(state, new FinishDraftCommand(Seat.One, [hand[0], hand[0]])).ShouldBeFalse();
        LegalCommands.IsLegal(state, new FinishDraftCommand(Seat.One, [deckCard])).ShouldBeFalse();
        LegalCommands.IsLegal(state, new FinishDraftCommand(Seat.One, [.. hand.Take(state.Player(Seat.One).RedrawLimit + 1)])).ShouldBeFalse();
        LegalCommands.IsLegal(state, new PassCommand(Seat.One)).ShouldBeFalse();
    }

    [Fact]
    public void Draft_commands_compare_as_sets()
    {
        var a = new FinishDraftCommand(Seat.One, [3, 1, 2]);
        var b = new FinishDraftCommand(Seat.One, [1, 2, 3]);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(new FinishDraftCommand(Seat.One, [1, 1, 2]));
        a.ShouldNotBe(new FinishDraftCommand(Seat.Two, [1, 2, 3]));
        new FinishDraftCommand(Seat.One, [1, 1, 2]).ShouldNotBe(new FinishDraftCommand(Seat.One, [1, 2, 2]));
    }

    private static int CheckState(MatchState state)
    {
        var count = 0;
        foreach (var seat in new[] { Seat.One, Seat.Two })
        {
            var legal = LegalCommands.For(state, seat).ToHashSet();
            foreach (var command in legal.Concat(Candidates(state, seat)))
            {
                LegalCommands.IsLegal(state, command).ShouldBe(legal.Contains(command), command.ToString());
                count++;
            }
        }

        return count;
    }

    /// <summary>Commands close to legal ones: every hand card on every row and target, leader targets, choices, drafts.</summary>
    private static IEnumerable<GameCommand> Candidates(MatchState state, Seat seat)
    {
        var handIds = state.Players.SelectMany(p => p.Hand).Select(c => c.Id).ToList();
        var boardIds = state.Players.SelectMany(p => p.Rows).SelectMany(r => r.Cards).Select(c => c.Id).Append(9999).ToList();
        var discardIds = state.Players.SelectMany(p => p.Discard).Select(c => c.Id).ToList();

        yield return new PassCommand(seat);
        yield return new UseLeaderCommand(seat);
        yield return new ForfeitCommand(seat);

        foreach (var id in boardIds)
        {
            yield return new UseLeaderCommand(seat, id);
        }

        foreach (var id in discardIds.Concat(handIds))
        {
            yield return new ResolveChoiceCommand(seat, id);
        }

        foreach (var cardId in handIds)
        {
            foreach (var row in RowsOrNone)
            {
                yield return new PlayCardCommand(seat, cardId, row);
                foreach (var target in boardIds)
                {
                    yield return new PlayCardCommand(seat, cardId, row, target);
                }
            }
        }

        var hand = state.Player(seat).Hand.Select(c => c.Id).ToList();
        yield return FinishDraftCommand.KeepHand(seat);
        yield return new FinishDraftCommand(seat, [9999]);
        yield return new FinishDraftCommand(seat, [.. hand.AsEnumerable().Reverse().Take(2)]);
        yield return new FinishDraftCommand(seat, [.. hand.Take(state.Player(seat).RedrawLimit + 1)]);
        if (hand.Count > 0)
        {
            yield return new FinishDraftCommand(seat, [hand[0], hand[0]]);
        }
    }
}
