using System.Text.Json;
using Gwota.Core.Data;
using Gwota.Core.Session;

namespace Gwota.Core.DeckBuilding;

/// <summary>
/// Reads and writes the player's decks as JSON. Reading migrates old ids and drops or clamps anything invalid;
/// null means no usable deck was found.
/// </summary>
public static class DeckCollectionSerializer
{
    public const int CurrentVersion = 3;

    private static readonly Faction[] AddedInVersion3 = [Faction.Horde];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static string Serialize(DeckCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var file = new FileDto
        {
            Version = CurrentVersion,
            Decks = collection.Decks.Select(d => (DeckDto?)new DeckDto
            {
                Id = d.Id,
                Name = d.Name,
                Faction = d.Faction.ToString().ToLowerInvariant(),
                Leader = d.LeaderId,
                Cards = d.Cards.ToDictionary(e => e.CardId, e => e.Count, StringComparer.Ordinal),
                CardBack = d.CardBackId,
            }).ToList(),
        };
        return JsonSerializer.Serialize(file, Options);
    }

    public static DeckCollection? Deserialize(string? json, GameData data, DeckRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        FileDto? file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        var version = file?.Version ?? CurrentVersion;
        var decks = (file?.Decks ?? []).Select(d => Sanitize(Migrate(d, version), data, rules ?? DeckRules.Classic)).OfType<DeckDefinition>().ToList();
        if (decks.Count == 0)
        {
            return null;
        }

        var collection = new DeckCollection(decks);
        if (version < 3)
        {
            AddMissingFactions(collection, data);
        }

        return collection;
    }

    private static DeckDto? Migrate(DeckDto? dto, int version)
    {
        if (dto is null || version >= CurrentVersion)
        {
            return dto;
        }

        var renameIds = version < 2;
        return new DeckDto
        {
            Id = dto.Id,
            Name = dto.Name,
            Faction = dto.Faction is { } faction ? LegacyIds.Factions.GetValueOrDefault(faction.ToLowerInvariant(), faction) : null,
            Leader = dto.Leader is { } leader && renameIds ? LegacyIds.Leaders.GetValueOrDefault(leader, leader) : dto.Leader,
            Cards = renameIds
                ? dto.Cards?
                    .GroupBy(e => LegacyIds.Cards.GetValueOrDefault(e.Key, e.Key), StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Value), StringComparer.Ordinal)
                : dto.Cards,
            CardBack = dto.CardBack,
        };
    }

    /// <summary>Gives older saves the shipped decks of factions added in version 3.</summary>
    private static void AddMissingFactions(DeckCollection collection, GameData data)
    {
        var owned = collection.Decks.Select(d => d.Faction).ToHashSet();
        foreach (var deck in MatchFactory.PlayableDecks(data)
                     .Where(d => AddedInVersion3.Contains(d.Faction) && !owned.Contains(d.Faction) && collection.Find(d.Id) is null))
        {
            collection.Save(deck);
            owned.Add(deck.Faction);
        }
    }

    private static DeckDefinition? Sanitize(DeckDto? dto, GameData data, DeckRules rules)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Id)
            || !Enum.TryParse<Faction>(dto.Faction, ignoreCase: true, out var faction) || faction == Faction.Neutral
            || !Enum.IsDefined(faction))
        {
            return null;
        }

        var leaderId = dto.Leader is { } id && data.Leaders.TryGetValue(id, out var leader) && leader.Faction == faction
            ? id
            : DeckEditor.DefaultLeader(data, faction).Id;

        var cards = new List<DeckEntry>();
        foreach (var (cardId, count) in dto.Cards ?? [])
        {
            if (count < 1 || !data.Cards.TryGetValue(cardId, out var card) || (card.Faction != Faction.Neutral && card.Faction != faction))
            {
                continue;
            }

            cards.Add(new DeckEntry(cardId, Math.Min(count, card.IsHero ? rules.MaxHeroCopies : rules.MaxCopiesPerCard)));
        }

        return new DeckDefinition
        {
            Id = dto.Id.Trim(),
            Name = DeckEditor.NormalizeName(dto.Name) ?? "Unnamed deck",
            Faction = faction,
            LeaderId = leaderId,
            Cards = cards,
            CardBackId = CardBacks.Normalize(dto.CardBack),
        };
    }

    private sealed class FileDto
    {
        public int Version { get; set; }

        public List<DeckDto?>? Decks { get; set; }
    }

    private sealed class DeckDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Faction { get; set; }

        public string? Leader { get; set; }

        public Dictionary<string, int>? Cards { get; set; }

        public string? CardBack { get; set; }
    }
}
