using Godot;
using Gwota.Core.Cards;
using Gwota.Core.Data;
using Gwota.Core.DeckBuilding;
using FileAccess = Godot.FileAccess;

namespace Gwota.Game.Infrastructure;

/// <summary>
/// Owns the player's decks in <c>user://decks.json</c>, seeded with the shipped decks and written on every change.
/// An unreadable file is kept as <c>decks.json.bak</c> before being replaced.
/// </summary>
public sealed class DeckStore
{
    private const string FilePath = "user://decks.json";
    private const string BackupPath = "user://decks.json.bak";

    private DeckStore(DeckCollection collection) => Collection = collection;

    public DeckCollection Collection { get; }

    public static DeckStore Load(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        string? json = null;
        if (FileAccess.FileExists(FilePath))
        {
            using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
            json = file?.GetAsText();
        }

        var result = DeckLoadResult.From(json, data);
        var store = new DeckStore(result.Collection);
        switch (result.Status)
        {
            case DeckLoadStatus.Seeded:
                store.Write();
                break;
            case DeckLoadStatus.Recovered:
                var error = DirAccess.CopyAbsolute(FilePath, BackupPath);
                GD.PushWarning($"{FilePath} holds no usable deck; default decks restored (old file copied to {BackupPath}: {error}).");
                store.Write();
                break;
        }

        return store;
    }

    /// <summary>Creates, stores and returns an empty deck of <paramref name="faction"/>.</summary>
    public DeckDefinition Create(Faction faction, GameData data)
    {
        var deck = Collection.Create(faction, data);
        Write();
        return deck;
    }

    public void Save(DeckDefinition deck)
    {
        Collection.Save(deck);
        Write();
    }

    public bool Delete(string id)
    {
        var deleted = Collection.Delete(id);
        if (deleted)
        {
            Write();
        }

        return deleted;
    }

    /// <summary>Failures are logged, never thrown: the decks stay usable for this session.</summary>
    private void Write()
    {
        using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"Cannot save decks to {FilePath}: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(DeckCollectionSerializer.Serialize(Collection));
    }
}
