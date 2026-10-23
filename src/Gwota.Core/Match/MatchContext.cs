namespace Gwota.Core.Match;

/// <summary>
/// Mutable working context shared by the engine and ability handlers: state, rules, randomness,
/// the event buffer and the low-level board operations every effect is built from.
/// </summary>
internal sealed class MatchContext(MatchState state, MatchRules rules, IRandom random)
{
    private readonly List<GameEvent> _events = [];

    public MatchState State { get; } = state;

    public MatchRules Rules { get; } = rules;

    public IRandom Random { get; } = random;

    public PlayerState Player(Seat seat) => State.Player(seat);

    public void Emit(GameEvent gameEvent) => _events.Add(gameEvent);

    public IReadOnlyList<GameEvent> TakeEvents()
    {
        var events = _events.ToArray();
        _events.Clear();
        return events;
    }

    public MatchContext Clone() => new(State.Clone(), Rules, Random.Clone());

    public IReadOnlyList<CardInstance> Draw(Seat seat, int count)
    {
        var player = Player(seat);
        var drawn = new List<CardInstance>(count);
        while (drawn.Count < count && player.DeckList.Count > 0)
        {
            var card = player.DeckList[0];
            player.DeckList.RemoveAt(0);
            player.HandList.Add(card);
            drawn.Add(card);
        }

        if (drawn.Count > 0)
        {
            Emit(new CardsDrawn(seat, drawn.Select(c => c.Id).ToArray()));
        }

        return drawn;
    }

    /// <summary>Puts a unit into play (spies go to the opponent's side) and triggers its on-play abilities.</summary>
    public void PlayUnit(Seat player, CardInstance card, RowType row)
    {
        var side = card.Definition.Has(UnitAbility.Spy) ? player.Opponent() : player;
        Player(side).Row(row).CardList.Add(card);
        Emit(new CardPlayed(player, card, side, row));

        foreach (var ability in card.Definition.Abilities)
        {
            UnitAbilities.Find(ability)?.OnPlay(this, player, card, row);
        }
    }

    public void Discard(CardInstance card) => Player(card.Owner).DiscardList.Add(card);

    public void Destroy(IReadOnlyList<CardInstance> cards)
    {
        if (cards.Count == 0)
        {
            return;
        }

        foreach (var card in cards)
        {
            RemoveFromBoard(card);
            Discard(card);
        }

        Emit(new CardsDestroyed(cards));
    }

    public void ClearWeather()
    {
        if (State.WeatherList.Count == 0)
        {
            return;
        }

        foreach (var weather in State.WeatherList)
        {
            Discard(weather);
        }

        State.WeatherList.Clear();
        Emit(new WeatherCleared());
    }

    public IEnumerable<(BoardRow Row, CardInstance Card)> UnitsOnBoard(Seat side) =>
        Player(side).Rows.SelectMany(row => row.Cards.Select(card => (row, card)));

    /// <summary>Destroys the strongest non-hero unit(s) among the candidates (all ties die).</summary>
    public void ScorchStrongest(IEnumerable<(BoardRow Row, CardInstance Card)> candidates)
    {
        var scored = candidates
            .Where(c => c.Card.Definition.IsUnit && !c.Card.Definition.IsHero)
            .Select(c => (c.Card, Strength: StrengthCalculator.Calculate(State, c.Row, c.Card)))
            .ToList();

        if (scored.Count == 0)
        {
            return;
        }

        var max = scored.Max(c => c.Strength);
        if (max <= 0)
        {
            return;
        }

        Destroy(scored.Where(c => c.Strength == max).Select(c => c.Card).ToList());
    }

    /// <summary>Scorches a row if its total strength reaches the configured threshold.</summary>
    public void ScorchRow(Seat side, RowType rowType)
    {
        var row = Player(side).Row(rowType);
        if (StrengthCalculator.RowTotal(State, row) < Rules.RowScorchThreshold)
        {
            return;
        }

        ScorchStrongest(row.Cards.Select(card => (row, card)));
    }

    public void RequestChoice(Seat seat, ChoiceKind kind, IReadOnlyList<int> options)
    {
        var choice = new PendingChoice(seat, kind, options);
        State.PendingChoice = choice;
        Emit(new ChoiceRequested(choice));
    }

    private void RemoveFromBoard(CardInstance card)
    {
        foreach (var player in State.Players)
        {
            foreach (var row in player.Rows)
            {
                if (row.CardList.RemoveAll(c => c.Id == card.Id) > 0)
                {
                    return;
                }
            }
        }
    }
}
