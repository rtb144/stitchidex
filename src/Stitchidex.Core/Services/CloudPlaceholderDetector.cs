// SPDX-License-Identifier: MIT
namespace Stitchidex.Core.Services;

/// <summary>
/// Detects Dropbox / OneDrive (Windows Cloud Files API) placeholders that are not hydrated locally.
/// Reading such a file's bytes triggers a download, so the scanner catalogs them from FileInfo/filename only.
/// Only attributes are read here (never file contents), which does not hydrate the placeholder.
/// </summary>
public static class CloudPlaceholderDetector
{
    public const int FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    public const int FILE_ATTRIBUTE_OFFLINE = 0x00001000;
    public const int FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00040000;
    public const int FILE_ATTRIBUTE_PINNED = 0x00080000;
    public const int FILE_ATTRIBUTE_UNPINNED = 0x00100000;
    public const int FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 0x00400000;

    public const string CloudOnlyBadge = "Cloud Only (Not Downloaded)";

    /// <summary>Test hook / platform override for reading raw attributes. Default: File.GetAttributes.</summary>
    public static Func<string, FileAttributes>? AttributeProvider { get; set; }

    /// <summary>True when the attributes describe a cloud-only (unhydrated) placeholder.</summary>
    public static bool IsCloudPlaceholder(FileAttributes attributes)
    {
        int a = (int)attributes;
        if ((a & FILE_ATTRIBUTE_PINNED) != 0) return false; // "Always keep on this device" / "Available offline"
        return (a & (FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS | FILE_ATTRIBUTE_RECALL_ON_OPEN | FILE_ATTRIBUTE_OFFLINE | FILE_ATTRIBUTE_UNPINNED)) != 0;
    }

    public static bool IsCloudPlaceholder(string path)
    {
        try { return IsCloudPlaceholder((AttributeProvider ?? File.GetAttributes)(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }
}
