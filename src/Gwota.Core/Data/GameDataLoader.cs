using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Gwota.Core.Data;

/// <summary>Result of loading data without throwing: the usable data plus every problem found.</summary>
public sealed record GameDataLoadResult(GameData Data, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Loads and validates the data folder:
/// <c>cards/&lt;faction&gt;/&lt;id&gt;.json</c>, <c>leaders/&lt;id&gt;.json</c>, <c>decks/&lt;id&gt;.json</c>.
/// </summary>
public static partial class GameDataLoader
{
    public const string CardsDirectory = "cards";
    public const string LeadersDirectory = "leaders";
    public const string DecksDirectory = "decks";
    public const string JsonExtension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
    };

    /// <summary>Loads everything, throwing <see cref="GameDataException"/> listing every error.</summary>
    public static GameData Load(IDataSource source)
    {
        var result = TryLoad(source);
        return result.IsValid ? result.Data : throw new GameDataException(result.Errors);
    }

    public static GameDataLoadResult TryLoad(IDataSource source, DeckRules? deckRules = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var errors = new List<string>();
        var cards = LoadAll<CardDto, CardDefinition>(source, CardsDirectory, ValidateCard, c => c.Id, errors);
        var leaders = LoadAll<LeaderDto, LeaderDefinition>(source, LeadersDirectory, ValidateLeader, l => l.Id, errors);
        var decks = LoadAll<DeckDto, DeckDefinition>(source, DecksDirectory, ValidateDeckShape, d => d.Id, errors);

        foreach (var deck in decks.Values)
        {
            errors.AddRange(DeckValidator.Validate(deck, cards, leaders, deckRules).Select(e => $"Deck '{deck.Id}': {e}"));
        }

        return new GameDataLoadResult(new GameData(cards, leaders, decks), errors);
    }

    public static string ToDataName<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    private static Dictionary<string, TDefinition> LoadAll<TDto, TDefinition>(
        IDataSource source,
        string directory,
        Func<TDto, string, List<string>, TDefinition?> validate,
        Func<TDefinition, string> idOf,
        List<string> errors)
        where TDto : class
        where TDefinition : class
    {
        var result = new Dictionary<string, TDefinition>(StringComparer.Ordinal);
        foreach (var path in source.ListFiles(directory, JsonExtension))
        {
            var fileErrors = new List<string>();
            var definition = Parse<TDto>(source, path, fileErrors) is { } dto ? validate(dto, path, fileErrors) : null;

            if (definition is not null && !result.TryAdd(idOf(definition), definition))
            {
                fileErrors.Add($"Duplicate id '{idOf(definition)}'.");
            }

            errors.AddRange(fileErrors.Select(e => $"{path}: {e}"));
        }

        return result;
    }

    private static TDto? Parse<TDto>(IDataSource source, string path, List<string> errors)
        where TDto : class
    {
        try
        {
            var dto = JsonSerializer.Deserialize<TDto>(source.ReadAllText(path), JsonOptions);
            if (dto is null)
            {
                errors.Add("File is empty.");
            }

            return dto;
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return null;
        }
    }

    private static CardDefinition? ValidateCard(CardDto dto, string path, List<string> errors)
    {
        ValidateIdentity(dto.Id, dto.Name, path, errors);
        if (dto.Faction is null)
        {
            errors.Add("'faction' is required.");
        }
        else
        {
            // cards/<faction>/[any/sub/folders/]<id>.json
            var parts = path.Split('/');
            var folder = parts.Length >= 3 ? parts[1] : null;
            var expectedFolder = ToDataName(dto.Faction.Value);
            if (folder != expectedFolder)
            {
                errors.Add($"A {expectedFolder} card must be stored in '{CardsDirectory}/{expectedFolder}/'.");
            }
        }

        if (dto.Kind is null)
        {
            errors.Add("'kind' is required (unit or special).");
        }

        var rows = dto.Rows ?? [];
        var abilities = dto.Abilities ?? [];
        if (rows.Distinct().Count() != rows.Count)
        {
            errors.Add("'rows' contains duplicates.");
        }

        if (abilities.Distinct().Count() != abilities.Count)
        {
            errors.Add("'abilities' contains duplicates.");
        }

        if (dto.Kind == CardKind.Unit)
        {
            ValidateUnit(dto, rows, abilities, errors);
        }
        else if (dto.Kind == CardKind.Special)
        {
            ValidateSpecial(dto, rows, abilities, errors);
        }

        if (errors.Count > 0)
        {
            return null;
        }

        return new CardDefinition
        {
            Id = dto.Id!,
            Name = dto.Name!,
            Faction = dto.Faction!.Value,
            Kind = dto.Kind!.Value,
            Rows = rows,
            Strength = dto.Strength ?? 0,
            IsHero = dto.Hero ?? false,
            Abilities = abilities,
            Effect = dto.Effect,
            MusterGroup = dto.MusterGroup,
            Description = dto.Description,
        };
    }

    private static void ValidateUnit(CardDto dto, List<RowType> rows, List<UnitAbility> abilities, List<string> errors)
    {
        if (rows.Count == 0)
        {
            errors.Add("A unit needs at least one row in 'rows'.");
        }

        if (dto.Strength is null or < 0)
        {
            errors.Add("A unit needs a 'strength' of 0 or more.");
        }

        if (dto.Effect is not null)
        {
            errors.Add("A unit cannot have an 'effect'; use 'abilities'.");
        }

        if (abilities.Contains(UnitAbility.Muster) && string.IsNullOrWhiteSpace(dto.MusterGroup))
        {
            errors.Add("A unit with the 'muster' ability needs a 'musterGroup'.");
        }
    }

    private static void ValidateSpecial(CardDto dto, List<RowType> rows, List<UnitAbility> abilities, List<string> errors)
    {
        if (dto.Effect is null)
        {
            errors.Add("A special card needs an 'effect'.");
        }

        if (dto.Strength is not null and not 0)
        {
            errors.Add("A special card cannot have a 'strength'.");
        }

        if (dto.Hero == true)
        {
            errors.Add("A special card cannot be a hero.");
        }

        if (abilities.Count > 0)
        {
            errors.Add("A special card cannot have 'abilities'; use 'effect'.");
        }

        if (dto.MusterGroup is not null)
        {
            errors.Add("A special card cannot have a 'musterGroup'.");
        }

        switch (dto.Effect)
        {
            case SpecialEffect.Weather when rows.Count == 0:
                errors.Add("A weather card needs the affected rows in 'rows'.");
                break;
            case SpecialEffect.Scorch or SpecialEffect.Decoy or SpecialEffect.ClearWeather when rows.Count > 0:
                errors.Add($"A '{ToDataName(dto.Effect.Value)}' card cannot have 'rows'.");
                break;
        }
    }

    private static LeaderDefinition? ValidateLeader(LeaderDto dto, string path, List<string> errors)
    {
        ValidateIdentity(dto.Id, dto.Name, path, errors);
        if (dto.Faction is null or Faction.Neutral)
        {
            errors.Add("A leader needs a 'faction' other than neutral.");
        }

        if (dto.Ability is null)
        {
            errors.Add("'ability' is required.");
        }
        else
        {
            var needsRow = dto.Ability is LeaderAbility.HornRow or LeaderAbility.ScorchRow or LeaderAbility.PlayWeatherFromDeck;
            if (needsRow && dto.Row is null)
            {
                errors.Add($"Ability '{ToDataName(dto.Ability.Value)}' needs a 'row'.");
            }
            else if (!needsRow && dto.Row is not null)
            {
                errors.Add($"Ability '{ToDataName(dto.Ability.Value)}' does not use a 'row'.");
            }
        }

        return errors.Count > 0
            ? null
            : new LeaderDefinition
            {
                Id = dto.Id!,
                Name = dto.Name!,
                Faction = dto.Faction!.Value,
                Ability = dto.Ability!.Value,
                Row = dto.Row,
                Description = dto.Description,
            };
    }

    private static DeckDefinition? ValidateDeckShape(DeckDto dto, string path, List<string> errors)
    {
        ValidateIdentity(dto.Id, dto.Name, path, errors);
        if (dto.Faction is null)
        {
            errors.Add("'faction' is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Leader))
        {
            errors.Add("'leader' is required.");
        }

        if (dto.Cards is null || dto.Cards.Count == 0)
        {
            errors.Add("'cards' must list at least one card.");
        }

        return errors.Count > 0
            ? null
            : new DeckDefinition
            {
                Id = dto.Id!,
                Name = dto.Name!,
                Faction = dto.Faction!.Value,
                LeaderId = dto.Leader!,
                Cards = dto.Cards!.Select(kv => new DeckEntry(kv.Key, kv.Value)).ToList(),
            };
    }

    private static void ValidateIdentity(string? id, string? name, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add("'id' is required.");
        }
        else
        {
            if (!IdPattern().IsMatch(id))
            {
                errors.Add($"Id '{id}' must be snake_case (lowercase letters, digits and underscores).");
            }

            var fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName != id)
            {
                errors.Add($"Id '{id}' must match the file name '{fileName}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("'name' is required.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(_[a-z0-9]+)*$")]
    private static partial Regex IdPattern();
}
