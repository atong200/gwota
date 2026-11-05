using Godot;
using Gwota.Core.Data;
using Gwota.Core.Session;
using Gwota.Game.Infrastructure;

namespace Gwota.Game;

/// <summary>Headless load-and-play check used by CI (<c>-- --smoke</c>).</summary>
public static class SmokeTest
{
    private const int SeedsPerPairing = 3;

    /// <summary>Checks every card back loads and every card image is where its card looks for it, then plays AI-vs-AI matches for every pairing of decks. Returns the process exit code.</summary>
    public static int Run(GameDataLoadResult load, string version)
    {
        ArgumentNullException.ThrowIfNull(load);
        if (!load.IsValid)
        {
            foreach (var error in load.Errors)
            {
                GD.PrintErr(error);
            }

            return 1;
        }

        var backs = CardArt.CardBacks().Entries;
        var broken = backs.Where(b => b.File is not null && CardArt.CardBack(b.Id) is null).ToList();
        if (broken.Count > 0)
        {
            GD.PrintErr($"Smoke: card backs that do not load: {string.Join(", ", broken.Select(b => b.File))}.");
            return 1;
        }

        var misplaced = ArtPaths.FindMisplacedCardArt(load.Data, CardArt.CardArtFiles());
        if (misplaced.Count > 0)
        {
            foreach (var problem in misplaced)
            {
                GD.PrintErr($"Smoke: card art {problem}");
            }

            return 1;
        }

        try
        {
            var decks = MatchFactory.PlayableDecks(load.Data);
            var matches = 0;
            foreach (var one in decks)
            {
                foreach (var two in decks)
                {
                    for (ulong seed = 1; seed <= SeedsPerPairing; seed++)
                    {
                        var session = MatchFactory.AiVersusAi(load.Data, new MatchSetup(one, two, seed));
                        session.RunControllers();
                        if (!session.IsFinished)
                        {
                            GD.PrintErr($"Smoke: {one.Id} vs {two.Id} (seed {seed}) did not finish.");
                            return 1;
                        }

                        matches++;
                    }
                }
            }

            GD.Print($"Smoke OK: {load.Data.Cards.Count} cards, {load.Data.Leaders.Count} leaders, {backs.Count} card backs, {decks.Count} decks, {matches} AI matches (v{version}).");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            GD.PrintErr($"Smoke failed: {ex}");
            return 1;
        }
    }
}
