using Gwota.Net.Lan;
using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Lan;

public sealed class LanHostTests : IDisposable
{
    private readonly InMemoryListener _listener = new();
    private readonly ManualTime _time = new();

    [Fact]
    public void A_matching_guest_starts_the_match_on_both_sides()
    {
        using var host = Host();
        using var guest = Guest(new LanPlayer("Bob", Shipped.LastDeck));

        host.Poll();
        guest.Poll();

        var hostMatch = host.Match.ShouldNotBeNull();
        var guestMatch = guest.Match.ShouldNotBeNull();
        hostMatch.LocalSeat.ShouldBe(Seat.One);
        guestMatch.LocalSeat.ShouldBe(Seat.Two);
        guestMatch.Setup.Seed.ShouldBe(hostMatch.Setup.Seed);
        hostMatch.Session.State.Player(Seat.One).Name.ShouldBe("Alice");
        guestMatch.Session.State.Player(Seat.Two).Name.ShouldBe("Bob");
        guestMatch.Session.State.Player(Seat.Two).Faction.ShouldBe(Shipped.LastDeck.Faction);
        guestMatch.Session.Checksum().ShouldBe(hostMatch.Session.Checksum());
    }

    [Fact]
    public void The_host_stops_listening_once_a_match_starts()
    {
        using var host = Host();
        using var guest = Guest();
        host.Poll();

        _listener.IsDisposed.ShouldBeTrue();
        _listener.Connect().IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void A_guest_of_another_version_is_turned_away()
    {
        using var host = Host();
        using var guest = Guest(identity: LanRig.Identity with { GameVersion = "other" });

        host.Poll();
        guest.Poll();

        host.Match.ShouldBeNull();
        guest.Match.ShouldBeNull();
        guest.Error.ShouldNotBeNull().ShouldContain("Version mismatch");
        host.LastRejection.ShouldBe(guest.Error);
    }

    [Fact]
    public void A_guest_with_other_card_data_is_turned_away()
    {
        using var host = Host();
        using var guest = Guest(identity: LanRig.Identity with { Fingerprint = "0000" });

        host.Poll();
        guest.Poll();

        guest.Error.ShouldNotBeNull().ShouldContain("card data");
    }

    [Fact]
    public void A_guest_with_an_unplayable_deck_is_turned_away()
    {
        using var host = Host();
        using var guest = Guest(new LanPlayer("Bob", Shipped.FirstDeck with { Cards = [] }));

        host.Poll();
        guest.Poll();

        guest.Error.ShouldNotBeNull().ShouldStartWith("Your deck cannot be played");
    }

    [Fact]
    public void After_a_rejection_the_host_keeps_waiting()
    {
        using var host = Host();
        using var wrong = Guest(identity: LanRig.Identity with { Fingerprint = "0000" });
        host.Poll();

        using var right = Guest();
        host.Poll();
        right.Poll();

        right.Match.ShouldNotBeNull();
    }

    [Fact]
    public void A_connection_that_never_says_hello_is_dropped()
    {
        using var host = Host();
        var silent = _listener.Connect();
        host.Poll();

        _time.Advance(LanHost.HelloTimeout + TimeSpan.FromSeconds(1));
        host.Poll();

        silent.IsClosed.ShouldBeTrue();
        host.Match.ShouldBeNull();
    }

    [Fact]
    public void Guest_names_are_normalized()
    {
        using var host = Host();
        using var guest = Guest(new LanPlayer("  A very long name indeed, really  ", Shipped.LastDeck));

        host.Poll();
        guest.Poll();

        host.Match.ShouldNotBeNull().Setup.OpponentName.Length.ShouldBeLessThanOrEqualTo(16);
        guest.Match.ShouldNotBeNull().Setup.OpponentName.ShouldBe(host.Match.Setup.OpponentName);
    }

    [Fact]
    public void Disposing_the_host_stops_listening()
    {
        var host = Host();

        host.Dispose();

        _listener.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void A_guest_whose_host_disappears_reports_it()
    {
        var (client, server) = InMemoryConnection.CreatePair();
        using var guest = new LanGuest(client, Shipped.Data, LanRig.Identity, new LanPlayer("Bob", Shipped.LastDeck));

        server.Close("gone");
        guest.Poll();

        guest.Error.ShouldBe("The host closed the connection.");
    }

    [Fact]
    public void A_guest_refuses_decks_it_cannot_play()
    {
        var (client, server) = InMemoryConnection.CreatePair();
        using var guest = new LanGuest(client, Shipped.Data, LanRig.Identity, new LanPlayer("Bob", Shipped.LastDeck));
        var bad = DeckDto.From(Shipped.FirstDeck) with { LeaderId = "nobody" };

        server.Send(NetCodec.Encode(new StartMessage(1, "Alice", bad, "Bob", DeckDto.From(Shipped.LastDeck))));
        guest.Poll();

        guest.Match.ShouldBeNull();
        guest.Error.ShouldNotBeNull();
        client.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void The_guest_says_hello_with_its_identity_and_deck()
    {
        var (client, server) = InMemoryConnection.CreatePair();
        using var guest = new LanGuest(client, Shipped.Data, LanRig.Identity, new LanPlayer("Bob", Shipped.LastDeck));

        server.TryReceive(out var line).ShouldBeTrue();

        var hello = NetCodec.Decode(line).ShouldBeOfType<HelloMessage>();
        hello.Protocol.ShouldBe(NetProtocol.Version);
        hello.Fingerprint.ShouldBe(LanRig.Identity.Fingerprint);
        hello.Name.ShouldBe("Bob");
        hello.Deck.ShouldBe(DeckDto.From(Shipped.LastDeck), DeckComparer.Instance);
    }

    public void Dispose() => _listener.Dispose();

    private LanHost Host() =>
        new(_listener, Shipped.Data, LanRig.Identity, new LanPlayer("Alice", Shipped.FirstDeck), () => 99, _time);

    private LanGuest Guest(LanPlayer? player = null, LanIdentity? identity = null) =>
        new(_listener.Connect(), Shipped.Data, identity ?? LanRig.Identity, player ?? new LanPlayer("Bob", Shipped.LastDeck));

    private sealed class DeckComparer : IEqualityComparer<DeckDto>
    {
        public static readonly DeckComparer Instance = new();

        public bool Equals(DeckDto? x, DeckDto? y) =>
            x is not null && y is not null && x.Name == y.Name && x.Faction == y.Faction && x.LeaderId == y.LeaderId && x.Cards.SequenceEqual(y.Cards);

        public int GetHashCode(DeckDto obj) => obj.Name.GetHashCode(StringComparison.Ordinal);
    }
}