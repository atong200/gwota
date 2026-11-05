using Godot;
using Gwota.Core.Common;
using Gwota.Core.Match;
using Gwota.Core.Session;
using Timer = Godot.Timer;

namespace Gwota.Game.Ui;

/// <summary>How <see cref="MatchDriver"/> hands applied commands to the screen.</summary>
public interface IMatchDriverHost
{
    /// <summary>True while card moves are animated; commands wait until they end.</summary>
    bool IsAnimating { get; }

    /// <summary>Applies a command during play and animates the cards it moved.</summary>
    void Execute(Func<CommandResult?> command);

    /// <summary>Applies a draft command and animates the redraw.</summary>
    void ExecuteDraft(Func<CommandResult?> command);

    void Refresh();

    void ReportError(string message);

    /// <summary>The remote match started a rematch: a new screen takes over.</summary>
    void StartRematch();
}

/// <summary>
/// Moves the match forward: applies the local player's commands (or sends them to a remote match, which applies them
/// once agreed), paces the controllers (AI) with a timer and polls the remote match every frame.
/// </summary>
public sealed partial class MatchDriver : Node
{
    public const double ControllerDelay = 0.7;
    public const double ControllerDelayAfterRound = 2.2;

    private readonly MatchSession _session;
    private readonly IMatchLink? _link;
    private readonly Seat _local;
    private readonly IMatchDriverHost _host;
    private readonly Timer _timer = new() { OneShot = true };
    private double _nextDelay = ControllerDelay;
    private bool _rematchStarted;
    private (bool CanSend, bool HasPending, string? CloseReason, RematchStatus Rematch) _linkState;

    public MatchDriver(MatchSession session, IMatchLink? link, Seat local, IMatchDriverHost host)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _link = link;
        _local = local;
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>Raised before a local command is applied or sent.</summary>
    public event Action? Sending;

    /// <summary>True when the player here may send a command now.</summary>
    public bool LocalCanAct => _link is null
        ? _session.HumanToAct == _local
        : _link.CanSend && _session.CanAct(_local);

    public override void _Ready()
    {
        AddChild(_timer);
        _timer.Timeout += OnTimer;
        SetProcess(_link is not null);
    }

    /// <summary>Reads the link, applies agreed commands between animations and hands over to a new screen on rematch.</summary>
    public override void _Process(double delta)
    {
        if (_link is not { } link || _rematchStarted)
        {
            return;
        }

        link.Poll();
        if (link.Session != _session)
        {
            _rematchStarted = true;
            _host.StartRematch();
            return;
        }

        if (!_host.IsAnimating && link.HasPending)
        {
            ApplyLinked(link);
        }

        var state = (link.CanSend, link.HasPending, link.CloseReason, link.Rematch);
        if (state != _linkState)
        {
            _linkState = state;
            _host.Refresh();
        }
    }

    /// <summary>Starts the controller timer when a controller is due to act.</summary>
    public void ScheduleController()
    {
        if (_session.IsControllerTurn && _timer.IsStopped())
        {
            _timer.Start(_nextDelay);
            _nextDelay = ControllerDelay;
        }
    }

    /// <summary>Gives the next controller move more time (e.g. while the round banner shows).</summary>
    public void DelayNextController() => _nextDelay = ControllerDelayAfterRound;

    public void SetPaused(bool paused) => _timer.Paused = paused;

    public void StopController() => _timer.Stop();

    /// <summary>Plays a command for the player here, unless an animation is running.</summary>
    public void Submit(GameCommand command)
    {
        if (!_host.IsAnimating)
        {
            Dispatch(command, _host.Execute);
        }
    }

    public void SubmitDraft(FinishDraftCommand command) => Dispatch(command, _host.ExecuteDraft);

    /// <summary>Applies a command here at once, or sends it to the remote match (it is applied when it comes back agreed).</summary>
    public void SubmitNow(GameCommand command)
    {
        if (_link is not null)
        {
            Sending?.Invoke();
            _link.Send(command);
            return;
        }

        Apply(command);
    }

    public void RequestRematch()
    {
        _link?.RequestRematch();
        _host.Refresh();
    }

    private void Dispatch(GameCommand command, Action<Func<CommandResult?>> run)
    {
        if (_link is not null)
        {
            if (LocalCanAct)
            {
                SubmitNow(command);
                _host.Refresh();
            }

            return;
        }

        run(() => Apply(command));
    }

    private CommandResult? Apply(GameCommand command)
    {
        Sending?.Invoke();
        return Report(_session.Submit(command));
    }

    /// <summary>Applies the next agreed command of the remote match, animated like a local one.</summary>
    private void ApplyLinked(IMatchLink link)
    {
        if (_session.State.Phase == MatchPhase.Draft)
        {
            _host.ExecuteDraft(() => Report(link.ApplyNext()));
            return;
        }

        _host.Execute(() => Report(link.ApplyNext()));
    }

    private CommandResult? Report(CommandResult? result)
    {
        if (result is { IsSuccess: false })
        {
            _host.ReportError(result.Error ?? "The command failed.");
        }

        return result;
    }

    private void OnTimer()
    {
        if (_host.IsAnimating)
        {
            _timer.Start(ControllerDelay);
            return;
        }

        try
        {
            _host.Execute(_session.StepController);
        }
        catch (InvalidOperationException ex)
        {
            GD.PushError(ex.ToString());
            _host.ReportError($"AI error: {ex.Message}");
        }
    }
}
