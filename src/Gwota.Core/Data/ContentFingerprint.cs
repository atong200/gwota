namespace Gwota.Core.Data;

/// <summary>A stable hash of the gameplay data of every card and leader, used to check two games share the same content.</summary>
public static class ContentFingerprint
{
    public static string Compute(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var hash = new Fnv64();
        foreach (var card in data.Cards.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            hash.Add(card.Id);
            hash.Add((int)card.Faction);
            hash.Add((int)card.Kind);
            hash.Add(card.Strength);
            hash.Add(card.IsHero);
            hash.Add(card.Effect is { } effect ? (int)effect : -1);
            hash.Add(card.MusterGroup);
            hash.Add(card.Rows.Count);
            foreach (var row in card.Rows)
            {
                hash.Add((int)row);
            }

            hash.Add(card.Abilities.Count);
            foreach (var ability in card.Abilities)
            {
                hash.Add((int)ability);
            }
        }

        foreach (var leader in data.Leaders.Values.OrderBy(l => l.Id, StringComparer.Ordinal))
        {
            hash.Add(leader.Id);
            hash.Add((int)leader.Faction);
            hash.Add((int)leader.Ability);
            hash.Add(leader.Row is { } row ? (int)row : -1);
        }

        return hash.Value.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }
}