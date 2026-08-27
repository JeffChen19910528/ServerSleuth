using System.Net;

namespace ServerSleuth.Linux.Networking;

/// <summary>
/// Parses `/proc/net/{tcp,tcp6,udp,udp6}`'s fixed-column text format. Local address/port are
/// hex-encoded in the kernel's native byte order (each 32-bit word byte-reversed, IPv6 words
/// kept in order) — see proc(5). Malformed rows are skipped, never guessed at.
/// </summary>
public static class ProcNetParser
{
    public const string TcpListenState = "0A";

    public static IReadOnlyList<ProcNetRow> Parse(string text)
    {
        var rows = new List<ProcNetRow>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // First line is the column header — skip it.
        for (var i = 1; i < lines.Length; i++)
        {
            var columns = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 10)
            {
                continue;
            }

            var localAddressColumn = columns[1]; // "0100007F:0050"
            var stateColumn = columns[3];
            var inodeColumn = columns[9];

            var addressParts = localAddressColumn.Split(':');
            if (addressParts.Length != 2)
            {
                continue;
            }

            string address;
            try
            {
                address = ParseAddress(addressParts[0]);
            }
            catch (FormatException)
            {
                continue;
            }

            if (!int.TryParse(addressParts[1], System.Globalization.NumberStyles.HexNumber, null, out var port))
            {
                continue;
            }

            rows.Add(new ProcNetRow { LocalAddress = address, LocalPort = port, StateHex = stateColumn, Inode = inodeColumn });
        }

        return rows;
    }

    private static string ParseAddress(string hex)
    {
        var raw = Convert.FromHexString(hex);

        if (raw.Length == 4)
        {
            Array.Reverse(raw);
            return new IPAddress(raw).ToString();
        }

        if (raw.Length == 16)
        {
            var result = new byte[16];
            for (var word = 0; word < 4; word++)
            {
                for (var b = 0; b < 4; b++)
                {
                    result[(word * 4) + b] = raw[(word * 4) + (3 - b)];
                }
            }

            return new IPAddress(result).ToString();
        }

        throw new FormatException($"Unexpected address length {raw.Length} for hex '{hex}'.");
    }
}
