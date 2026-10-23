namespace Gwota.Net.Tests.Support;

/// <summary>The game data shipped in <c>game/data</c>, loaded once.</summary>
public static class Shipped
{
    public static GameData Data { get; } = GameDataLoader.Load(new FileSystemDataSource(Path.Combine(FindRoot(), "game", "data")));

    public static DeckDefinition DeckOf(Faction faction) => Data.Decks.Values.OrderBy(d => d.Id, StringComparer.Ordinal).First(d => d.Faction == faction);

    public static DeckDefinition FirstDeck => Data.Decks.Values.OrderBy(d => d.Id, StringComparer.Ordinal).First();

    public static DeckDefinition LastDeck => Data.Decks.Values.OrderBy(d => d.Id, StringComparer.Ordinal).Last();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Gwota.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root (Gwota.slnx).");
    }
}