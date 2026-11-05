using System.Net;
using System.Net.Sockets;
using System.Text;
using Gwota.Net.Lan;
using Gwota.Net.Tests.Support;
using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Transport;

public class TcpConnectionTests
{
    private static readonly TimeSpan Wait = Eventually.DefaultWait;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lines_cross_a_loopback_connection_both_ways()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var client = await ConnectAsync(listener);
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
        using var client = await ConnectAsync(listener);
        using var server = await AcceptAsync(listener);

        client.Close("bye");

        client.CloseReason.ShouldBe("bye");
        await Eventually.True(() => server.IsClosed);
    }

    [Fact]
    public async Task A_line_sent_just_before_closing_still_arrives()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var client = await ConnectAsync(listener);
        using var server = await AcceptAsync(listener);

        client.Send("goodbye");
        client.Close("left");

        (await ReceiveAsync(server)).ShouldBe("goodbye");
    }

    [Fact]
    public async Task Every_line_queued_before_closing_arrives_in_order()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var client = await ConnectAsync(listener);
        using var server = await AcceptAsync(listener);
        var sent = Enumerable.Range(0, 200).Select(i => $"line {i} {new string('x', i * 10)}").ToList();

        sent.ForEach(client.Send);
        client.Close("left");

        var received = new List<string>();
        foreach (var _ in sent)
        {
            received.Add(await ReceiveAsync(server));
        }

        received.ShouldBe(sent);
        await Eventually.True(() => server.IsClosed);
    }

    [Fact]
    public async Task Sending_after_closing_is_ignored()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var client = await ConnectAsync(listener);
        using var server = await AcceptAsync(listener);

        client.Close("left");
        client.Send("too late");

        await Eventually.True(() => server.IsClosed);
        server.TryReceive(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_lan_match_over_tcp_reports_a_leaving_opponent()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var host = new LanHost(listener, Shipped.Data, LanRig.Identity, new LanPlayer("Alice", Shipped.FirstDeck));
        var connection = await ConnectAsync(listener);
        using var guest = new LanGuest(connection, Shipped.Data, LanRig.Identity, new LanPlayer("Bob", Shipped.LastDeck));
        await Eventually.True(() =>
        {
            host.Poll();
            guest.Poll();
            return host.Match is not null && guest.Match is not null;
        });
        var hostMatch = host.Match.ShouldNotBeNull();
        var guestMatch = guest.Match.ShouldNotBeNull();

        hostMatch.Leave();
        await Eventually.True(() =>
        {
            guestMatch.Poll();
            return guestMatch.CloseReason is not null;
        });

        guestMatch.CloseReason.ShouldBe(LanMatch.OpponentLeftReason);
        guestMatch.OpponentLeft.ShouldBeTrue();
    }

    [Fact]
    public async Task Lines_split_or_joined_across_reads_are_framed_correctly()
    {
        var (raw, connection) = await RawPairAsync();
        using (raw)
        using (connection)
        {
            var stream = raw.GetStream();
            foreach (var chunk in new[] { "hel", "lo\nwor", "ld\n\nca", "f", "é\n" })
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(chunk), Token);
                await stream.FlushAsync(Token);
            }

            (await ReceiveAsync(connection)).ShouldBe("hello");
            (await ReceiveAsync(connection)).ShouldBe("world");
            (await ReceiveAsync(connection)).ShouldBe("café");
            connection.TryReceive(out _).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task A_line_at_the_size_limit_is_accepted()
    {
        var (raw, connection) = await RawPairAsync();
        using (raw)
        using (connection)
        {
            var line = new string('a', NetProtocol.MaxLineLength);
            await raw.GetStream().WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), Token);

            (await ReceiveAsync(connection)).ShouldBe(line);
            connection.IsClosed.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task An_oversized_line_closes_the_connection()
    {
        var (raw, connection) = await RawPairAsync();
        using (raw)
        using (connection)
        {
            await raw.GetStream().WriteAsync(Encoding.UTF8.GetBytes(new string('a', NetProtocol.MaxLineLength + 1)), Token);

            await Eventually.True(() => connection.IsClosed);
            connection.CloseReason.ShouldBe("The other side sent an oversized message.");
            connection.TryReceive(out _).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task A_heartbeat_goes_out_once_nothing_was_sent_for_the_heartbeat_interval()
    {
        var time = new ManualTime();
        var (raw, connection) = await RawPairAsync(TimeSpan.FromSeconds(2), TimeSpan.FromHours(1), time);
        using (raw)
        using (connection)
        {
            var stream = raw.GetStream();
            await Task.Delay(100, Token);
            raw.Available.ShouldBe(0);

            time.Advance(TimeSpan.FromSeconds(2));

            var received = new byte[1];
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
            timeout.CancelAfter(Wait);
            (await stream.ReadAsync(received, timeout.Token)).ShouldBe(1);
            received[0].ShouldBe((byte)'\n');
            connection.IsClosed.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Heartbeats_keep_a_quiet_connection_open()
    {
        var raw = new TcpListener(IPAddress.Loopback, 0);
        raw.Start();
        try
        {
            using var clientSocket = new TcpClient();
            await clientSocket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)raw.LocalEndpoint).Port, Token);
            using var serverSocket = await raw.AcceptTcpClientAsync(Token);
            using var client = TcpConnection.Wrap(clientSocket, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(400));
            using var server = TcpConnection.Wrap(serverSocket, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(400));

            // Real time on purpose: both sides' heartbeats must outpace each other's timeout.
            await Task.Delay(1000, Token);

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
        var time = new ManualTime();
        var (raw, connection) = await RawPairAsync(TimeSpan.FromHours(1), TimeSpan.FromSeconds(10), time);
        using (raw)
        using (connection)
        {
            await Task.Delay(100, Token);
            connection.IsClosed.ShouldBeFalse();

            time.Advance(TimeSpan.FromSeconds(11));

            await Eventually.True(() => connection.IsClosed);
            connection.CloseReason.ShouldBe("The connection timed out.");
        }
    }

    [Fact]
    public async Task Lines_cannot_contain_a_newline()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var connection = await ConnectAsync(listener);

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

        await Should.ThrowAsync<Exception>(() => TcpConnection.ConnectAsync("127.0.0.1", port, Wait, cancellation: Token));
    }

    [Fact]
    public async Task A_cancelled_connect_fails()
    {
        using var listener = new TcpConnectionListener(0, IPAddress.Loopback);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => TcpConnection.ConnectAsync("127.0.0.1", listener.Port, Wait, cancellation: cancelled.Token));
    }

    private static Task<TcpConnection> ConnectAsync(TcpConnectionListener listener) =>
        TcpConnection.ConnectAsync("127.0.0.1", listener.Port, Wait, cancellation: Token);

    /// <summary>A plain socket on one end, to write exact bytes, and a connection under test on the other.</summary>
    private static async Task<(TcpClient Raw, TcpConnection Connection)> RawPairAsync(
        TimeSpan? heartbeat = null, TimeSpan? timeout = null, TimeProvider? time = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var raw = new TcpClient { NoDelay = true };
            await raw.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, Token);
            var accepted = await listener.AcceptTcpClientAsync(Token);
            return (raw, TcpConnection.Wrap(accepted, heartbeat, timeout, time));
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<IConnection> AcceptAsync(TcpConnectionListener listener)
    {
        IConnection? accepted = null;
        await Eventually.True(() => (accepted = listener.TryAccept()) is not null);
        return accepted.ShouldNotBeNull();
    }

    private static async Task<string> ReceiveAsync(IConnection connection)
    {
        string? line = null;
        await Eventually.True(() => connection.TryReceive(out line));
        return line.ShouldNotBeNull();
    }
}
