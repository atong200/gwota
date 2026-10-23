using Gwota.Core.Ai;

namespace Gwota.Core.Session;

/// <summary>
/// Runs a match between local humans and <see cref="IPlayerController"/>s: controlled seats act through
/// <see cref="StepController"/>, others through <see cref="Submit"/>. The front-end decides when to step.
/// </summary>
public sealed class MatchSession
{
    private readonly Dictionary<Seat, IPlayerController> _controllers;
    private readonly List<GameEvent> _history = [];

    public MatchSession(MatchEngine engine, IReadOnlyDictionary<Seat, IPlayerController> controllers)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(controllers);

        Engine = engine;
        _controllers = new Dictionary<Seat, IPlayerController>(controllers);
        _history.AddRange(engine.InitialEvents);
    }

    /// <summary>Raised after every successful command with the events it produced.</summary>
    public event EventHandler<IReadOnlyList<GameEvent>>? EventsApplied;

    public MatchEngine Engine { get; }

    public MatchState State => Engine.State;

    /// <summary>Every event since the match was created, starting with the engine's initial events.</summary>
    public IReadOnlyList<GameEvent> History => _history;

    public bool IsFinished => Engine.IsFinished;

    /// <summary>True when the next command must come from a controller (call <see cref="StepController"/>).</summary>
    public bool IsControllerTurn => State.SeatToAct is { } seat && _controllers.ContainsKey(seat);

    /// <summary>The human seat expected to act now, or null if a controller acts or the match is over.</summary>
    public Seat? HumanToAct => State.SeatToAct is { } seat && !_controllers.ContainsKey(seat) ? seat : null;

    public bool IsControlled(Seat seat) => _controllers.ContainsKey(seat);

    /// <summary>Whether <paramref name="seat"/> may send a command now (during the draft both seats can).</summary>
    public bool CanAct(Seat seat) => State.Phase switch
    {
        MatchPhase.Draft => !State.Player(seat).HasFinishedDraft,
        MatchPhase.Playing => State.SeatToAct == seat,
        _ => false,
    };

    public ulong Checksum() => StateChecksum.Compute(State);

    /// <summary>Legal ways for <paramref name="seat"/> to play the given hand card (rows, decoy targets).</summary>
    public IReadOnlyList<PlayCardCommand> PlayOptions(Seat seat, int cardId) =>
        Engine.GetLegalCommands(seat).OfType<PlayCardCommand>().Where(c => c.CardId == cardId).ToList();

    public bool IsLegal(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Engine.GetLegalCommands(command.Seat).Contains(command);
    }

    /// <summary>Applies a command on behalf of a human seat. Controller-driven seats are rejected.</summary>
    public CommandResult Submit(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _controllers.ContainsKey(command.Seat)
            ? CommandResult.Fail($"Seat {command.Seat} is driven by a controller.")
            : Apply(command);
    }

    /// <summary>Lets the controller whose turn it is act once. Returns null when no controller is due.</summary>
    public CommandResult? StepController()
    {
        if (State.SeatToAct is not { } seat || !_controllers.TryGetValue(seat, out var controller))
        {
            return null;
        }

        var result = Apply(controller.Decide(Engine, seat));
        return result.IsSuccess
            ? result
            : throw new InvalidOperationException($"Controller for seat {seat} produced an illegal command: {result.Error}");
    }

    /// <summary>Steps controllers until a human must act or the match ends. Returns the number of commands applied.</summary>
    public int RunControllers(int maxCommands = 1000)
    {
        var applied = 0;
        while (StepController() is not null)
        {
            if (++applied >= maxCommands)
            {
                throw new InvalidOperationException($"Controllers did not yield after {maxCommands} commands.");
            }
        }

        return applied;
    }

    private CommandResult Apply(GameCommand command)
    {
        var result = Engine.Apply(command);
        if (result.IsSuccess)
        {
            _history.AddRange(result.Events);
            EventsApplied?.Invoke(this, result.Events);
        }

        return result;
    }
}
