namespace Gwota.Core.Cards;

/// <summary>
/// A copy of a card in a match; <see cref="Id"/> is unique per match. <see cref="Owner"/> decides whose discard
/// pile it goes to (a spy keeps its owner while on the opponent's side).
/// </summary>
public sealed record CardInstance(int Id, CardDefinition Definition, Seat Owner)
{
    /// <summary>Which copy of its card this is in its owner's deck: 0 for the first, 1 for the second, and so on.</summary>
    public int Copy { get; init; }

    public override string ToString() => $"{Definition.Name}#{Id}";
}
