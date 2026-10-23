namespace Gwota.Game.Ui;

/// <summary>Navigation callbacks the match screen needs from the composition root.</summary>
public sealed record MatchScreenActions(Action ExitToMenu, Action Rematch, Action Quit);
