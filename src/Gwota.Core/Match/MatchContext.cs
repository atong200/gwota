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
        while (drawn.Count < count && player.DeckList.Count > 0 && !player.IsHandFull)
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

    /// <summary>Puts a unit into play on its placement side and triggers its on-play abilities.</summary>
    public void PlayUnit(Seat player, CardInstance card, RowType row)
    {
        var side = card.Definition.PlacementSide(player);
        Player(side).Row(row).CardList.Add(card);
        Emit(new CardPlayed(player, card, side, row));

        foreach (var ability in card.Definition.Abilities)
        {
            UnitAbilities.Find(ability)?.OnPlay(this, player, card, row);
        }
    }

    public CardInstance TakeFromHand(Seat seat, int cardId) => Take(Player(seat).HandList, cardId, "hand");

    public CardInstance TakeFromDeck(Seat seat, int cardId) => Take(Player(seat).DeckList, cardId, "deck");

    public CardInstance TakeFromDiscard(Seat seat, int cardId) => Take(Player(seat).DiscardList, cardId, "discard pile");

    /// <summary>Takes a card off <paramref name="seat"/>'s side of the board (its rows, not the horn slots).</summary>
    public CardInstance TakeFromBoard(Seat seat, int cardId)
    {
        foreach (var row in Player(seat).Rows)
        {
            var index = row.CardList.FindIndex(c => c.Id == cardId);
            if (index >= 0)
            {
                var card = row.CardList[index];
                row.CardList.RemoveAt(index);
                return card;
            }
        }

        throw new InvalidOperationException($"Card {cardId} is not on seat {seat}'s board.");
    }

    /// <summary>Puts a card into <paramref name="player"/>'s hand; a card from the other side (an enemy spy) changes owner.</summary>
    public CardInstance ReturnToHand(Seat player, CardInstance card)
    {
        var returned = card.Owner == player ? card : card with { Owner = player };
        Player(player).HandList.Add(returned);
        Emit(new CardReturnedToHand(player, returned));
        return returned;
    }

    /// <summary>Moves a card from <paramref name="seat"/>'s deck straight onto one of their rows, without on-play abilities.</summary>
    public void MoveDeckToRow(Seat seat, CardInstance card, RowType row) =>
        Player(seat).Row(row).CardList.Add(TakeFromDeck(seat, card.Id));

    /// <summary>Puts a weather card in play for both sides.</summary>
    public void AddWeather(Seat player, CardInstance weather)
    {
        State.WeatherList.Add(weather);
        Emit(new CardPlayed(player, weather, player, null));
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

    /// <summary>Destroys the strongest non-hero unit(s) on both sides of the battlefield.</summary>
    public void ScorchBattlefield() => ScorchStrongest(UnitsOnBoard(Seat.One).Concat(UnitsOnBoard(Seat.Two)));

    /// <summary>Destroys the strongest non-hero unit(s) among the candidates (all ties die).</summary>
    public void ScorchStrongest(IEnumerable<(BoardRow Row, CardInstance Card)> candidates)
    {
        var scored = candidates
            .Where(c => c.Card.Definition.IsNonHeroUnit)
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

    private static CardInstance Take(List<CardInstance> pile, int cardId, string pileName)
    {
        var index = pile.FindIndex(c => c.Id == cardId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Card {cardId} is not in the {pileName}.");
        }

        var card = pile[index];
        pile.RemoveAt(index);
        return card;
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
