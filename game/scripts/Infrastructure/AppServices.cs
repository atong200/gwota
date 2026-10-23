using Gwota.Core.Data;

namespace Gwota.Game.Infrastructure;

/// <summary>The loaded game data and the player's stores, shared by the menu screens.</summary>
public sealed record AppServices(GameData Data, SettingsStore Settings, DeckStore Decks, string Version);
