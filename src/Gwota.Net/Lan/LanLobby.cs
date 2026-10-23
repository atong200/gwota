using System.Net.Sockets;
using Gwota.Net.Discovery;
using Gwota.Net.Transport;

namespace Gwota.Net.Lan;

public enum LanLobbyState
{
    Browsing,
    Hosting,
    Joining,
    Started,
}

/// <summary>
/// The LAN screen's state: lists announced games while browsing, hosts or joins one, and ends with a started
/// <see cref="Match"/> that the caller then owns. Poll it every frame.
/// </summary>
public sealed class LanLobby : IDisposable
{
    private readonly GameData _data;
    private readonly LanIdentity _identity;
    private readonly ILanNetwork _network;
    private readonly Func<ulong>? _seeds;
    private ILanScanner? _scanner;
    private ILanAnnouncer? _announcer;
    private LanHost? _host;
    private string? _seenRejection;
    private LanGuest? _guest;
    private LanPlayer? _joiner;
    private Task<IConnection>? _connecting;
    private CancellationTokenSource? _cancelConnect;
    private bool _disposed;

    public LanLobby(GameData data, LanIdentity identity, ILanNetwork network, Func<ulong>? seeds = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(network);
        _data = data;
        _identity = identity;
        _network = network;
        _seeds = seeds;
        Directory = new LanGameDirectory(identity.GameVersion) { OwnInstanceId = InstanceId };
        StartScanning();
    }

    public string InstanceId { get; } = Guid.NewGuid().ToString("N");

    public LanGameDirectory Directory { get; }

    public LanLobbyState State { get; private set; }

    /// <summary>False when announced games cannot be heard (port taken or blocked); joining by address still works.</summary>
    public bool IsScanning => _scanner?.IsListening == true;

    /// <summary>The port guests connect to while hosting.</summary>
    public int? HostPort => State == LanLobbyState.Hosting ? _host?.Port : null;

    public LanAddress? JoinTarget { get; private set; }

    /// <summary>Why the last host or join attempt failed.</summary>
    public string? Error { get; private set; }

    /// <summary>Something worth showing that did not stop hosting (a guest turned away, no announcement).</summary>
    public string? Notice { get; private set; }

    public LanMatch? Match { get; private set; }

    public void Host(LanPlayer player, int port = NetProtocol.DefaultGamePort, string? roomName = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        RequireBrowsing();
        Error = Notice = null;

        IConnectionListener listener;
        try
        {
            listener = _network.Listen(port);
        }
        catch (SocketException ex)
        {
            Error = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"Port {port} is already in use, maybe by another game on this computer."
                : $"Could not open port {port}: {ex.Message}";
            return;
        }

        // Another game on this computer then gets the beacons sent to loopback.
        StopScanning();
        var host = player.Normalized();
        _host = new LanHost(listener, _data, _identity, host, _seeds);
        _seenRejection = null;
        try
        {
            _announcer = _network.Announce(new LanBeacon(
                InstanceId,
                NetworkSettings.RoomNameOrDefault(roomName, host.Name),
                host.Name,
                _host.Port,
                NetProtocol.Version,
                _identity.GameVersion));
        }
        catch (SocketException)
        {
            Notice = "Your game could not be announced on the network. Others can still join with your address.";
        }

        State = LanLobbyState.Hosting;
    }

    public void Join(LanAddress address, LanPlayer player)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(player);
        RequireBrowsing();
        Error = Notice = null;
        JoinTarget = address;
        _joiner = player;
        _cancelConnect = new CancellationTokenSource();
        _connecting = _network.ConnectAsync(address, _cancelConnect.Token);
        State = LanLobbyState.Joining;
    }

    /// <summary>Empties the game list and, while browsing, retries listening if it had failed.</summary>
    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.Clear();
        if (State == LanLobbyState.Browsing)
        {
            StartScanning();
        }
    }

    /// <summary>Stops hosting or joining and goes back to browsing.</summary>
    public void Cancel()
    {
        if (State is LanLobbyState.Hosting or LanLobbyState.Joining)
        {
            Teardown();
            State = LanLobbyState.Browsing;
            StartScanning();
        }
    }

    /// <summary>Advances discovery, hosting and joining; returns true when anything shown on screen changed.</summary>
    public bool Poll()
    {
        if (_disposed)
        {
            return false;
        }

        var before = (State, Error, Notice);
        var gamesChanged = _scanner?.Poll() ?? Directory.Prune();
        switch (State)
        {
            case LanLobbyState.Hosting:
                PollHosting();
                break;
            case LanLobbyState.Joining:
                PollJoining();
                break;
        }

        return gamesChanged || before != (State, Error, Notice);
    }

    /// <summary>Stops everything except a started match, which belongs to the caller.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Teardown();
        StopScanning();
    }

    private void PollHosting()
    {
        var host = _host!;
        _announcer?.Poll();
        host.Poll();
        if (host.LastRejection is { } rejection && !ReferenceEquals(rejection, _seenRejection))
        {
            _seenRejection = rejection;
            Notice = $"A player could not join: {rejection}";
        }

        if (host.Match is { } match)
        {
            Start(match);
        }
    }

    private void PollJoining()
    {
        if (_connecting is { IsCompleted: true } connecting)
        {
            _connecting = null;
            if (!connecting.IsCompletedSuccessfully)
            {
                Fail(ConnectError(connecting, JoinTarget!));
                return;
            }

            _guest = new LanGuest(connecting.Result, _data, _identity, _joiner!);
        }

        if (_guest is null)
        {
            return;
        }

        _guest.Poll();
        if (_guest.Match is { } match)
        {
            Start(match);
        }
        else if (_guest.Error is { } error)
        {
            Fail(error);
        }
    }

    private void Start(LanMatch match)
    {
        Match = match;
        Teardown();
        StopScanning();
        State = LanLobbyState.Started;
    }

    private void Fail(string error)
    {
        Teardown();
        Error = error;
        State = LanLobbyState.Browsing;
        StartScanning();
    }

    /// <summary>Releases the host or join attempt; a connection still being opened is closed once it opens.</summary>
    private void Teardown()
    {
        _announcer?.Dispose();
        _announcer = null;
        _host?.Dispose();
        _host = null;
        _guest?.Dispose();
        _guest = null;
        if (_connecting is { } pending)
        {
            _cancelConnect?.Cancel();
            pending.ContinueWith(
                task => task.Result.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
            _connecting = null;
        }

        _cancelConnect?.Dispose();
        _cancelConnect = null;
    }

    private void StartScanning()
    {
        if (_scanner is null && !_disposed)
        {
            try
            {
                _scanner = _network.Scan(Directory);
            }
            catch (SocketException)
            {
                _scanner = null;
            }
        }
    }

    private void StopScanning()
    {
        _scanner?.Dispose();
        _scanner = null;
    }

    private void RequireBrowsing()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != LanLobbyState.Browsing)
        {
            throw new InvalidOperationException($"Cannot start while {State}.");
        }
    }

    private static string ConnectError(Task connecting, LanAddress address)
    {
        var timedOut = $"No answer from {address}. Check the address, and that the host's firewall lets Gwota through.";
        if (connecting.IsCanceled)
        {
            return timedOut;
        }

        return connecting.Exception?.GetBaseException() switch
        {
            SocketException { SocketErrorCode: SocketError.ConnectionRefused } => $"No game is hosted at {address}.",
            SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain } =>
                $"The address \"{address.Host}\" could not be found.",
            SocketException { SocketErrorCode: SocketError.TimedOut } or OperationCanceledException => timedOut,
            { } ex => $"Could not connect to {address}: {ex.Message}",
            null => $"Could not connect to {address}.",
        };
    }
}
