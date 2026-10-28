// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Text.RegularExpressions;
namespace Stitchidex.Core.Help;

public enum HelpBlockKind { Heading1, Heading2, Heading3, Paragraph, Bullet, Numbered, Code }
public sealed record HelpBlock(HelpBlockKind Kind, string Text);

public sealed record HelpTopic(string Id, string Title, string Markdown)
{
    public IReadOnlyList<HelpBlock> Blocks => OfflineHelpCatalog.Render(Markdown);
    public string PlainText => string.Join("\n", Blocks.Select(b => b.Text));
}

/// <summary>
/// The GitHub wiki + Windows User Guide, compiled into Stitchidex.Core as embedded resources
/// (Stitchidex.Help.*.md) so Help works with zero internet connection.
/// </summary>
public static class OfflineHelpCatalog
{
    const string Prefix = "Stitchidex.Help.";
    /// <summary>Sidebar order; any other embedded page is appended alphabetically.</summary>
    static readonly (string Id, string Title)[] Order =
    {
        ("Home", "Home / Welcome"), ("Quick-Start", "Quick Start"),
        ("Windows-10-and-11-Installation-Guide", "Installing on Windows 10 & 11"), ("Portable-Zip-Guide", "Installation & Portable .zip Guide"),
        ("Watched-Folders-and-Cloud-Safety", "Watched Folders & Dropbox/Cloud Safety"), ("Search-and-Faceted-Filtering", "Search & Faceted Filtering"),
        ("EMB-Working-Files-vs-Stitch-Out-Formats", ".EMB Working Files vs Stitch-Out Formats"), ("Selection-and-Tagging", "Selection & Tagging"),
        ("Searching-Tagging-and-Organizing-Patterns", "Searching, Tagging & Organizing"), ("Sending-Patterns-to-Your-USB-Stick", "Send to USB Embroidery Machine Workflow"),
        ("Table-View-and-Preview-Background", "Table View & Preview Background"), ("Troubleshooting-and-FAQ", "Troubleshooting & FAQ"),
        ("Windows-10-11-User-Guide", "Windows 10/11 User Guide (full)"),
    };

    static IReadOnlyList<HelpTopic>? _topics;
    public static IReadOnlyList<HelpTopic> Topics => _topics ??= Load();

    static IReadOnlyList<HelpTopic> Load()
    {
        var asm = typeof(OfflineHelpCatalog).Assembly;
        var pages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(Prefix) && n.EndsWith(".md")))
        {
            var id = name[Prefix.Length..^3];
            if (id.StartsWith("_")) continue; // _Sidebar
            using var s = asm.GetManifestResourceStream(name)!;
            pages[id] = new StreamReader(s).ReadToEnd();
        }
        var list = new List<HelpTopic>();
        foreach (var (id, title) in Order)
            if (pages.Remove(id, out var md)) list.Add(new HelpTopic(id, title, md));
        foreach (var kv in pages.OrderBy(k => k.Key)) list.Add(new HelpTopic(kv.Key, kv.Key.Replace('-', ' '), kv.Value));
        return list;
    }

    public static HelpTopic? Get(string id) => Topics.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Offline keyword search: every word must appear in the topic title or text (case-insensitive).</summary>
    public static IReadOnlyList<HelpTopic> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Topics;
        var q = query.Trim();
        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Topics.Select(t => (t, hay: t.Title + "\n" + t.PlainText))
            .Where(x => words.All(w => x.hay.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.t.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => Regex.Matches(x.hay, Regex.Escape(q), RegexOptions.IgnoreCase).Count)
            .Select(x => x.t).ToList();
    }

    static string Inline(string s)
    {
        s = Regex.Replace(s, @"\[\[([^|\]]+)\|[^\]]+\]\]", "$1");     // [[Label|Page]]
        s = Regex.Replace(s, @"\[\[([^\]]+)\]\]", "$1");
        s = Regex.Replace(s, @"!\[([^\]]*)\]\([^)]*\)", "");          // images need internet: drop
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1");
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = s.Replace("**", "").Replace("`", "");
        s = Regex.Replace(s, @"(?<!\w)\*([^*]+)\*(?!\w)", "$1");
        return s.Trim();
    }

    /// <summary>Minimal Markdown → formatted blocks for the in-app viewer (no browser, no network).</summary>
    public static IReadOnlyList<HelpBlock> Render(string markdown)
    {
        var blocks = new List<HelpBlock>();
        bool code = false; var buf = new List<string>();
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```")) { if (code) { blocks.Add(new(HelpBlockKind.Code, string.Join("\n", buf))); buf.Clear(); } code = !code; continue; }
            if (code) { buf.Add(raw); continue; }
            var t = line.Trim();
            if (t.Length == 0 || t == "---") continue;
            HelpBlock? b = t switch
            {
                _ when t.StartsWith("### ") => new(HelpBlockKind.Heading3, Inline(t[4..])),
                _ when t.StartsWith("## ") => new(HelpBlockKind.Heading2, Inline(t[3..])),
                _ when t.StartsWith("# ") => new(HelpBlockKind.Heading1, Inline(t[2..])),
                _ when t.StartsWith("- ") || t.StartsWith("* ") => new(HelpBlockKind.Bullet, Inline(t[2..])),
                _ when Regex.IsMatch(t, @"^\d+\.\s") => new(HelpBlockKind.Numbered, Inline(t)),
                _ => new(HelpBlockKind.Paragraph, Inline(t)),
            };
            if (b.Text.Length > 0) blocks.Add(b);
        }
        if (code && buf.Count > 0) blocks.Add(new(HelpBlockKind.Code, string.Join("\n", buf)));
        return blocks;
    }
}
