namespace Gwota.Core.Match;

/// <summary>A deterministic hash of everything that can differ between two copies of a match.</summary>
public static class StateChecksum
{
    public static ulong Compute(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var hash = new Fnv64();
        hash.Add((int)state.Phase);
        hash.Add(state.Round);
        hash.Add((int)state.ActiveSeat);
        hash.Add((int)state.RoundStarter);
        hash.Add(state.NextInstanceId);
        AddChoice(ref hash, state.PendingChoice);
        AddOutcome(ref hash, state.Outcome);
        AddCards(ref hash, state.Weather);
        hash.Add(state.RoundResults.Count);
        foreach (var result in state.RoundResults)
        {
            hash.Add(result.ScoreOne);
            hash.Add(result.ScoreTwo);
            hash.Add(result.Winner is { } winner ? (int)winner : -1);
        }

        foreach (var player in state.Players)
        {
            AddPlayer(ref hash, player);
        }

        return hash.Value;
    }

    private static void AddPlayer(ref Fnv64 hash, PlayerState player)
    {
        hash.Add(player.Gems);
        hash.Add(player.HasPassed);
        hash.Add(player.LeaderUsed);
        hash.Add(player.HasFinishedDraft);
        hash.Add(player.RedrawLimit);
        AddCards(ref hash, player.Deck);
        AddCards(ref hash, player.Hand);
        AddCards(ref hash, player.Discard);
        foreach (var row in player.Rows)
        {
            AddCards(ref hash, row.Cards);
            hash.Add(row.HornCard?.Id ?? -1);
            hash.Add(row.HasLeaderHorn);
        }
    }

    private static void AddChoice(ref Fnv64 hash, PendingChoice? choice)
    {
        hash.Add(choice is null ? -1 : (int)choice.Seat);
        if (choice is not null)
        {
            hash.Add((int)choice.Kind);
            hash.Add(choice.Options.Count);
            foreach (var option in choice.Options)
            {
                hash.Add(option);
            }
        }
    }

    private static void AddOutcome(ref Fnv64 hash, MatchOutcome? outcome)
    {
        hash.Add(outcome is null);
        if (outcome is not null)
        {
            hash.Add(outcome.Winner is { } winner ? (int)winner : -1);
            hash.Add(outcome.ForfeitedBy is { } quitter ? (int)quitter : -1);
        }
    }

    private static void AddCards(ref Fnv64 hash, IReadOnlyList<CardInstance> cards)
    {
        hash.Add(cards.Count);
        foreach (var card in cards)
        {
            hash.Add(card.Id);
            hash.Add(card.Definition.Id);
        }
    }
}