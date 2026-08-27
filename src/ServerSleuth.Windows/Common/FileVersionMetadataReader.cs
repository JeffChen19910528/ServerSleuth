using System.Diagnostics;

namespace ServerSleuth.Windows.Common;

public sealed class FileVersionMetadataReader : IFileVersionMetadataReader
{
    public FileVersionMetadata? TryRead(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new FileVersionMetadata
            {
                FileVersion = info.FileVersion,
                ProductVersion = info.ProductVersion,
                CompanyName = info.CompanyName,
                ProductName = info.ProductName
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
