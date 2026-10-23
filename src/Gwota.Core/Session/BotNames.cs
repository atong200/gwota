namespace Gwota.Core.Session;

/// <summary>
/// Bot display names ("BOT Gabe") drawn from a pool of first names. Callers only use <see cref="Pick"/>,
/// so other pools can be added per instance.
/// </summary>
public sealed class BotNames
{
    public const string Prefix = "BOT ";

    private readonly List<string> _names;

    public BotNames(IEnumerable<string> firstNames)
    {
        ArgumentNullException.ThrowIfNull(firstNames);

        _names = firstNames
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => Prefix + n)
            .ToList();
        if (_names.Count == 0)
        {
            throw new ArgumentException("A bot name pool needs at least one name.", nameof(firstNames));
        }
    }

    /// <summary>The pool used until bot difficulty tiers exist.</summary>
    public static BotNames Default { get; } = new(
    [
        "Gabe", "Albert", "Bert", "Cliffe", "Crusher", "Dave", "Erik", "Frank", "Garret", "Hank",
        "Ivan", "Jim", "Kurt", "Lester", "Moe", "Ned", "Oscar", "Quinn", "Rip", "Sam",
        "Ted", "Ulric", "Vinny", "Wolf", "Yuri", "Zed",
    ]);

    /// <summary>Every full name of the pool ("BOT ..."), in pool order.</summary>
    public IReadOnlyList<string> All => _names;

    /// <summary>
    /// Picks a name, avoiding <paramref name="taken"/> (case-insensitive) when possible so two players never share
    /// a name. If the whole pool is taken, any name is returned.
    /// </summary>
    public string Pick(IRandom random, IEnumerable<string>? taken = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        var excluded = new HashSet<string>(taken ?? [], StringComparer.OrdinalIgnoreCase);
        var free = _names.Where(n => !excluded.Contains(n)).ToList();
        return random.Pick(free.Count > 0 ? free : _names);
    }
}
