using System.Diagnostics;
using System.Xml.Linq;
using Stitchidex.Core.Help;
using Stitchidex.Core.Parsers;
using Stitchidex.Core.Services;
namespace Stitchidex.Tests;

/// <summary>v0.5.1 tester feedback (Stitchidex Notes.odt): cloud safety, offline help, .EMB, selection, layout.</summary>
[Collection("UsbVolumes")]
public class V051TesterFeedbackTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("stxv051").FullName;
    public void Dispose()
    {
        CloudPlaceholderDetector.AttributeProvider = null;
        DefaultAppLauncher.Starter = Process.Start;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }
    static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (!Directory.Exists(Path.Combine(d.FullName, "samples"))) d = d.Parent!; return d.FullName; }
    static string Sample(string ext) => Directory.GetFiles(Path.Combine(Root(), "samples")).First(f => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    static XDocument MainXaml() => XDocument.Load(Path.Combine(Root(), "src", "Stitchidex.Desktop", "Views", "MainWindow.axaml"));
    static readonly XNamespace Av = "https://github.com/avaloniaui";
    static string? Attr(XElement e, string n) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == n)?.Value;
    static string? Name(XElement e) => Attr(e, "Name");

    // ---------- 1. Cloud placeholders ----------
    [Theory]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS, true)]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_RECALL_ON_OPEN, true)]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_OFFLINE, true)]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_UNPINNED | CloudPlaceholderDetector.FILE_ATTRIBUTE_REPARSE_POINT, true)]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_UNPINNED, true)]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_PINNED | CloudPlaceholderDetector.FILE_ATTRIBUTE_REPARSE_POINT, false)]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_PINNED | CloudPlaceholderDetector.FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS, false)]
    [InlineData((int)FileAttributes.Normal, false)]
    [InlineData((int)FileAttributes.Archive, false)]
    [InlineData(CloudPlaceholderDetector.FILE_ATTRIBUTE_REPARSE_POINT, false)]
    public void Given_file_attributes_When_detecting_Then_only_unhydrated_cloud_files_are_placeholders(int attrs, bool expected) =>
        Assert.Equal(expected, CloudPlaceholderDetector.IsCloudPlaceholder((FileAttributes)attrs));

    [Fact]
    public void Given_attribute_constants_Then_they_match_the_Windows_SDK_values()
    {
        Assert.Equal(0x00040000, CloudPlaceholderDetector.FILE_ATTRIBUTE_RECALL_ON_OPEN);
        Assert.Equal(0x00400000, CloudPlaceholderDetector.FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS);
        Assert.Equal(0x00100000, CloudPlaceholderDetector.FILE_ATTRIBUTE_UNPINNED);
        Assert.Equal(0x00080000, CloudPlaceholderDetector.FILE_ATTRIBUTE_PINNED);
        Assert.Equal((int)FileAttributes.Offline, CloudPlaceholderDetector.FILE_ATTRIBUTE_OFFLINE);
        Assert.Equal((int)FileAttributes.ReparsePoint, CloudPlaceholderDetector.FILE_ATTRIBUTE_REPARSE_POINT);
    }

    [Fact]
    public void Given_a_real_local_file_When_detecting_Then_it_is_not_a_placeholder()
    {
        var f = Path.Combine(_dir, "local.pes"); File.Copy(Sample(".pes"), f);
        Assert.False(CloudPlaceholderDetector.IsCloudPlaceholder(f));
        Assert.False(CloudPlaceholderDetector.IsCloudPlaceholder(Path.Combine(_dir, "missing.pes")));
    }

    (SqliteCatalogService Cat, string Lib) DropboxLibrary()
    {
        var lib = Path.Combine(_dir, "Dropbox"); Directory.CreateDirectory(Path.Combine(lib, "online-only"));
        File.Copy(Sample(".pes"), Path.Combine(lib, "local_rose.pes"));
        File.Copy(Sample(".vp3"), Path.Combine(lib, "online-only", "cloud_tree.vp3"));
        File.Copy(Sample(".pes"), Path.Combine(lib, "online-only", "cloud_star.pes"));
        CloudPlaceholderDetector.AttributeProvider = p => p.Contains("online-only")
            ? (FileAttributes)(CloudPlaceholderDetector.FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS | CloudPlaceholderDetector.FILE_ATTRIBUTE_UNPINNED | (int)FileAttributes.ReparsePoint)
            : FileAttributes.Archive;
        return (new SqliteCatalogService(Path.Combine(_dir, "c.db")), lib);
    }

    [Fact]
    public async Task Given_Dropbox_online_only_files_When_scanning_in_Cloud_Safe_Mode_Then_no_bytes_are_parsed_and_badge_is_set()
    {
        var (cat, lib) = DropboxLibrary();
        using var _ = cat;
        Assert.True(cat.SkipCloudPlaceholders); // default
        Assert.Equal(3, cat.ScanDirectory(lib));
        Assert.Equal(2, cat.CloudPlaceholdersSkipped);
        var all = cat.All();
        var local = all.Single(e => e.Path.EndsWith("local_rose.pes"));
        Assert.True(local.StitchCount > 0);
        foreach (var c in all.Where(e => e.Path.Contains("online-only")))
        {
            // Real sample bytes would parse to >0 stitches: 0 proves the file stream was never read.
            Assert.Equal(0, c.StitchCount);
            Assert.Null(c.Thumbnail);
            Assert.Equal(CloudPlaceholderDetector.CloudOnlyBadge, c.HoopCategory);
            Assert.Equal("Cloud Only (Not Downloaded)", c.HoopCategory);
        }
        Assert.Contains(all, e => e.Title == "cloud tree");
        // Thumbnail repair must not hydrate placeholders either.
        Assert.Equal(0, await cat.RepairMissingThumbnailsAsync());
    }

    [Fact]
    public void Given_user_allows_cloud_downloads_When_scanning_Then_online_only_files_are_parsed()
    {
        var (cat, lib) = DropboxLibrary();
        using var _ = cat;
        cat.SkipCloudPlaceholders = false;
        cat.ScanDirectory(lib);
        Assert.Equal(0, cat.CloudPlaceholdersSkipped);
        Assert.All(cat.All(), e => Assert.True(e.StitchCount > 0));
    }

    [Fact]
    public async Task Given_a_scan_When_canceled_Then_it_stops_with_OperationCanceled()
    {
        var (cat, lib) = DropboxLibrary();
        using var _ = cat;
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cat.ScanDirectoryAsync(lib, null, cts.Token));
        Assert.Empty(cat.All());
    }

    // ---------- 2. Offline help ----------
    static readonly string[] RequiredTopics =
    {
        "Home", "Quick-Start", "Windows-10-and-11-Installation-Guide", "Portable-Zip-Guide", "Watched-Folders-and-Cloud-Safety",
        "Search-and-Faceted-Filtering", "EMB-Working-Files-vs-Stitch-Out-Formats", "Selection-and-Tagging",
        "Searching-Tagging-and-Organizing-Patterns", "Sending-Patterns-to-Your-USB-Stick", "Table-View-and-Preview-Background",
        "Troubleshooting-and-FAQ", "Windows-10-11-User-Guide",
    };

    [Fact]
    public void Given_bundled_wiki_When_loading_offline_help_Then_every_section_is_present_and_non_empty()
    {
        var topics = OfflineHelpCatalog.Topics;
        foreach (var id in RequiredTopics)
        {
            var t = OfflineHelpCatalog.Get(id);
            Assert.NotNull(t);
            Assert.False(string.IsNullOrWhiteSpace(t!.Title));
            Assert.True(t.Blocks.Count >= 3, id);
            Assert.True(t.PlainText.Length > 200, id);
        }
        Assert.Equal("Home", topics[0].Id);
        Assert.DoesNotContain(topics, t => t.Id.StartsWith("_"));
    }

    [Fact]
    public void Given_every_wiki_markdown_file_in_repo_Then_it_is_embedded_in_the_help_catalog()
    {
        foreach (var f in Directory.GetFiles(Path.Combine(Root(), "wiki"), "*.md").Where(f => !Path.GetFileName(f).StartsWith("_")))
            Assert.NotNull(OfflineHelpCatalog.Get(Path.GetFileNameWithoutExtension(f)));
    }

    [Theory]
    [InlineData("Send to USB", "Sending-Patterns-to-Your-USB-Stick")]
    [InlineData("Dropbox", "Watched-Folders-and-Cloud-Safety")]
    [InlineData(".EMB", "EMB-Working-Files-vs-Stitch-Out-Formats")]
    [InlineData("Ctrl", "Selection-and-Tagging")]
    [InlineData("Table View", "Table-View-and-Preview-Background")]
    public void Given_keyword_When_searching_help_offline_Then_the_matching_guide_is_found(string q, string expectedId)
    {
        var hits = OfflineHelpCatalog.Search(q);
        Assert.NotEmpty(hits);
        Assert.Contains(hits, t => t.Id == expectedId);
        Assert.All(hits, t => Assert.All(q.Split(' '), w => Assert.Contains(w, t.Title + "\n" + t.PlainText, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(q, hits[0].Title + "\n" + hits[0].PlainText, StringComparison.OrdinalIgnoreCase); // exact phrase ranks first
    }

    [Fact]
    public void Given_empty_or_nonsense_query_Then_all_or_no_topics_are_returned()
    {
        Assert.Equal(OfflineHelpCatalog.Topics.Count, OfflineHelpCatalog.Search("  ").Count);
        Assert.Empty(OfflineHelpCatalog.Search("zzqxv-not-a-word"));
    }

    [Fact]
    public void Given_markdown_When_rendering_Then_blocks_are_formatted_and_online_images_and_links_are_stripped()
    {
        var b = OfflineHelpCatalog.Render("# Title\n![shot](https://x/y.png)\n## Sub\n- **bold** item\n1. step one\nSee [[Guide|Page-Id]] and [site](https://a.b).\n```\ncode\n```");
        Assert.Equal(new[] { HelpBlockKind.Heading1, HelpBlockKind.Heading2, HelpBlockKind.Bullet, HelpBlockKind.Numbered, HelpBlockKind.Paragraph, HelpBlockKind.Code }, b.Select(x => x.Kind));
        Assert.Equal("bold item", b[2].Text);
        Assert.Equal("See Guide and site.", b[4].Text);
        Assert.DoesNotContain(b, x => x.Text.Contains("http"));
    }

    // ---------- 3. .EMB working files ----------
    [Fact]
    public void Given_a_Hatch_emb_file_When_parsed_Then_it_is_a_Source_design_not_Unknown()
    {
        var f = Path.Combine(_dir, "Christmas_Tree.emb");
        File.WriteAllBytes(f, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0 });
        var p = EmbroideryParserRegistry.ParseFile(f);
        Assert.Equal("Source (.EMB)", p.HoopCategory);
        Assert.NotEqual("Unknown", p.HoopCategory);
        Assert.Equal(PatternFormats.EmbFamily, p.FormatFamily);
        Assert.True(PatternFormats.IsWorkingFile(f));
        Assert.False(PatternFormats.IsStitchOutFile(f));
        Assert.Equal("Source (.EMB)", PatternFormats.HoopBadge("old.EMB", "Unknown")); // pre-0.5.1 catalog rows
    }

    [Fact]
    public void Given_emb_vp3_and_pes_When_StitchOutOnly_filter_is_on_Then_emb_is_hidden_and_stitch_outs_kept()
    {
        var lib = Path.Combine(_dir, "hatch"); Directory.CreateDirectory(lib);
        File.WriteAllBytes(Path.Combine(lib, "snowman.emb"), new byte[64]);
        File.Copy(Sample(".vp3"), Path.Combine(lib, "snowman.vp3"));
        File.Copy(Sample(".pes"), Path.Combine(lib, "snowman.pes"));
        using var cat = new SqliteCatalogService(Path.Combine(_dir, "e.db"));
        cat.ScanDirectory(lib);
        var all = cat.All();
        Assert.Equal(3, all.Count);
        Assert.Equal("Source (.EMB)", all.Single(e => e.Path.EndsWith(".emb")).HoopCategory);
        var shown = PatternFormats.ApplyStitchOutOnly(all, hideWorkingFiles: true).ToList();
        Assert.Equal(2, shown.Count);
        Assert.DoesNotContain(shown, e => e.Path.EndsWith(".emb"));
        Assert.Contains(shown, e => e.Path.EndsWith(".vp3"));
        Assert.Contains(shown, e => e.Path.EndsWith(".pes"));
        Assert.Equal(3, PatternFormats.ApplyStitchOutOnly(all, false).Count());
        foreach (var ext in new[] { ".vp3", ".pes", ".dst", ".jef", ".exp", ".xxx", ".hus", ".pec" }) Assert.True(PatternFormats.IsStitchOutFile("a" + ext));
    }

    // ---------- 4. Selection + open in default app ----------
    [Fact]
    public void Given_checkboxes_When_toggling_Then_selected_count_drives_Tag_and_Send_labels()
    {
        var sel = new PatternSelectionModel<string>();
        int changes = 0; sel.Changed += () => changes++;
        Assert.Equal("🏷️ Tag Selected (0)", sel.TagSelectedLabel);
        Assert.True(sel.TogglePatternSelection("rose.pes"));
        Assert.True(sel.TogglePatternSelection("tree.vp3"));
        Assert.Equal(2, sel.Count);
        Assert.Equal("🏷️ Tag Selected (2)", sel.TagSelectedLabel);
        Assert.Equal("💾 Send to USB (2)", sel.SendToUsbLabel);
        Assert.False(sel.TogglePatternSelection("rose.pes"));
        Assert.Equal("🏷️ Tag Selected (1)", sel.TagSelectedLabel);
        Assert.Equal("💾 Send to USB", sel.SendToUsbLabel);
        Assert.False(sel.IsSelected("rose.pes"));
        // Ctrl/Shift-click path replaces the set; checkbox state follows.
        sel.Reset(new[] { "a", "b", "c", "a" });
        Assert.Equal(3, sel.Count); Assert.True(sel.IsSelected("b"));
        sel.Set("b", false); sel.Set("b", false);
        Assert.Equal(2, sel.Count);
        Assert.Equal(5, changes); // no-op Set does not fire
    }

    [Fact]
    public void Given_a_pattern_When_OpenInDefaultApp_Then_shell_execute_is_used_with_the_file_path()
    {
        var f = Path.Combine(_dir, "hatch_design.emb"); File.WriteAllBytes(f, new byte[8]);
        ProcessStartInfo? got = null;
        DefaultAppLauncher.Starter = psi => { got = psi; return null; };
        Assert.Null(DefaultAppLauncher.OpenInDefaultApp(f));
        Assert.NotNull(got);
        Assert.Equal(f, got!.FileName);
        Assert.True(got.UseShellExecute);
    }

    [Fact]
    public void Given_no_registered_app_or_missing_file_When_opening_Then_a_friendly_error_is_returned_not_thrown()
    {
        var f = Path.Combine(_dir, "x.vp3"); File.WriteAllBytes(f, new byte[8]);
        DefaultAppLauncher.Starter = _ => throw new System.ComponentModel.Win32Exception(1155);
        Assert.Contains("No program is set", DefaultAppLauncher.OpenInDefaultApp(f));
        Assert.Contains("not found", DefaultAppLauncher.OpenInDefaultApp(Path.Combine(_dir, "gone.pes")));
        Assert.NotNull(DefaultAppLauncher.OpenInDefaultApp(null));
    }

    // ---------- 5. XAML layout ----------
    [Fact]
    public void Given_MainWindow_xaml_Then_header_has_Help_button_and_dedicated_status_row()
    {
        var x = MainXaml();
        var els = x.Descendants().ToList();
        var help = els.Single(e => Name(e) == "HelpButton");
        Assert.Contains("Help", Attr(help, "Content"));
        Assert.Equal("OnOpenHelp", Attr(help, "Click"));
        Assert.False(string.IsNullOrEmpty(Attr(help, "ToolTip.Tip")));
        var header = els.Single(e => Name(e) == "HeaderGrid");
        Assert.Equal("Auto,Auto", Attr(header, "RowDefinitions"));
        var status = els.Single(e => Name(e) == "ScanStatusRow");
        Assert.Equal("1", Attr(status, "Grid.Row"));
        Assert.Contains(status.Descendants(), e => e.Name.LocalName == "ProgressBar");
        Assert.Contains(status.Descendants(), e => Attr(e, "Command") == "{Binding CancelScanCommand}" && Attr(e, "Content")!.Contains("Cancel Scan"));
        // Nothing else lives in the status row, so it can't overlap the Theme selector / Smart Search toggle.
        Assert.DoesNotContain(els, e => e.Name.LocalName == "ComboBox" && Attr(e, "Grid.Row") == "1");
        Assert.Equal("0", Attr(els.Single(e => Name(e) == "SmartSearchToggle"), "Grid.Row"));
    }

    [Fact]
    public void Given_MainWindow_xaml_Then_key_actions_have_tooltips()
    {
        var els = MainXaml().Descendants().ToList();
        bool Tip(Func<XElement, bool> pick) => els.Where(pick).Any() && els.Where(pick).All(e => !string.IsNullOrWhiteSpace(Attr(e, "ToolTip.Tip")));
        Assert.True(Tip(e => Attr(e, "Click") == "OnSendToUsb"));
        Assert.True(Tip(e => Attr(e, "Click") == "OnBulkTag"));
        Assert.True(Tip(e => Name(e) == "SmartSearchToggle"));
        Assert.True(Tip(e => Attr(e, "SelectedItem") == "{Binding SelectedPreviewBackground}"));
        Assert.True(Tip(e => Attr(e, "IsChecked") == "{Binding HideWorkingFiles}"));
        Assert.True(Tip(e => Attr(e, "IsChecked") == "{Binding AllowCloudDownloads}"));
        Assert.True(Tip(e => Attr(e, "Command") is "{Binding RemoveFolderCommand}" or "{Binding RescanAllCommand}" or "{Binding ClearFolderFilterCommand}" || Attr(e, "Click") == "OnAddFolder"));
        Assert.True(Tip(e => Attr(e, "Command") == "{Binding OpenInDefaultAppCommand}" && e.Name.LocalName == "Button"));
    }

    [Fact]
    public void Given_Table_View_Then_horizontal_scrolling_is_enabled_and_columns_have_fixed_widths()
    {
        var grid = MainXaml().Descendants().Single(e => Name(e) == "TableGrid");
        Assert.Equal("Auto", Attr(grid, "HorizontalScrollBarVisibility"));
        Assert.Equal("OnPatternDoubleTapped", Attr(grid, "DoubleTapped"));
        var cols = grid.Descendants().Where(e => e.Name.LocalName.StartsWith("DataGrid") && e.Name.LocalName.EndsWith("Column")).ToList();
        Assert.DoesNotContain(cols, c => Attr(c, "Width")?.Contains('*') == true); // star widths would squeeze instead of scroll
        Assert.Contains(cols, c => c.Descendants().Any(d => d.Name.LocalName == "CheckBox" && Attr(d, "Click") == "OnSelectCheckBoxClick"));
    }

    [Fact]
    public void Given_Watched_Folders_Then_list_is_bounded_scrollable_with_full_path_tooltips_and_buttons_wrap()
    {
        var els = MainXaml().Descendants().ToList();
        var sv = els.Single(e => Name(e) == "WatchedFoldersScroll");
        Assert.Equal("ScrollViewer", sv.Name.LocalName);
        Assert.True(double.Parse(Attr(sv, "MaxHeight")!) <= 220);
        Assert.Equal("Auto", Attr(sv, "HorizontalScrollBarVisibility"));
        Assert.Equal("Auto", Attr(sv, "VerticalScrollBarVisibility"));
        var list = sv.Descendants().Single(e => e.Name.LocalName == "ListBox");
        Assert.Equal("{Binding Folders}", Attr(list, "ItemsSource"));
        Assert.Contains(list.Descendants(), e => e.Name.LocalName == "TextBlock" && Attr(e, "ToolTip.Tip") == "{Binding}");
        var buttons = els.Single(e => Name(e) == "FolderButtons");
        Assert.Equal("WrapPanel", buttons.Name.LocalName);
        var bs = buttons.Elements().Where(e => e.Name.LocalName == "Button").ToList();
        Assert.Equal(4, bs.Count);
        Assert.Contains(bs, b => Attr(b, "Content")!.EndsWith("Remove Folder"));
        Assert.All(bs, b => { Assert.Null(Attr(b, "Width")); Assert.Null(Attr(b, "MaxWidth")); }); // content-sized: never clipped
        Assert.True(sv.ElementsBeforeSelf().Count() >= 0 && els.IndexOf(sv) < els.IndexOf(buttons)); // list sits above buttons in its own row
    }

    [Fact]
    public void Given_pattern_cards_Then_they_have_selection_checkbox_double_click_and_context_open_action()
    {
        var els = MainXaml().Descendants().ToList();
        var grid = els.Single(e => Name(e) == "GridList");
        Assert.Equal("OnPatternDoubleTapped", Attr(grid, "DoubleTapped"));
        var cb = grid.Descendants().Single(e => e.Name.LocalName == "CheckBox");
        Assert.Equal("{Binding IsSelected, Mode=OneWay}", Attr(cb, "IsChecked"));
        Assert.Equal("OnSelectCheckBoxClick", Attr(cb, "Click"));
        Assert.Contains(grid.Descendants(), e => e.Name.LocalName == "MenuItem" && Attr(e, "Header")!.Contains("Open in Default App"));
    }

    [Fact]
    public void Given_desktop_project_Then_help_docs_are_copied_next_to_the_exe_and_version_is_051()
    {
        var csproj = File.ReadAllText(Path.Combine(Root(), "src", "Stitchidex.Desktop", "Stitchidex.Desktop.csproj"));
        Assert.Contains("<Version>0.5.1</Version>", csproj);
        Assert.Contains(@"Link=""help\%(Filename)%(Extension)""", csproj);
        var core = File.ReadAllText(Path.Combine(Root(), "src", "Stitchidex.Core", "Stitchidex.Core.csproj"));
        Assert.Contains("Stitchidex.Help.%(Filename).md", core);
    }
}
