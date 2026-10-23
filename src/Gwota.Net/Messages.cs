using System.Text.Json.Serialization;

namespace Gwota.Net;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(RejectMessage), "reject")]
[JsonDerivedType(typeof(StartMessage), "start")]
[JsonDerivedType(typeof(CommandMessage), "command")]
[JsonDerivedType(typeof(AppliedMessage), "applied")]
[JsonDerivedType(typeof(RefusedMessage), "refused")]
[JsonDerivedType(typeof(RematchMessage), "rematch")]
[JsonDerivedType(typeof(LeaveMessage), "leave")]
public abstract record NetMessage;

/// <summary>Guest to host: who is joining and with which deck.</summary>
public sealed record HelloMessage(int Protocol, string GameVersion, string Fingerprint, string Name, DeckDto Deck) : NetMessage;

/// <summary>Host to guest: the join was refused.</summary>
public sealed record RejectMessage(string Reason) : NetMessage;

/// <summary>Host to guest: a match begins. The host sits in <see cref="Seat.One"/>.</summary>
public sealed record StartMessage(ulong Seed, string HostName, DeckDto HostDeck, string GuestName, DeckDto GuestDeck) : NetMessage;

/// <summary>Guest to host: a command the guest wants applied.</summary>
public sealed record CommandMessage(CommandDto Command) : NetMessage;

/// <summary>Host to guest: the next command both sides apply, and the host's state checksum after it.</summary>
public sealed record AppliedMessage(int Sequence, CommandDto Command, ulong Checksum) : NetMessage;

/// <summary>Host to guest: the guest's last command was illegal and was dropped.</summary>
public sealed record RefusedMessage(string Reason) : NetMessage;

/// <summary>Either way: the sender wants to play again.</summary>
public sealed record RematchMessage : NetMessage;

/// <summary>Either way: the sender is leaving.</summary>
public sealed record LeaveMessage(LeaveReason Reason) : NetMessage;

public enum LeaveReason
{
    Quit,
    Desync,
}