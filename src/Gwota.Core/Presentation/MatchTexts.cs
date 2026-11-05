using Gwota.Core.Session;

namespace Gwota.Core.Presentation;

/// <summary>The phase line (display font, capitals) and an optional hint line (body font) of the match screen.</summary>
public readonly record struct MatchStatus(string Phase, string Hint);

/// <summary>What the end-of-match screen says; <see cref="RematchLabel"/> is null when no rematch can be asked for.</summary>
public sealed record MatchEndText(string Title, string Body, string? RematchLabel);

/// <summary>The discard pile viewer: its title, its cards (most recent first) and the text shown when it is empty.</summary>
public sealed record DiscardPileText(string Title, IReadOnlyList<CardInstance> Cards, string EmptyText);

/// <summary>The part of a remote link the end-of-match screen depends on.</summary>
public readonly record struct LinkState(string? CloseReason, bool OpponentLeft, RematchStatus Rematch)
{
    public static LinkState Of(IMatchLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return new LinkState(link.CloseReason, link.OpponentLeft, link.Rematch);
    }
}

/// <summary>Player-facing texts of the match screen, seen from the <c>local</c> seat.</summary>
public static class MatchTexts
{
    public const string StoppedTitle = "Match stopped";

    public const string DraftTitle = "Draft phase";

    public static MatchStatus Status(MatchSession session, Seat local, MatchSelection selection)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(selection);

        var state = session.State;
        if (state.Phase == MatchPhase.Finished)
        {
            return new("MATCH OVER", string.Empty);
        }

        if (state.Phase == MatchPhase.Draft)
        {
            return new("DRAFT PHASE", string.Empty);
        }

        if (state.SeatToAct is { } seat && session.IsControlled(seat))
        {
            return new($"{state.Player(seat).Name} is thinking...".ToUpperInvariant(), string.Empty);
        }

        if (state.SeatToAct is { } other && other != local)
        {
            return new($"ROUND {state.Round} - {state.Player(other).Name.ToUpperInvariant()}'S TURN", string.Empty);
        }

        var phase = $"ROUND {state.Round} - YOUR TURN";
        var targets = selection.Targets;
        if (selection.IsLeader)
        {
            return new(phase, targets.IsBoardWide
                ? "Click the leader again or anywhere on the board to use it.\nRight-click to cancel."
                : "Click a highlighted unit to return it to your hand.\nClick the leader again or right-click to cancel.");
        }

        if (selection.CardId is null)
        {
            return new(phase, string.Empty);
        }

        var action = targets.IsBoardWide ? "Click anywhere on the board to play it."
            : targets.Rows.Count > 0 ? "Click a highlighted row to play it."
            : "Click a highlighted card to target it.";
        return new(phase, $"{action}\nRight-click to cancel.");
    }

    /// <summary>The end-of-match screen; <paramref name="link"/> is null for a local match.</summary>
    public static MatchEndText End(MatchState state, Seat local, LinkState? link)
    {
        ArgumentNullException.ThrowIfNull(state);

        var winner = state.Outcome?.Winner;
        var title = winner is null ? "Draw" : winner == local ? "Victory" : "Defeat";
        var lines = state.RoundResults.Select(r => $"Round {r.Round}:  {r.ScoreOne} - {r.ScoreTwo}").ToList();
        if (state.Outcome?.ForfeitedBy is { } quitter)
        {
            lines.Insert(0, quitter == local ? "You forfeited the match." : $"{state.Player(quitter).Name} forfeited the match.");
        }

        if (link is not { } remote)
        {
            return EndText(title, lines, "Rematch");
        }

        if (remote.CloseReason is { } reason)
        {
            if (remote.OpponentLeft)
            {
                lines.Add(reason);
            }

            return EndText(title, lines, null);
        }

        var opponent = state.Player(local.Opponent()).Name;
        switch (remote.Rematch)
        {
            case RematchStatus.RequestedByYou:
                lines.Add($"Waiting for {opponent} to accept the rematch...");
                return EndText(title, lines, null);
            case RematchStatus.RequestedByOpponent:
                lines.Add($"{opponent} wants a rematch.");
                return EndText(title, lines, "Accept rematch");
            default:
                return EndText(title, lines, "Rematch");
        }
    }

    public static string ChoiceTitle(ChoiceKind kind) => kind == ChoiceKind.MedicRevive ? "Revive a unit" : "Return a card to your hand";

    public static string WaitingForDraft(string opponent) => $"Waiting for {opponent} to finish drafting...";

    public static string Unplayable(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return $"{card.Name} cannot be played right now.";
    }

    /// <summary>The banner shown when a round ends.</summary>
    public static string RoundBanner(MatchState state, RoundResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);
        return result.Winner is { } winner ? $"{state.Player(winner).Name} wins round {result.Round}" : "Round drawn";
    }

    /// <summary>The discard pile of <paramref name="owner"/> as the viewer shows it: most recent card first.</summary>
    public static DiscardPileText DiscardPile(MatchState state, Seat owner, Seat local)
    {
        ArgumentNullException.ThrowIfNull(state);

        var cards = state.Player(owner).Discard.Reverse().ToList();
        var whose = owner == local ? "Your" : $"{state.Player(owner).Name}'s";
        var empty = owner == local ? "No cards in your discard pile yet." : $"No cards in {state.Player(owner).Name}'s discard pile yet.";
        return new DiscardPileText($"{whose} discard pile ({cards.Count})", cards, empty);
    }

    private static MatchEndText EndText(string title, List<string> lines, string? rematchLabel) =>
        new(title, string.Join('\n', lines), rematchLabel);
}
