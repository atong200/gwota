using System.Text.Json.Serialization;

namespace Gwota.Core.Data;

// Raw JSON shapes. Everything is nullable so validation can report precise, friendly errors
// instead of failing deserialization on the first missing field.

internal sealed class CardDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public Faction? Faction { get; set; }

    public CardKind? Kind { get; set; }

    public List<RowType>? Rows { get; set; }

    public int? Strength { get; set; }

    public bool? Hero { get; set; }

    public List<UnitAbility>? Abilities { get; set; }

    public SpecialEffect? Effect { get; set; }

    public string? MusterGroup { get; set; }

    public string? Description { get; set; }
}

internal sealed class LeaderDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public Faction? Faction { get; set; }

    public LeaderAbility? Ability { get; set; }

    public RowType? Row { get; set; }

    public string? Description { get; set; }
}

internal sealed class DeckDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public Faction? Faction { get; set; }

    public string? Leader { get; set; }

    public Dictionary<string, int>? Cards { get; set; }
}
