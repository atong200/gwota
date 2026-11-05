namespace Gwota.Core.Match;

/// <summary>
/// The rules engine. Pure C#, deterministic for a given seed, no engine/UI dependency.
/// Feed it <see cref="GameCommand"/>s; read <see cref="State"/> and the returned <see cref="GameEvent"/>s.
/// </summary>
public sealed class MatchEngine
{
    private readonly MatchContext _context;

    private MatchEngine(MatchContext context) => _context = context;

    public MatchState State => _context.State;

    public MatchRules Rules => _context.Rules;

    /// <summary>Events produced while setting the match up (initial draws, coin toss).</summary>
    public IReadOnlyList<GameEvent> InitialEvents { get; private init; } = [];

    public bool IsFinished => State.Phase == MatchPhase.Finished;

    public static MatchEngine Create(PlayerSetup one, PlayerSetup two, IRandom random, MatchRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(two);
        ArgumentNullException.ThrowIfNull(random);
        rules ??= MatchRules.Classic;

        var state = new MatchState(CreatePlayer(Seat.One, one, rules), CreatePlayer(Seat.Two, two, rules));
        var context = new MatchContext(state, rules, random);

        foreach (var (seat, setup) in new[] { (Seat.One, one), (Seat.Two, two) })
        {
            var deck = state.Player(seat).DeckList;
            var copies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var definition in setup.Deck)
            {
                var copy = copies.GetValueOrDefault(definition.Id);
                copies[definition.Id] = copy + 1;
                deck.Add(new CardInstance(state.NextInstanceId++, definition, seat) { Copy = copy });
            }
            random.Shuffle(deck);
        }

        context.Draw(Seat.One, rules.StartingHandSize);
        context.Draw(Seat.Two, rules.StartingHandSize);

        var starter = random.Next(2) == 0 ? Seat.One : Seat.Two;
        state.RoundStarter = starter;
        state.ActiveSeat = starter;
        context.Emit(new MatchStarted(starter));

        return new MatchEngine(context) { InitialEvents = context.TakeEvents() };
    }

    /// <summary>Wraps an arbitrary state; used by tests to set up precise scenarios.</summary>
    internal static MatchEngine FromState(MatchState state, IRandom random, MatchRules? rules = null) =>
        new(new MatchContext(state, rules ?? MatchRules.Classic, random));

    /// <summary>Deep copy, including the random generator. Used by the AI to simulate moves.</summary>
    public MatchEngine Clone() => new(_context.Clone());

    public int Score(Seat seat) => StrengthCalculator.PlayerTotal(State, seat);

    public int RowScore(Seat seat, RowType row) => StrengthCalculator.RowTotal(State, State.Player(seat).Row(row));

    public int Strength(BoardRow row, CardInstance card) => StrengthCalculator.Calculate(State, row, card);

    public IReadOnlyList<GameCommand> GetLegalCommands(Seat seat) => LegalCommands.For(State, seat);

    public bool IsLegal(GameCommand command) => LegalCommands.IsLegal(State, command);

    public CommandResult Apply(GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command is ForfeitCommand forfeit)
        {
            return Forfeit(forfeit.Seat);
        }

        return IsLegal(command)
            ? CommandResult.Ok(Execute(command))
            : CommandResult.Fail($"{command} is not legal in the current state.");
    }

    /// <summary>
    /// Applies a command already known to be legal (taken from <see cref="GetLegalCommands"/> for this exact state),
    /// skipping the legality check. Used by AI simulations.
    /// </summary>
    internal IReadOnlyList<GameEvent> ApplyLegal(GameCommand command) => Execute(command);

    private IReadOnlyList<GameEvent> Execute(GameCommand command)
    {
        switch (command)
        {
            case FinishDraftCommand draft:
                FinishDraft(draft);
                break;
            case PlayCardCommand play:
                PlayCard(play);
                break;
            case UseLeaderCommand leader:
                UseLeader(leader);
                break;
            case ResolveChoiceCommand resolve:
                ResolveChoice(resolve);
                break;
            case PassCommand pass:
                Pass(pass.Seat);
                break;
            default:
                throw new InvalidOperationException($"Unsupported command {command.GetType().Name}.");
        }

        return _context.TakeEvents();
    }

    private static PlayerState CreatePlayer(Seat seat, PlayerSetup setup, MatchRules rules) =>
        new(seat, setup.Name, setup.Faction, setup.Leader, rules.StartingGems)
        {
            RedrawLimit = rules.MaxRedraws,
            HandLimit = rules.MaxHandSize,
        };

    /// <summary>
    /// Replacements are all drawn before the returned cards are shuffled back, so a player never redraws a card
    /// they just sent away.
    /// </summary>
    private void FinishDraft(FinishDraftCommand command)
    {
        var seat = command.Seat;
        var player = State.Player(seat);
        var returned = new List<CardInstance>();
        var drawn = new List<CardInstance>();

        for (var index = 0; index < player.HandList.Count; index++)
        {
            if (!command.RedrawnCardIds.Contains(player.HandList[index].Id))
            {
                continue;
            }

            returned.Add(player.HandList[index]);
            drawn.Add(player.DeckList[0]);
            player.HandList[index] = player.DeckList[0];
            player.DeckList.RemoveAt(0);
        }

        foreach (var card in returned)
        {
            player.DeckList.Insert(_context.Random.Next(player.DeckList.Count + 1), card);
        }

        player.HasFinishedDraft = true;
        _context.Emit(new DraftFinished(seat, returned.Select(c => c.Id).ToList(), drawn.Select(c => c.Id).ToList()));

        if (State.Players.All(p => p.HasFinishedDraft))
        {
            State.Phase = MatchPhase.Playing;
            StartRound(State.RoundStarter);
        }
    }

    private void PlayCard(PlayCardCommand command)
    {
        var card = _context.TakeFromHand(command.Seat, command.CardId);
        var definition = card.Definition;
        if (definition.IsUnit)
        {
            _context.PlayUnit(command.Seat, card, SpecialEffects.RequireRow(command));
        }
        else
        {
            var effect = definition.Effect ?? throw new InvalidOperationException($"Special card '{definition.Id}' has no effect.");
            SpecialEffects.Get(effect).Resolve(_context, command, card);
        }

        EndAction(command.Seat);
    }

    private void UseLeader(UseLeaderCommand command)
    {
        var seat = command.Seat;
        var player = State.Player(seat);
        player.LeaderUsed = true;
        _context.Emit(new LeaderUsed(seat, player.Leader));
        LeaderAbilities.Get(player.Leader.Ability).Use(_context, command, player.Leader);
        EndAction(seat);
    }

    private void ResolveChoice(ResolveChoiceCommand command)
    {
        var choice = State.PendingChoice ?? throw new InvalidOperationException("No choice is pending.");
        State.PendingChoice = null;

        var card = _context.TakeFromDiscard(command.Seat, command.CardId);
        switch (choice.Kind)
        {
            case ChoiceKind.MedicRevive:
                _context.Emit(new CardRevived(command.Seat, card));
                _context.PlayUnit(command.Seat, card, card.Definition.Rows[0]);
                break;
            case ChoiceKind.ReturnFromDiscard:
                _context.ReturnToHand(command.Seat, card);
                break;
            default:
                throw new InvalidOperationException($"Unknown choice kind {choice.Kind}.");
        }

        EndAction(command.Seat);
    }

    private void Pass(Seat seat)
    {
        State.Player(seat).HasPassed = true;
        _context.Emit(new PlayerPassed(seat, Automatic: false));
        AdvanceTurn(seat);
    }

    /// <summary>Ends the actor's action unless a choice is still pending.</summary>
    private void EndAction(Seat actor)
    {
        if (State.PendingChoice is null)
        {
            AdvanceTurn(actor);
        }
    }

    private void AdvanceTurn(Seat actor)
    {
        var other = actor.Opponent();
        if (!State.Player(other).HasPassed)
        {
            BeginTurn(other);
        }
        else if (!State.Player(actor).HasPassed)
        {
            BeginTurn(actor);
        }
        else if (RoundResolver.EndRound(_context) is { } nextStarter)
        {
            StartRound(nextStarter);
        }
    }

    private void BeginTurn(Seat seat)
    {
        State.ActiveSeat = seat;
        _context.Emit(new TurnStarted(seat));

        if (!LegalCommands.HasActionBesidesPass(State, seat))
        {
            State.Player(seat).HasPassed = true;
            _context.Emit(new PlayerPassed(seat, Automatic: true));
            AdvanceTurn(seat);
        }
    }

    private void StartRound(Seat starter)
    {
        State.RoundStarter = starter;
        foreach (var player in State.Players)
        {
            player.HasPassed = false;
        }

        _context.Emit(new RoundStarted(State.Round, starter));
        BeginTurn(starter);
    }

    private CommandResult Forfeit(Seat seat)
    {
        if (IsFinished)
        {
            return CommandResult.Fail("The match is already over.");
        }

        State.PendingChoice = null;
        _context.Emit(new PlayerForfeited(seat));
        State.Outcome = new MatchOutcome(seat.Opponent(), ForfeitedBy: seat);
        State.Phase = MatchPhase.Finished;
        _context.Emit(new MatchEnded(State.Outcome));
        return CommandResult.Ok(_context.TakeEvents());
    }
}
