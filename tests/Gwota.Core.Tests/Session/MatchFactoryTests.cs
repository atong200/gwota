using Gwota.Core.Data;
using Gwota.Core.Session;

namespace Gwota.Core.Tests.Session;

public class MatchFactoryTests
{
    private static readonly GameData Shipped = GameDataLoader.Load(new FileSystemDataSource(RepoPaths.GameData));

    private static DeckDefinition Deck(string id) => Shipped.Decks[id];

    [Fact]
    public void Playable_decks_are_ordered_by_faction_then_name() =>
        MatchFactory.PlayableDecks(Shipped).Select(d => d.Faction).ShouldBeInOrder();

    [Fact]
    public void Opponent_comes_from_another_faction()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            MatchFactory.PickOpponent(Shipped, Deck("realms_starter"), new SeededRandom(seed)).Faction.ShouldNotBe(Faction.Realms);
        }
    }

    [Fact]
    public void Opponent_falls_back_to_any_deck_when_no_rival_faction_exists()
    {
        var realms = Deck("realms_starter");
        var onlyRealms = new GameData(Shipped.Cards, Shipped.Leaders, new Dictionary<string, DeckDefinition> { [realms.Id] = realms });

        MatchFactory.PickOpponent(onlyRealms, realms, new SeededRandom(1)).ShouldBe(realms);
    }

    [Fact]
    public void Versus_ai_accepts_a_player_built_deck_that_is_not_shipped()
    {
        var built = Deck("realms_starter") with { Id = "deck_7", Name = "My deck" };

        var session = MatchFactory.VersusAi(Shipped, new MatchSetup(built, Deck("hells_starter"), 3, "Tong", "BOT Gabe"));

        session.State.Player(MatchFactory.HumanSeat).Faction.ShouldBe(Faction.Realms);
    }

    [Fact]
    public void Versus_ai_puts_the_human_in_seat_one_and_the_ai_in_seat_two()
    {
        var session = MatchFactory.VersusAi(Shipped, new MatchSetup(Deck("realms_starter"), Deck("hells_starter"), 3, "Tong", "BOT Gabe"));

        session.IsControlled(MatchFactory.HumanSeat).ShouldBeFalse();
        session.IsControlled(MatchFactory.AiSeat).ShouldBeTrue();
        session.State.Player(Seat.One).Name.ShouldBe("Tong");
        session.State.Player(Seat.Two).Name.ShouldBe("BOT Gabe");
        session.State.Player(Seat.Two).Leader.Id.ShouldBe(Deck("hells_starter").LeaderId);
    }

    [Fact]
    public void Names_do_not_depend_on_factions()
    {
        var session = MatchFactory.VersusAi(Shipped, new MatchSetup(Deck("hells_starter"), Deck("hells_starter"), 3));

        session.State.Player(Seat.One).Name.ShouldBe(MatchSetup.DefaultPlayerName);
        session.State.Player(Seat.Two).Name.ShouldBe(MatchSetup.DefaultOpponentName);
    }

    [Fact]
    public void Versus_ai_setup_names_the_player_and_picks_a_bot()
    {
        var setup = MatchFactory.VersusAiSetup(Shipped, Deck("realms_starter"), "Tong", new SeededRandom(7), 99);

        setup.PlayerName.ShouldBe("Tong");
        setup.OpponentName.ShouldStartWith(BotNames.Prefix);
        setup.OpponentDeck.Faction.ShouldNotBe(Faction.Realms);
        setup.Seed.ShouldBe(99UL);
    }

    [Fact]
    public void Versus_ai_setup_never_gives_the_bot_the_player_name()
    {
        var taken = BotNames.Default.All[0];
        for (ulong seed = 1; seed <= 30; seed++)
        {
            MatchFactory.VersusAiSetup(Shipped, Deck("realms_starter"), taken, new SeededRandom(seed), seed).OpponentName.ShouldNotBe(taken);
        }
    }

    [Fact]
    public void Ai_versus_ai_setup_uses_two_distinct_bot_names()
    {
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var setup = MatchFactory.AiVersusAiSetup(Deck("realms_starter"), Deck("hells_starter"), new SeededRandom(seed), seed);

            setup.PlayerName.ShouldStartWith(BotNames.Prefix);
            setup.OpponentName.ShouldStartWith(BotNames.Prefix);
            setup.PlayerName.ShouldNotBe(setup.OpponentName);
        }
    }

    [Fact]
    public void A_rematch_keeps_the_same_names()
    {
        var setup = new MatchSetup(Deck("realms_starter"), Deck("hells_starter"), 3, "Tong", "BOT Gabe");

        var rematch = MatchFactory.VersusAi(Shipped, setup with { Seed = setup.Seed + 1 });

        rematch.State.Player(Seat.Two).Name.ShouldBe("BOT Gabe");
    }

    [Fact]
    public void Same_setup_produces_the_same_match()
    {
        var setup = new MatchSetup(Deck("realms_starter"), Deck("hells_starter"), 42);

        var first = MatchFactory.VersusAi(Shipped, setup).State.Player(Seat.One).Hand.Select(c => c.Definition.Id);
        var second = MatchFactory.VersusAi(Shipped, setup).State.Player(Seat.One).Hand.Select(c => c.Definition.Id);

        first.ShouldBe(second);
    }

    [Fact]
    public void Ai_versus_ai_sessions_finish()
    {
        var session = MatchFactory.AiVersusAi(Shipped, new MatchSetup(Deck("realms_starter"), Deck("hells_starter"), 5));

        session.RunControllers();

        session.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void Versus_player_has_no_controllers_and_seats_the_player_deck_first()
    {
        var session = MatchFactory.VersusPlayer(Shipped, new MatchSetup(Deck("realms_starter"), Deck("hells_starter"), 3, "Alice", "Bob"));

        session.IsControlled(Seat.One).ShouldBeFalse();
        session.IsControlled(Seat.Two).ShouldBeFalse();
        session.State.Player(Seat.One).Name.ShouldBe("Alice");
        session.State.Player(Seat.Two).Name.ShouldBe("Bob");
    }

    [Fact]
    public void Versus_player_is_deterministic_for_a_seed()
    {
        var setup = new MatchSetup(Deck("realms_starter"), Deck("hells_starter"), 11, "Alice", "Bob");

        MatchFactory.VersusPlayer(Shipped, setup).Checksum().ShouldBe(MatchFactory.VersusPlayer(Shipped, setup).Checksum());
    }}
