using System.Net;
using System.Net.Sockets;
using Gwota.Net.Discovery;
using Gwota.Net.Lan;
using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Support;

/// <summary>An in-memory network: listeners by port, scripted connect results, recorded announcers and scanners.</summary>
public sealed class FakeLanNetwork : ILanNetwork
{
    private readonly Dictionary<int, InMemoryListener> _listeners = [];

    public HashSet<int> BusyPorts { get; } = [];

    public bool AnnounceFails { get; set; }

    public bool ScanFails { get; set; }

    /// <summary>When set, connects wait until <see cref="PendingConnect"/> is completed by the test.</summary>
    public bool HoldConnects { get; set; }

    public TaskCompletionSource<IConnection>? PendingConnect { get; private set; }

    public CancellationToken LastConnectToken { get; private set; }

    public Exception? ConnectFailure { get; set; }

    public List<FakeAnnouncer> Announcers { get; } = [];

    public List<FakeScanner> Scanners { get; } = [];

    public FakeScanner? ActiveScanner => Scanners.LastOrDefault(s => !s.IsDisposed);

    public IConnectionListener Listen(int port)
    {
        if (BusyPorts.Contains(port) || (_listeners.TryGetValue(port, out var open) && !open.IsDisposed))
        {
            throw new SocketException((int)SocketError.AddressAlreadyInUse);
        }

        var listener = new InMemoryListener();
        _listeners[port] = listener;
        return new PortListener(listener, port);
    }

    public Task<IConnection> ConnectAsync(LanAddress address, CancellationToken cancellation)
    {
        LastConnectToken = cancellation;
        if (HoldConnects)
        {
            PendingConnect = new TaskCompletionSource<IConnection>();
            return PendingConnect.Task;
        }

        if (ConnectFailure is { } failure)
        {
            return Task.FromException<IConnection>(failure);
        }

        return _listeners.TryGetValue(address.Port, out var listener) && !listener.IsDisposed
            ? Task.FromResult<IConnection>(listener.Connect())
            : Task.FromException<IConnection>(new SocketException((int)SocketError.ConnectionRefused));
    }

    public ILanAnnouncer Announce(LanBeacon beacon)
    {
        if (AnnounceFails)
        {
            throw new SocketException((int)SocketError.AccessDenied);
        }

        var announcer = new FakeAnnouncer(beacon);
        Announcers.Add(announcer);
        return announcer;
    }

    public ILanScanner Scan(LanGameDirectory directory)
    {
        if (ScanFails)
        {
            throw new SocketException((int)SocketError.AddressAlreadyInUse);
        }

        var scanner = new FakeScanner(directory);
        Scanners.Add(scanner);
        return scanner;
    }

    /// <summary>Reports the port it was opened on.</summary>
    private sealed class PortListener(InMemoryListener inner, int port) : IConnectionListener
    {
        public int Port => port;

        public IConnection? TryAccept() => inner.TryAccept();

        public void Dispose() => inner.Dispose();
    }

    public sealed class FakeAnnouncer(LanBeacon beacon) : ILanAnnouncer
    {
        public LanBeacon Beacon { get; } = beacon;

        public int Polls { get; private set; }

        public bool IsDisposed { get; private set; }

        public void Poll() => Polls++;

        public void Dispose() => IsDisposed = true;
    }

    public sealed class FakeScanner(LanGameDirectory directory) : ILanScanner
    {
        private readonly Queue<LanBeacon> _heard = [];

        public bool IsDisposed { get; private set; }

        public bool IsListening => !IsDisposed;

        public void Hear(LanBeacon beacon) => _heard.Enqueue(beacon);

        public bool Poll()
        {
            var changed = directory.Prune();
            while (_heard.TryDequeue(out var beacon))
            {
                changed |= directory.Observe(beacon, IPAddress.Loopback);
            }

            return changed;
        }

        public void Dispose() => IsDisposed = true;
    }
}
