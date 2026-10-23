namespace Gwota.Core.Match;

/// <summary>Outcome of <see cref="MatchEngine.Apply"/>. Game-rule violations are failures, not exceptions.</summary>
public sealed record CommandResult
{
    private CommandResult(bool isSuccess, string? error, IReadOnlyList<GameEvent> events)
    {
        IsSuccess = isSuccess;
        Error = error;
        Events = events;
    }

    public bool IsSuccess { get; }

    public string? Error { get; }

    public IReadOnlyList<GameEvent> Events { get; }

    public static CommandResult Ok(IReadOnlyList<GameEvent> events) => new(true, null, events);

    public static CommandResult Fail(string error) => new(false, error, []);
}
