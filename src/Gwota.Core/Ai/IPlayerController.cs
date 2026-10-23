namespace Gwota.Core.Ai;

/// <summary>Anything that can take decisions for a seat: the heuristic AI, a scripted bot, a remote player.</summary>
public interface IPlayerController
{
    GameCommand Decide(MatchEngine engine, Seat seat);
}
