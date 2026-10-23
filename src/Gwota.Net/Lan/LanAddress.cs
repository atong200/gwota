using System.Globalization;
using System.Net;

namespace Gwota.Net.Lan;

/// <summary>A typed host address: <c>host</c> or <c>host:port</c>, the port defaulting to <see cref="NetProtocol.DefaultGamePort"/>.</summary>
public sealed record LanAddress(string Host, int Port)
{
    public override string ToString() => Host.Contains(':', StringComparison.Ordinal) ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

    /// <summary>Parses an IPv4 address, a host name or a bracketed IPv6 address, each with an optional port.</summary>
    public static LanAddress? Parse(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return null;
        }

        string host;
        string? port = null;
        if (value.StartsWith('['))
        {
            var close = value.IndexOf(']', StringComparison.Ordinal);
            if (close < 0)
            {
                return null;
            }

            host = value[1..close];
            var rest = value[(close + 1)..];
            if (rest.Length > 0)
            {
                if (!rest.StartsWith(':'))
                {
                    return null;
                }

                port = rest[1..];
            }

            if (!IPAddress.TryParse(host, out _))
            {
                return null;
            }
        }
        else
        {
            var parts = value.Split(':');
            if (parts.Length > 2)
            {
                return IPAddress.TryParse(value, out _) ? new LanAddress(value, NetProtocol.DefaultGamePort) : null;
            }

            host = parts[0];
            port = parts.Length == 2 ? parts[1] : null;
            if (!IsHostName(host))
            {
                return null;
            }
        }

        if (port is null)
        {
            return new LanAddress(host, NetProtocol.DefaultGamePort);
        }

        return int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is > 0 and <= IPEndPoint.MaxPort
            ? new LanAddress(host, number)
            : null;
    }

    private static bool IsHostName(string host) =>
        host.Length is > 0 and <= 253
        && host.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-')
        && !host.StartsWith('.') && !host.StartsWith('-') && !host.EndsWith('-');
}
