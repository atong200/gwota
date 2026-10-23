using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Transport;

public class InMemoryConnectionTests
{
    [Fact]
    public void Lines_arrive_in_order_on_the_other_end()
    {
        var (a, b) = InMemoryConnection.CreatePair();

        a.Send("one");
        a.Send("two");

        b.TryReceive(out var first).ShouldBeTrue();
        b.TryReceive(out var second).ShouldBeTrue();
        (first, second).ShouldBe(("one", "two"));
        b.TryReceive(out _).ShouldBeFalse();
        a.TryReceive(out _).ShouldBeFalse();
    }

    [Fact]
    public void Closing_one_end_closes_both_but_keeps_lines_already_sent()
    {
        var (a, b) = InMemoryConnection.CreatePair();
        a.Send("bye");

        a.Close("done");
        a.Send("lost");

        a.CloseReason.ShouldBe("done");
        b.IsClosed.ShouldBeTrue();
        b.TryReceive(out var line).ShouldBeTrue();
        line.ShouldBe("bye");
        b.TryReceive(out _).ShouldBeFalse();
    }

    [Fact]
    public void The_listener_hands_out_the_server_end()
    {
        var listener = new InMemoryListener();
        listener.TryAccept().ShouldBeNull();

        var client = listener.Connect();
        var server = listener.TryAccept().ShouldNotBeNull();
        client.Send("hi");

        server.TryReceive(out var line).ShouldBeTrue();
        line.ShouldBe("hi");
    }

    [Fact]
    public void A_disposed_listener_refuses_connections()
    {
        var listener = new InMemoryListener();
        listener.Dispose();

        listener.Connect().IsClosed.ShouldBeTrue();
        listener.TryAccept().ShouldBeNull();
    }
}