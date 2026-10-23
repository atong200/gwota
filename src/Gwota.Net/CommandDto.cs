namespace Gwota.Net;

public enum CommandKind
{
    FinishDraft,
    PlayCard,
    UseLeader,
    ResolveChoice,
    Pass,
    Forfeit,
}

/// <summary>A <see cref="GameCommand"/> in wire form.</summary>
public sealed record CommandDto(
    CommandKind Kind,
    Seat Seat,
    int? CardId = null,
    RowType? Row = null,
    int? TargetId = null,
    IReadOnlyList<int>? CardIds = null)
{
    public static CommandDto From(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command switch
        {
            FinishDraftCommand draft => new(CommandKind.FinishDraft, draft.Seat, CardIds: [.. draft.RedrawnCardIds]),
            PlayCardCommand play => new(CommandKind.PlayCard, play.Seat, play.CardId, play.Row, play.TargetId),
            UseLeaderCommand leader => new(CommandKind.UseLeader, leader.Seat),
            ResolveChoiceCommand choice => new(CommandKind.ResolveChoice, choice.Seat, choice.CardId),
            PassCommand pass => new(CommandKind.Pass, pass.Seat),
            ForfeitCommand forfeit => new(CommandKind.Forfeit, forfeit.Seat),
            _ => throw new ArgumentException($"Unknown command {command.GetType().Name}.", nameof(command)),
        };
    }

    /// <summary>The command this describes, or null when a required field is missing or out of range.</summary>
    public GameCommand? ToCommand()
    {
        if (!Enum.IsDefined(Seat) || (Row is { } row && !Enum.IsDefined(row)))
        {
            return null;
        }

        return Kind switch
        {
            CommandKind.FinishDraft => new FinishDraftCommand(Seat, CardIds ?? []),
            CommandKind.PlayCard when CardId is { } card => new PlayCardCommand(Seat, card, Row, TargetId),
            CommandKind.UseLeader => new UseLeaderCommand(Seat),
            CommandKind.ResolveChoice when CardId is { } card => new ResolveChoiceCommand(Seat, card),
            CommandKind.Pass => new PassCommand(Seat),
            CommandKind.Forfeit => new ForfeitCommand(Seat),
            _ => null,
        };
    }
}