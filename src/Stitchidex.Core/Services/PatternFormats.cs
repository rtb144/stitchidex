// SPDX-License-Identifier: MIT
namespace Stitchidex.Core.Services;

/// <summary>Working/source design formats (editable, not machine-sewable) vs machine stitch-out formats.</summary>
public static class PatternFormats
{
    public const string SourceEmbBadge = "Source (.EMB)";
    public const string EmbFamily = "Wilcom / Hatch Source Design (.emb)";
    static readonly HashSet<string> Working = new(StringComparer.OrdinalIgnoreCase) { ".emb" };
    public static readonly IReadOnlySet<string> StitchOutExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".vp3", ".pes", ".dst", ".jef", ".exp", ".xxx", ".hus", ".pec", ".sew", ".pcs", ".u01", ".dsb", ".dsz", ".tap", ".vip", ".vp4", ".shv", ".csd", ".ksm", ".pcm", ".pcq", ".pcd", ".jef+" };

    public static bool IsWorkingFile(string path) => Working.Contains(Path.GetExtension(path));
    public static bool IsStitchOutFile(string path) => StitchOutExtensions.Contains(Path.GetExtension(path));

    /// <summary>Card/table hoop badge: "Source (.EMB)" for working files instead of "Unknown".</summary>
    public static string HoopBadge(string path, string hoopCategory) =>
        IsWorkingFile(path) && (hoopCategory == "Unknown" || string.IsNullOrEmpty(hoopCategory)) ? SourceEmbBadge : hoopCategory;

    /// <summary>One-click "Stitch-Out Files Only (Hide .EMB)" filter.</summary>
    public static IEnumerable<CatalogEntry> ApplyStitchOutOnly(IEnumerable<CatalogEntry> entries, bool hideWorkingFiles) =>
        hideWorkingFiles ? entries.Where(e => !IsWorkingFile(e.Path)) : entries;
}
