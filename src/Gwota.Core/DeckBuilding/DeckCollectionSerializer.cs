using System.Text.Json;
using Gwota.Core.Data;

namespace Gwota.Core.DeckBuilding;

/// <summary>
/// Reads and writes the player's decks as JSON. Reading migrates old ids and drops or clamps anything invalid;
/// null means no usable deck was found.
/// </summary>
public static class DeckCollectionSerializer
{
    public const int CurrentVersion = 2;

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

        var legacy = file is not null && file.Version < CurrentVersion;
        var decks = (file?.Decks ?? []).Select(d => Sanitize(legacy ? Migrate(d) : d, data, rules ?? DeckRules.Classic)).OfType<DeckDefinition>().ToList();
        var collection = new DeckCollection(decks);
        return collection.Count == 0 ? null : collection;
    }

    private static DeckDto? Migrate(DeckDto? dto) => dto is null ? null : new DeckDto
    {
        Id = dto.Id,
        Name = dto.Name,
        Faction = dto.Faction is { } faction ? LegacyIds.Factions.GetValueOrDefault(faction.ToLowerInvariant(), faction) : null,
        Leader = dto.Leader is { } leader ? LegacyIds.Leaders.GetValueOrDefault(leader, leader) : null,
        Cards = dto.Cards?
            .GroupBy(e => LegacyIds.Cards.GetValueOrDefault(e.Key, e.Key), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Value), StringComparer.Ordinal),
    };

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
    }
}
