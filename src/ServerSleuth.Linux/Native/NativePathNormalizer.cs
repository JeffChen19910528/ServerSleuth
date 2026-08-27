namespace ServerSleuth.Linux.Native;

/// <summary>
/// Pure, filesystem-free path normalization for RPATH/RUNPATH entries — see skill.md
/// (Phase 6F) §20 ("never resolve `../..` outside a bounded search root"). `$ORIGIN` is expanded
/// to the importing binary's own directory (the one legitimate, bounded dynamic-loader token
/// this resolver supports); `..` segments are collapsed lexically and can never pop past the
/// filesystem root, so a maliciously-crafted RPATH cannot walk outside `/` no matter how many
/// `../` segments it contains. No filesystem access occurs during normalization — symlinks are
/// never followed here.
/// </summary>
internal static class NativePathNormalizer
{
    public static string ExpandOrigin(string entry, string? importingBinaryDirectory) =>
        importingBinaryDirectory is not null ? entry.Replace("$ORIGIN", importingBinaryDirectory, StringComparison.Ordinal) : entry;

    public static string Normalize(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case ".":
                    continue;
                case "..":
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }
                    // else: already at root — a ".." here is simply dropped, never allowed to
                    // escape above "/".
                    continue;
                default:
                    stack.Add(segment);
                    break;
            }
        }

        return "/" + string.Join('/', stack);
    }
}
