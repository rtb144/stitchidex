// SPDX-License-Identifier: MIT
using System.Diagnostics;
namespace Stitchidex.Core.Services;

/// <summary>Opens a pattern in the user's associated embroidery software (Wilcom Hatch, PE-Design, ...).</summary>
public static class DefaultAppLauncher
{
    public static Func<ProcessStartInfo, Process?> Starter { get; set; } = Process.Start;

    public static ProcessStartInfo BuildStartInfo(string filePath) => new() { FileName = filePath, UseShellExecute = true };

    /// <summary>Returns null on success, otherwise a friendly error message. Never throws.</summary>
    public static string? OpenInDefaultApp(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return "Select a pattern to open.";
        if (!File.Exists(filePath)) return $"File not found: {filePath}";
        try { Starter(BuildStartInfo(filePath)); return null; }
        catch (System.ComponentModel.Win32Exception)
        { return $"No program is set to open {Path.GetExtension(filePath)} files. In Explorer choose Open with → Choose another app and pick your embroidery software."; }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException or IOException)
        { return "Could not open the file: " + ex.Message; }
    }
}
