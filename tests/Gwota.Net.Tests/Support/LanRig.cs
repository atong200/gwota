using Gwota.Core.Ai;
using Gwota.Net.Lan;
using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Support;

/// <summary>A host and a guest joined over an in-memory connection.</summary>
public static class LanRig
{
    public static LanIdentity Identity { get; } = LanIdentity.For(Shipped.Data, "test");

    public static (LanMatch Host, LanMatch Guest) Connect(ulong seed = 7, DeckDefinition? hostDeck = null, DeckDefinition? guestDeck = null)
    {
        var listener = new InMemoryListener();
        var seeds = new Queue<ulong>([seed, seed + 1, seed + 2, seed + 3]);
        using var host = new LanHost(listener, Shipped.Data, Identity, new LanPlayer("Alice", hostDeck ?? Shipped.FirstDeck), seeds.Dequeue);
        using var guest = new LanGuest(listener.Connect(), Shipped.Data, Identity, new LanPlayer("Bob", guestDeck ?? Shipped.LastDeck));
        host.Poll();
        guest.Poll();
        return (host.Match.ShouldNotBeNull(), guest.Match.ShouldNotBeNull());
    }

    /// <summary>Delivers messages and applies agreed commands until both sides are idle.</summary>
    public static void Pump(params LanMatch[] links)
    {
        for (var i = 0; i < 8; i++)
        {
            foreach (var link in links)
            {
                link.Poll();
                while (link.ApplyNext() is not null)
                {
                }
            }
        }
    }

    /// <summary>Lets an AI play both seats, each through its own link, until both matches end.</summary>
    public static void PlayOut(LanMatch host, LanMatch guest, int maxSteps = 5000)
    {
        var ai = new HeuristicAi();
        for (var step = 0; step < maxSteps; step++)
        {
            Pump(host, guest);
            if (host.Session.IsFinished && guest.Session.IsFinished)
            {
                return;
            }

            foreach (var link in new[] { host, guest })
            {
                if (link.CanSend && link.Session.CanAct(link.LocalSeat))
                {
                    link.Send(ai.Decide(link.Session.Engine, link.LocalSeat));
                }
            }
        }

        throw new InvalidOperationException("The match did not finish.");
    }
}