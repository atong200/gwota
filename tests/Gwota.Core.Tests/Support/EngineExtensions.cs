namespace Gwota.Core.Tests.Support;

internal static class EngineExtensions
{
    /// <summary>Applies a command that the test expects to be legal and returns its events.</summary>
    public static IReadOnlyList<GameEvent> ApplyOk(this MatchEngine engine, GameCommand command)
    {
        var result = engine.Apply(command);
        result.IsSuccess.ShouldBeTrue(result.Error);
        return result.Events;
    }

    public static IReadOnlyList<GameEvent> Play(this MatchEngine engine, CardInstance card, RowType? row = null, int? targetId = null) =>
        engine.ApplyOk(new PlayCardCommand(card.Owner, card.Id, row ?? (card.Definition.IsUnit ? card.Definition.Rows[0] : null), targetId));

    public static IReadOnlyList<GameEvent> Pass(this MatchEngine engine, Seat seat) => engine.ApplyOk(new PassCommand(seat));

    public static IReadOnlyList<GameEvent> Choose(this MatchEngine engine, Seat seat, CardInstance card) =>
        engine.ApplyOk(new ResolveChoiceCommand(seat, card.Id));

    public static IReadOnlyList<GameEvent> UseLeader(this MatchEngine engine, Seat seat) => engine.ApplyOk(new UseLeaderCommand(seat));
}
