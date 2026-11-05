using Gwota.Core.Data;
using Gwota.Net.Lan;

namespace Gwota.Game.Ui;

/// <summary>Switches between the app's screens.</summary>
public interface INavigator
{
    void ShowMainMenu(MainMenu.Page page = MainMenu.Page.Title);

    void ShowDecks();

    void ShowLan();

    /// <summary>A match against the computer with the player's <paramref name="playerDeck"/>.</summary>
    void StartMatch(DeckDefinition playerDeck);

    /// <summary>A match against a remote player; also called again when that match starts a rematch.</summary>
    void StartMatch(LanMatch link);

    void Quit();
}
