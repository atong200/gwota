using System.Net;
using Gwota.Net.Discovery;

namespace Gwota.Net.Tests.Discovery;

public class LanGameDirectoryTests
{
    private static readonly IPAddress Lan = IPAddress.Parse("192.168.1.20");

    private readonly ManualTime _time = new();
    private readonly LanGameDirectory _directory;

    public LanGameDirectoryTests() => _directory = new LanGameDirectory("1.0", _time);

    [Fact]
    public void Heard_games_are_listed_by_room_name()
    {
        _directory.Observe(Beacon("b", "Zed"), Lan).ShouldBeTrue();
        _directory.Observe(Beacon("a", "amy"), Lan).ShouldBeTrue();

        _directory.Games.Select(g => g.Beacon.RoomName).ShouldBe(["amy", "Zed"]);
        _directory.Games[0].Endpoint.ShouldBe("192.168.1.20:7777");
    }

    [Fact]
    public void Clearing_forgets_games_until_heard_again()
    {
        _directory.Observe(Beacon("a"), Lan);

        _directory.Clear();

        _directory.Games.ShouldBeEmpty();
        _directory.Observe(Beacon("a"), Lan).ShouldBeTrue();
    }

    [Fact]
    public void Hearing_the_same_beacon_again_changes_nothing()
    {
        _directory.Observe(Beacon("a"), Lan);

        _directory.Observe(Beacon("a"), Lan).ShouldBeFalse();
        _directory.Games.Count.ShouldBe(1);
    }

    [Fact]
    public void Games_expire_when_their_beacon_stops()
    {
        _directory.Observe(Beacon("a"), Lan);
        _time.Advance(TimeSpan.FromSeconds(2));
        _directory.Observe(Beacon("b"), Lan);
        _time.Advance(TimeSpan.FromSeconds(2));

        _directory.Prune().ShouldBeTrue();
        _directory.Games.Single().Beacon.InstanceId.ShouldBe("b");
        _directory.Prune().ShouldBeFalse();
    }

    [Fact]
    public void The_own_beacon_is_ignored()
    {
        _directory.OwnInstanceId = "me";

        _directory.Observe(Beacon("me"), Lan).ShouldBeFalse();
        _directory.Games.ShouldBeEmpty();
    }

    [Fact]
    public void A_lan_address_is_preferred_over_loopback()
    {
        _directory.Observe(Beacon("a"), Lan);
        _directory.Observe(Beacon("a"), IPAddress.Loopback);
        _directory.Games.Single().Address.ShouldBe(Lan);

        var other = new LanGameDirectory("1.0", _time);
        other.Observe(Beacon("a"), IPAddress.Loopback);
        other.Observe(Beacon("a"), Lan).ShouldBeTrue();
        other.Games.Single().Address.ShouldBe(Lan);
    }

    [Fact]
    public void Games_from_another_version_are_listed_as_incompatible()
    {
        _directory.Observe(Beacon("a") with { GameVersion = "0.9" }, Lan);
        _directory.Observe(Beacon("b") with { Protocol = NetProtocol.Version + 1 }, Lan);
        _directory.Observe(Beacon("c"), Lan);

        _directory.Games.Select(g => g.IsCompatible).ShouldBe([false, false, true]);
    }

    private static LanBeacon Beacon(string id, string room = "Room") =>
        new(id, room, "Host", 7777, NetProtocol.Version, "1.0");
}