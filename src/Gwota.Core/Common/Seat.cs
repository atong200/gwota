namespace Gwota.Core.Common;

/// <summary>One of the two players sitting at the table.</summary>
public enum Seat
{
    One = 0,
    Two = 1,
}

public static class SeatExtensions
{
    public static Seat Opponent(this Seat seat) => seat == Seat.One ? Seat.Two : Seat.One;

    public static int Index(this Seat seat) => (int)seat;
}
