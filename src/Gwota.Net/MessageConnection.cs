using Gwota.Net.Transport;

namespace Gwota.Net;

/// <summary>Exchanges <see cref="NetMessage"/>s over a line connection.</summary>
public static class MessageConnection
{
    public static void Send(this IConnection connection, NetMessage message)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.Send(NetCodec.Encode(message));
    }

    /// <summary>Sends a last message (a goodbye or a refusal), then closes; the message is still delivered.</summary>
    public static void SendAndClose(this IConnection connection, NetMessage farewell, string reason)
    {
        connection.Send(farewell);
        connection.Close(reason);
    }

    /// <summary>Takes the next line, if any; <paramref name="message"/> is null when that line is not a valid message.</summary>
    public static bool TryReceiveMessage(this IConnection connection, out NetMessage? message)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.TryReceive(out var line))
        {
            message = NetCodec.Decode(line);
            return true;
        }

        message = null;
        return false;
    }
}
