namespace Gwota.Core.Match;

/// <summary>
/// A player's intent as a value object. The engine accepts it only if it equals one of
/// <see cref="MatchEngine.GetLegalCommands"/>, so UI and AI share the same validation.
/// </summary>
public abstract record GameCommand(Seat Seat);

/// <summary>
/// Ends the draft phase for a seat, redrawing the given hand cards (none keeps the hand). Equality ignores the
/// order of <paramref name="RedrawnCardIds"/>, so any ordering of a legal selection is accepted.
/// </summary>
public sealed record FinishDraftCommand(Seat Seat, IReadOnlyList<int> RedrawnCardIds) : GameCommand(Seat)
{
    public static FinishDraftCommand KeepHand(Seat seat) => new(seat, []);

    public bool Equals(FinishDraftCommand? other) =>
        other is not null && Seat == other.Seat && SameIds(RedrawnCardIds, other.RedrawnCardIds);

    public override int GetHashCode()
    {
        // Order-independent, like Equals.
        var ids = 0;
        foreach (var id in RedrawnCardIds)
        {
            ids = unchecked(ids + HashCode.Combine(id));
        }

        return HashCode.Combine(Seat, RedrawnCardIds.Count, ids);
    }

    public override string ToString() => $"{nameof(FinishDraftCommand)} {{ Seat = {Seat}, RedrawnCardIds = [{string.Join(", ", RedrawnCardIds)}] }}";

    /// <summary>Multiset equality without allocating; selections hold at most a few ids.</summary>
    private static bool SameIds(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var id in left)
        {
            if (Occurrences(left, id) != Occurrences(right, id))
            {
                return false;
            }
        }

        return true;
    }

    private static int Occurrences(IReadOnlyList<int> ids, int id)
    {
        var count = 0;
        foreach (var candidate in ids)
        {
            if (candidate == id)
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// Play a card from hand. <paramref name="Row"/> is required for units (agile choice) and horns;
/// <paramref name="TargetId"/> is the unit swapped by a decoy.
/// </summary>
public sealed record PlayCardCommand(Seat Seat, int CardId, RowType? Row = null, int? TargetId = null) : GameCommand(Seat);

/// <summary>Use the leader ability once per match. <paramref name="TargetId"/> is the unit picked by a targeted ability.</summary>
public sealed record UseLeaderCommand(Seat Seat, int? TargetId = null) : GameCommand(Seat);

/// <summary>Answer the current <see cref="PendingChoice"/> by picking one of its options.</summary>
public sealed record ResolveChoiceCommand(Seat Seat, int CardId) : GameCommand(Seat);

public sealed record PassCommand(Seat Seat) : GameCommand(Seat);

/// <summary>
/// Concedes the match: the opponent wins at once. Accepted at any time before the match ends, whoever's turn it
/// is, so it is not part of <see cref="MatchEngine.GetLegalCommands"/> (an AI choosing among legal moves never forfeits).
/// </summary>
public sealed record ForfeitCommand(Seat Seat) : GameCommand(Seat);
