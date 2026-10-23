namespace Gwota.Core.Tests.Support;

/// <summary>Factory for card and leader definitions used by tests. Ids are derived from names when omitted.</summary>
internal static class TestCards
{
    public static CardDefinition Unit(
        int strength,
        RowType row = RowType.Melee,
        string? id = null,
        Faction faction = Faction.Neutral,
        bool hero = false,
        string? musterGroup = null,
        params UnitAbility[] abilities) =>
        new()
        {
            Id = id ?? $"unit_{row.ToString().ToLowerInvariant()}_{strength}{(hero ? "_hero" : string.Empty)}",
            Name = id ?? $"Unit {strength}",
            Faction = faction,
            Kind = CardKind.Unit,
            Rows = [row],
            Strength = strength,
            IsHero = hero,
            Abilities = abilities,
            MusterGroup = musterGroup,
        };

    public static CardDefinition Hero(int strength, RowType row = RowType.Melee, string? id = null) =>
        Unit(strength, row, id ?? $"hero_{strength}", hero: true);

    public static CardDefinition Agile(int strength, string id = "agile", params RowType[] rows) =>
        Unit(strength, id: id) with { Rows = rows.Length > 0 ? rows : [RowType.Melee, RowType.Ranged] };

    public static CardDefinition WithAbility(int strength, UnitAbility ability, RowType row = RowType.Melee, string? id = null) =>
        Unit(strength, row, id ?? $"{ability.ToString().ToLowerInvariant()}_{strength}", abilities: ability);

    public static CardDefinition Spy(int strength = 1, RowType row = RowType.Melee) =>
        WithAbility(strength, UnitAbility.Spy, row, "spy");

    public static CardDefinition Medic(int strength = 1, RowType row = RowType.Melee) =>
        WithAbility(strength, UnitAbility.Medic, row, "medic");

    public static CardDefinition Muster(int strength, string group, RowType row = RowType.Melee, string? id = null) =>
        Unit(strength, row, id ?? $"{group}_{row.ToString().ToLowerInvariant()}", musterGroup: group, abilities: UnitAbility.Muster);

    public static CardDefinition Special(SpecialEffect effect, string? id = null, params RowType[] rows) =>
        new()
        {
            Id = id ?? effect.ToString().ToLowerInvariant(),
            Name = id ?? effect.ToString(),
            Faction = Faction.Neutral,
            Kind = CardKind.Special,
            Effect = effect,
            Rows = rows,
        };

    public static CardDefinition Weather(params RowType[] rows) =>
        Special(SpecialEffect.Weather, "weather_" + string.Join('_', rows.Select(r => r.ToString().ToLowerInvariant())), rows);

    public static CardDefinition Horn(params RowType[] rows) => Special(SpecialEffect.Horn, "horn", rows);

    public static CardDefinition Scorch() => Special(SpecialEffect.Scorch);

    public static CardDefinition Decoy() => Special(SpecialEffect.Decoy);

    public static CardDefinition ClearWeather() => Special(SpecialEffect.ClearWeather, "clear_weather");

    public static LeaderDefinition Leader(LeaderAbility ability, RowType? row = null, Faction faction = Faction.Realms) =>
        new()
        {
            Id = "leader_" + ability.ToString().ToLowerInvariant(),
            Name = "Leader " + ability,
            Faction = faction,
            Ability = ability,
            Row = row,
        };
}
