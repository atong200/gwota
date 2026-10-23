using Gwota.Core.Data;

namespace Gwota.Core.DeckBuilding;

/// <summary>A card and how many copies of it a deck holds.</summary>
public sealed record DeckLine(CardDefinition Card, int Count)
{
    /// <summary>Where the deck list shows the card: its first row for units, <see cref="BookFilter.Special"/> otherwise.</summary>
    public BookFilter Group => DeckEditor.GroupOf(Card);
}

/// <summary>Headline numbers of a deck, shown by the deck builder and the deck list.</summary>
public sealed record DeckStats(int Units, int Specials, int Heroes, int TotalStrength)
{
    public int Total => Units + Specials;
}

/// <summary>
/// Edits one deck under <see cref="DeckRules"/>. Additions breaking a hard limit (faction, copies, specials) are refused;
/// a missing unit minimum is only listed in <see cref="Problems"/>, so incomplete decks can be saved.
/// </summary>
public sealed class DeckEditor
{
    public const int MaxNameLength = 32;

    private readonly GameData _data;
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    public DeckEditor(DeckDefinition deck, GameData data, DeckRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(data);

        _data = data;
        Rules = rules ?? DeckRules.Classic;
        Id = deck.Id;
        Name = deck.Name;
        Faction = deck.Faction;
        Leader = data.Leaders.TryGetValue(deck.LeaderId, out var leader) && leader.Faction == deck.Faction
            ? leader
            : DefaultLeader(data, deck.Faction);
        Leaders = LeadersFor(data, deck.Faction);

        foreach (var entry in deck.Cards.Where(e => e.Count > 0 && data.Cards.ContainsKey(e.CardId)))
        {
            if (!_counts.ContainsKey(entry.CardId))
            {
                _order.Add(entry.CardId);
            }

            _counts[entry.CardId] = _counts.GetValueOrDefault(entry.CardId) + entry.Count;
        }
    }

    public DeckRules Rules { get; }

    public string Id { get; }

    public string Name { get; private set; }

    public Faction Faction { get; }

    public LeaderDefinition Leader { get; private set; }

    /// <summary>Leaders the deck may use (its faction's), ordered by name.</summary>
    public IReadOnlyList<LeaderDefinition> Leaders { get; }

    public DeckDefinition Deck => new()
    {
        Id = Id,
        Name = Name,
        Faction = Faction,
        LeaderId = Leader.Id,
        Cards = _order.Select(id => new DeckEntry(id, _counts[id])).ToList(),
    };

    /// <summary>Cards in the deck grouped by row (melee, ranged, siege, specials); heroes then strongest first within a group.</summary>
    public IReadOnlyList<DeckLine> Lines => _order
        .Select(id => new DeckLine(_data.Cards[id], _counts[id]))
        .OrderBy(l => l.Group)
        .ThenByDescending(l => l.Card.IsHero)
        .ThenByDescending(l => l.Card.Strength)
        .ThenBy(l => l.Card.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public DeckStats Stats
    {
        get
        {
            int units = 0, specials = 0, heroes = 0, strength = 0;
            foreach (var line in Lines)
            {
                if (line.Card.IsUnit)
                {
                    units += line.Count;
                    strength += line.Card.Strength * line.Count;
                    heroes += line.Card.IsHero ? line.Count : 0;
                }
                else
                {
                    specials += line.Count;
                }
            }

            return new DeckStats(units, specials, heroes, strength);
        }
    }

    /// <summary>Every rule the deck currently breaks; empty when it can be played.</summary>
    public IReadOnlyList<string> Problems => DeckValidator.Validate(Deck, _data.Cards, _data.Leaders, Rules);

    public bool IsPlayable => Problems.Count == 0;

    public int CountOf(string cardId) => _counts.GetValueOrDefault(cardId);

    public int MaxCopies(CardDefinition card) => Rules.MaxCopiesOf(card);

    /// <summary>Copies of <paramref name="card"/> that can still be added (the book's "x3" counter).</summary>
    public int Remaining(CardDefinition card) => Math.Max(0, MaxCopies(card) - CountOf(card.Id));

    /// <summary>Why <paramref name="card"/> cannot be added right now, or null if it can.</summary>
    public string? AddRefusal(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.Faction != Faction.Neutral && card.Faction != Faction)
        {
            return $"{card.Name} belongs to {FactionInfo.Name(card.Faction)}.";
        }

        if (CountOf(card.Id) >= MaxCopies(card))
        {
            return card.IsHero ? $"Heroes are limited to {MaxCopies(card)} copy per deck." : $"At most {MaxCopies(card)} copies per card.";
        }

        return card.IsSpecial && Stats.Specials >= Rules.MaxSpecials ? $"At most {Rules.MaxSpecials} special cards per deck." : null;
    }

    /// <summary>Adds one copy of a known card if the rules allow it.</summary>
    public bool TryAdd(string cardId)
    {
        if (!_data.Cards.TryGetValue(cardId, out var card) || AddRefusal(card) is not null)
        {
            return false;
        }

        if (!_counts.ContainsKey(cardId))
        {
            _order.Add(cardId);
        }

        _counts[cardId] = CountOf(cardId) + 1;
        return true;
    }

    /// <summary>Removes one copy; returns false when the deck holds none.</summary>
    public bool Remove(string cardId)
    {
        if (!_counts.TryGetValue(cardId, out var count))
        {
            return false;
        }

        if (count > 1)
        {
            _counts[cardId] = count - 1;
        }
        else
        {
            _counts.Remove(cardId);
            _order.Remove(cardId);
        }

        return true;
    }

    /// <summary>Renames the deck; blank names are refused, long ones truncated (see <see cref="NormalizeName"/>).</summary>
    public bool Rename(string? name)
    {
        if (NormalizeName(name) is not { } normalized)
        {
            return false;
        }

        Name = normalized;
        return true;
    }

    public bool SetLeader(string leaderId)
    {
        if (Leaders.FirstOrDefault(l => l.Id == leaderId) is not { } leader)
        {
            return false;
        }

        Leader = leader;
        return true;
    }

    /// <summary>Trims and collapses whitespace, caps the length at <see cref="MaxNameLength"/>; null when nothing is left.</summary>
    public static string? NormalizeName(string? name)
    {
        var words = (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join(' ', words);
        return joined.Length == 0 ? null : joined[..Math.Min(joined.Length, MaxNameLength)].TrimEnd();
    }

    public static BookFilter GroupOf(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !card.IsUnit || card.Rows.Count == 0
            ? BookFilter.Special
            : card.Rows[0] switch
            {
                RowType.Melee => BookFilter.Melee,
                RowType.Ranged => BookFilter.Ranged,
                _ => BookFilter.Siege,
            };
    }

    public static IReadOnlyList<LeaderDefinition> LeadersFor(GameData data, Faction faction)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Leaders.Values.Where(l => l.Faction == faction).OrderBy(l => l.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>The leader a new deck starts with: the first of its faction by name.</summary>
    public static LeaderDefinition DefaultLeader(GameData data, Faction faction) =>
        LeadersFor(data, faction) is [var first, ..] ? first : throw new InvalidOperationException($"No leader belongs to {FactionInfo.Name(faction)}.");
}
