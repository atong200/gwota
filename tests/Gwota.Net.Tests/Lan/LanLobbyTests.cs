using System.Net.Sockets;
using Gwota.Net.Discovery;
using Gwota.Net.Lan;
using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Lan;

public sealed class LanLobbyTests : IDisposable
{
    private static readonly LanAddress Local = new("127.0.0.1", NetProtocol.DefaultGamePort);

    private readonly FakeLanNetwork _network = new();
    private readonly List<IDisposable> _owned = [];

    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }
    }

    [Fact]
    public void A_new_lobby_browses_and_listens_for_games()
    {
        var lobby = Lobby();

        lobby.State.ShouldBe(LanLobbyState.Browsing);
        lobby.IsScanning.ShouldBeTrue();
        lobby.Match.ShouldBeNull();
    }

    [Fact]
    public void Heard_games_are_listed_but_not_the_lobby_itself()
    {
        var lobby = Lobby();
        var scanner = _network.ActiveScanner.ShouldNotBeNull();
        scanner.Hear(Beacon("other", "Bob"));
        scanner.Hear(Beacon(lobby.InstanceId, "Me"));

        lobby.Poll().ShouldBeTrue();

        lobby.Directory.Games.Select(g => g.Beacon.HostName).ShouldBe(["Bob"]);
        lobby.Poll().ShouldBeFalse();
    }

    [Fact]
    public void A_lobby_that_cannot_listen_for_games_can_still_host_and_join()
    {
        _network.ScanFails = true;
        var host = Lobby();
        var guest = Lobby();

        host.IsScanning.ShouldBeFalse();
        host.Host(Player("Alice"));
        guest.Join(Local, Player("Bob", Shipped.LastDeck));
        PollAll(host, guest);

        host.State.ShouldBe(LanLobbyState.Started);
        guest.State.ShouldBe(LanLobbyState.Started);
    }

    [Fact]
    public void Hosting_announces_the_game_and_stops_listening_for_games()
    {
        var lobby = Lobby();
        var scanner = _network.ActiveScanner.ShouldNotBeNull();

        lobby.Host(Player("  Alice  "), 7790, "  Friday   night ");
        lobby.Poll();

        lobby.State.ShouldBe(LanLobbyState.Hosting);
        lobby.HostPort.ShouldBe(7790);
        scanner.IsDisposed.ShouldBeTrue();
        lobby.IsScanning.ShouldBeFalse();
        var announcer = _network.Announcers.ShouldHaveSingleItem();
        announcer.Beacon.InstanceId.ShouldBe(lobby.InstanceId);
        announcer.Beacon.RoomName.ShouldBe("Friday night");
        announcer.Beacon.HostName.ShouldBe("Alice");
        announcer.Beacon.Port.ShouldBe(7790);
        announcer.Beacon.GameVersion.ShouldBe(LanRig.Identity.GameVersion);
        announcer.Polls.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void A_room_without_a_name_is_named_after_the_host(string? roomName)
    {
        var lobby = Lobby();

        lobby.Host(Player("Alice"), roomName: roomName);

        _network.Announcers.ShouldHaveSingleItem().Beacon.RoomName.ShouldBe("Alice's game");
    }

    [Fact]
    public void Refreshing_forgets_listed_games_until_they_announce_again()
    {
        var lobby = Lobby();
        var scanner = _network.ActiveScanner.ShouldNotBeNull();
        scanner.Hear(Beacon("other", "Bob"));
        lobby.Poll();
        lobby.Directory.Games.ShouldHaveSingleItem();

        lobby.Refresh();

        lobby.Directory.Games.ShouldBeEmpty();
        scanner.IsDisposed.ShouldBeFalse();
        scanner.Hear(Beacon("other", "Bob"));
        lobby.Poll().ShouldBeTrue();
        lobby.Directory.Games.ShouldHaveSingleItem();
    }

    [Fact]
    public void Refreshing_retries_listening_after_a_failure()
    {
        _network.ScanFails = true;
        var lobby = Lobby();
        lobby.IsScanning.ShouldBeFalse();

        _network.ScanFails = false;
        lobby.Refresh();

        lobby.IsScanning.ShouldBeTrue();
    }

    [Fact]
    public void Refreshing_while_hosting_does_not_listen_for_games()
    {
        var lobby = Lobby();
        lobby.Host(Player("Alice"));

        lobby.Refresh();

        lobby.IsScanning.ShouldBeFalse();
        lobby.State.ShouldBe(LanLobbyState.Hosting);
    }

    [Fact]
    public void A_port_in_use_is_reported_and_the_lobby_keeps_browsing()
    {
        _network.BusyPorts.Add(7777);
        var lobby = Lobby();

        lobby.Host(Player("Alice"));

        lobby.State.ShouldBe(LanLobbyState.Browsing);
        lobby.Error.ShouldNotBeNull().ShouldContain("Port 7777 is already in use");
        lobby.IsScanning.ShouldBeTrue();
        _network.Announcers.ShouldBeEmpty();
    }

    [Fact]
    public void A_game_that_cannot_be_announced_is_still_hosted()
    {
        _network.AnnounceFails = true;
        var lobby = Lobby();

        lobby.Host(Player("Alice"));

        lobby.State.ShouldBe(LanLobbyState.Hosting);
        lobby.Notice.ShouldNotBeNull().ShouldContain("join with your address");
        lobby.Error.ShouldBeNull();
    }

    [Fact]
    public void A_host_and_a_guest_start_the_same_match()
    {
        var host = Lobby();
        var guest = Lobby();
        host.Host(Player("Alice"));
        guest.Join(Local, Player("Bob", Shipped.LastDeck));
        guest.State.ShouldBe(LanLobbyState.Joining);
        guest.JoinTarget.ShouldBe(Local);

        PollAll(host, guest);

        host.State.ShouldBe(LanLobbyState.Started);
        guest.State.ShouldBe(LanLobbyState.Started);
        var hostMatch = host.Match.ShouldNotBeNull();
        var guestMatch = guest.Match.ShouldNotBeNull();
        _owned.Add(hostMatch);
        _owned.Add(guestMatch);
        hostMatch.IsHost.ShouldBeTrue();
        guestMatch.IsHost.ShouldBeFalse();
        guestMatch.Session.Checksum().ShouldBe(hostMatch.Session.Checksum());
        guestMatch.Session.State.Player(Seat.One).Name.ShouldBe("Alice");
        _network.Announcers.ShouldAllBe(a => a.IsDisposed);
        _network.Scanners.ShouldAllBe(s => s.IsDisposed);
    }

    [Fact]
    public void Disposing_the_lobby_keeps_the_started_match_open()
    {
        var host = Lobby();
        var guest = Lobby();
        host.Host(Player("Alice"));
        guest.Join(Local, Player("Bob"));
        PollAll(host, guest);

        host.Dispose();
        guest.Dispose();

        var hostMatch = host.Match.ShouldNotBeNull();
        var guestMatch = guest.Match.ShouldNotBeNull();
        _owned.Add(hostMatch);
        _owned.Add(guestMatch);
        LanRig.Pump(hostMatch, guestMatch);
        hostMatch.CloseReason.ShouldBeNull();
        guestMatch.CloseReason.ShouldBeNull();
    }

    [Fact]
    public void Joining_where_nobody_hosts_reports_it()
    {
        var lobby = Lobby();

        lobby.Join(new LanAddress("10.0.0.9", 7777), Player("Bob"));
        lobby.Poll().ShouldBeTrue();

        lobby.State.ShouldBe(LanLobbyState.Browsing);
        lobby.Error.ShouldBe("No game is hosted at 10.0.0.9:7777.");
        lobby.IsScanning.ShouldBeTrue();
    }

    [Fact]
    public void An_unknown_host_name_is_reported()
    {
        _network.ConnectFailure = new SocketException((int)SocketError.HostNotFound);
        var lobby = Lobby();

        lobby.Join(new LanAddress("alice-pc", 7777), Player("Bob"));
        lobby.Poll();

        lobby.Error.ShouldBe("The address \"alice-pc\" could not be found.");
    }

    [Fact]
    public void A_connection_that_times_out_points_at_the_firewall()
    {
        _network.HoldConnects = true;
        var lobby = Lobby();
        lobby.Join(Local, Player("Bob"));

        _network.PendingConnect.ShouldNotBeNull().SetCanceled(TestContext.Current.CancellationToken);
        lobby.Poll();

        lobby.State.ShouldBe(LanLobbyState.Browsing);
        lobby.Error.ShouldNotBeNull().ShouldContain("firewall");
    }

    [Fact]
    public void A_rejected_guest_is_told_why_and_the_host_keeps_waiting()
    {
        var host = Lobby();
        var guest = Lobby(LanRig.Identity with { GameVersion = "other" });
        host.Host(Player("Alice"));
        guest.Join(Local, Player("Bob"));

        PollAll(host, guest);

        guest.State.ShouldBe(LanLobbyState.Browsing);
        guest.Error.ShouldNotBeNull().ShouldContain("Version mismatch");
        host.State.ShouldBe(LanLobbyState.Hosting);
        host.Notice.ShouldNotBeNull().ShouldContain("Version mismatch");

        var retry = Lobby();
        retry.Join(Local, Player("Carol"));
        PollAll(host, retry);
        host.State.ShouldBe(LanLobbyState.Started);
    }

    [Fact]
    public void Cancelling_hosting_closes_the_port_and_resumes_listening_for_games()
    {
        var lobby = Lobby();
        lobby.Host(Player("Alice"));

        lobby.Cancel();

        lobby.State.ShouldBe(LanLobbyState.Browsing);
        lobby.HostPort.ShouldBeNull();
        lobby.IsScanning.ShouldBeTrue();
        _network.Announcers.ShouldHaveSingleItem().IsDisposed.ShouldBeTrue();
        lobby.Host(Player("Alice"));
        lobby.State.ShouldBe(LanLobbyState.Hosting);
    }

    [Fact]
    public void Cancelling_a_join_stops_connecting_and_closes_a_late_connection()
    {
        _network.HoldConnects = true;
        var lobby = Lobby();
        lobby.Join(Local, Player("Bob"));
        var token = _network.LastConnectToken;

        lobby.Cancel();

        lobby.State.ShouldBe(LanLobbyState.Browsing);
        lobby.Error.ShouldBeNull();
        token.IsCancellationRequested.ShouldBeTrue();
        var (late, _) = InMemoryConnection.CreatePair();
        _network.PendingConnect.ShouldNotBeNull().SetResult(late);
        SpinWait.SpinUntil(() => late.IsClosed, TimeSpan.FromSeconds(5)).ShouldBeTrue();
    }

    [Fact]
    public void A_lobby_busy_hosting_or_joining_cannot_start_again()
    {
        _network.HoldConnects = true;
        var lobby = Lobby();
        lobby.Join(Local, Player("Bob"));

        Should.Throw<InvalidOperationException>(() => lobby.Host(Player("Bob")));
        Should.Throw<InvalidOperationException>(() => lobby.Join(Local, Player("Bob")));
    }

    [Fact]
    public void A_new_attempt_clears_the_previous_error()
    {
        var lobby = Lobby();
        lobby.Join(Local, Player("Bob"));
        lobby.Poll();
        lobby.Error.ShouldNotBeNull();

        lobby.Host(Player("Bob"));

        lobby.Error.ShouldBeNull();
    }

    private LanLobby Lobby(LanIdentity? identity = null)
    {
        var lobby = new LanLobby(Shipped.Data, identity ?? LanRig.Identity, _network, () => 11);
        _owned.Add(lobby);
        return lobby;
    }

    private static LanPlayer Player(string name, DeckDefinition? deck = null) => new(name, deck ?? Shipped.FirstDeck);

    private static LanBeacon Beacon(string id, string host) =>
        new(id, "Room", host, NetProtocol.DefaultGamePort, NetProtocol.Version, LanRig.Identity.GameVersion);

    private static void PollAll(params LanLobby[] lobbies)
    {
        for (var i = 0; i < 4; i++)
        {
            foreach (var lobby in lobbies)
            {
                lobby.Poll();
            }
        }
    }
}
