using System.Net;
using System.Net.Sockets;
using Gwota.Net.Lan;
using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Transport;

public class TcpConnectionTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Lines_cross_a_loopback_connection_both_ways()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var client = await TcpConnection.ConnectAsync("127.0.0.1", listener.Port, Wait, TestContext.Current.CancellationToken);
        using var server = await AcceptAsync(listener);

        client.Send("hello");
        server.Send("{\"type\":\"rematch\"}");
        client.Send("café ✓");

        (await ReceiveAsync(server)).ShouldBe("hello");
        (await ReceiveAsync(server)).ShouldBe("café ✓");
        (await ReceiveAsync(client)).ShouldBe("{\"type\":\"rematch\"}");
    }

    [Fact]
    public async Task Closing_one_side_is_seen_by_the_other()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var client = await TcpConnection.ConnectAsync("127.0.0.1", listener.Port, Wait, TestContext.Current.CancellationToken);
        using var server = await AcceptAsync(listener);

        client.Close("bye");

        client.CloseReason.ShouldBe("bye");
        await Until(() => server.IsClosed);
    }

    [Fact]
    public async Task A_line_sent_just_before_closing_still_arrives()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var client = await TcpConnection.ConnectAsync("127.0.0.1", listener.Port, Wait, TestContext.Current.CancellationToken);
        using var server = await AcceptAsync(listener);

        client.Send("goodbye");
        client.Close("left");

        (await ReceiveAsync(server)).ShouldBe("goodbye");
    }

    [Fact]
    public async Task A_lan_match_over_tcp_reports_a_leaving_opponent()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var host = new LanHost(listener, Shipped.Data, LanRig.Identity, new LanPlayer("Alice", Shipped.FirstDeck));
        var connection = await TcpConnection.ConnectAsync("127.0.0.1", listener.Port, Wait, TestContext.Current.CancellationToken);
        using var guest = new LanGuest(connection, Shipped.Data, LanRig.Identity, new LanPlayer("Bob", Shipped.LastDeck));
        await Until(() =>
        {
            host.Poll();
            guest.Poll();
            return host.Match is not null && guest.Match is not null;
        });

        host.Match!.Leave();
        await Until(() =>
        {
            guest.Match!.Poll();
            return guest.Match.CloseReason is not null;
        });

        guest.Match!.CloseReason.ShouldBe(LanMatch.OpponentLeftReason);
    }

    [Fact]
    public async Task Heartbeats_keep_a_quiet_connection_open()
    {
        var raw = new TcpListener(IPAddress.Loopback, 0);
        raw.Start();
        try
        {
            using var clientSocket = new TcpClient();
            await clientSocket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)raw.LocalEndpoint).Port, TestContext.Current.CancellationToken);
            using var serverSocket = await raw.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            using var client = TcpConnection.Wrap(clientSocket, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(400));
            using var server = TcpConnection.Wrap(serverSocket, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(400));

            await Task.Delay(1000, TestContext.Current.CancellationToken);

            client.IsClosed.ShouldBeFalse();
            server.IsClosed.ShouldBeFalse();
            client.TryReceive(out _).ShouldBeFalse();
        }
        finally
        {
            raw.Stop();
        }
    }
    [Fact]
    public async Task A_silent_peer_times_out()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var raw = new TcpClient();
        await raw.ConnectAsync("127.0.0.1", listener.Port, TestContext.Current.CancellationToken);
        using var connection = TcpConnection.Wrap(raw, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));

        // Nothing is accepted on the other side, so nothing is ever received.
        await Until(() => connection.IsClosed);

        connection.CloseReason.ShouldBe("The connection timed out.");
    }

    [Fact]
    public void Lines_cannot_contain_a_newline()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var raw = new TcpClient();
        raw.Connect("127.0.0.1", listener.Port);
        using var connection = TcpConnection.Wrap(raw);

        Should.Throw<ArgumentException>(() => connection.Send("a\nb"));
    }

    [Fact]
    public async Task Connecting_to_a_closed_port_fails()
    {
        int port;
        using (var probe = new TcpConnectionListener(0, IPAddress.Loopback))
        {
            port = probe.Port;
        }

        await Should.ThrowAsync<Exception>(() => TcpConnection.ConnectAsync("127.0.0.1", port, Wait, TestContext.Current.CancellationToken));
    }

    private static async Task<IConnection> AcceptAsync(TcpConnectionListener listener)
    {
        IConnection? accepted = null;
        await Until(() => (accepted = listener.TryAccept()) is not null);
        return accepted!;
    }

    private static async Task<string> ReceiveAsync(IConnection connection)
    {
        string? line = null;
        await Until(() => connection.TryReceive(out line));
        return line!;
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}