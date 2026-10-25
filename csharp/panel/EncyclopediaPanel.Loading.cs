using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

public partial class EncyclopediaPanel
{
    private int _catalogRequest;
    public bool IsCatalogLoading { get; private set; }
    internal double LastCatalogMaxSliceMilliseconds { get; private set; }

    private bool IsCatalogRequestCurrent(int request) => request == _catalogRequest && IsInsideTree() && IsVisibleInTree() && !_closing;

    private void CancelCatalogRequest()
    {
        _catalogRequest++;
        IsCatalogLoading = false;
    }

    public override void _ExitTree() => CancelCatalogRequest();

    private async Task EnsureCatalogLoadedAsync(Section section, int request)
    {
        var slice = Stopwatch.StartNew();
        LastCatalogMaxSliceMilliseconds = 0;
        if (!_entries.TryGetValue(section, out var entries))
        {
            IsCatalogLoading = true;
            ShowCatalogLoading(section);
            // Present the panel before resource work. No artificial timed loading screen.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsCatalogRequestCurrent(request)) return;
            slice.Restart();
            entries = [];
            var steps = section switch
            {
                Section.Statuses => LoadStatusEntriesSteps(entries),
                Section.Items => LoadItemEntriesSteps(entries),
                Section.Beasts => LoadBeastEntriesSteps(entries),
                Section.Terms => LoadTermEntriesSteps(entries),
                _ => Array.Empty<int>()
            };
            foreach (var _ in steps)
            {
                if (!IsCatalogRequestCurrent(request)) return;
                if (slice.Elapsed.TotalMilliseconds < 4) continue;
                LastCatalogMaxSliceMilliseconds = Math.Max(LastCatalogMaxSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!IsCatalogRequestCurrent(request)) return;
                slice.Restart();
            }
            entries = entries.OrderBy(entry => entry.Title, StringComparer.Ordinal).ToList();
            _entries[section] = entries;
        }

        // First construction is spread over frames; subsequent opens reuse the rows.
        foreach (var entry in entries)
        {
            if (!IsCatalogRequestCurrent(request)) return;
            if (_entryButtons.ContainsKey(entry)) continue;
            IsCatalogLoading = true;
            CreateEntryButton(entry);
            _entryButtons[entry].Hide();
            if (slice.Elapsed.TotalMilliseconds < 4) continue;
            LastCatalogMaxSliceMilliseconds = Math.Max(LastCatalogMaxSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsCatalogRequestCurrent(request)) return;
            slice.Restart();
        }
        LastCatalogMaxSliceMilliseconds = Math.Max(LastCatalogMaxSliceMilliseconds, slice.Elapsed.TotalMilliseconds);
    }

    private void ShowCatalogLoading(Section section)
    {
        foreach (var (tabSection, tab) in _tabs) StyleCategoryTab(tab, tabSection == section);
        foreach (var button in _entryButtons.Values) button.Hide();
        if (_selectedEntry is { } previous && _entryButtons.TryGetValue(previous, out var previousButton)) StyleEntry(previousButton);
        _selectedEntry = null;
        if (_entryCount is not null) _entryCount.Text = "正在载入…";
        if (_detailTitle is not null) _detailTitle.Text = $"正在整理{SectionTitle(section)}目录…";
        if (_detailMeta is not null) _detailMeta.Text = "";
        if (_detailBody is not null) { _detailBody.Text = ""; _detailBody.Show(); }
        _detailScroll?.Show();
        _itemDetailContent?.Hide();
        _statusDetailContent?.Hide();
        _detailIconPlate?.Hide();
        _beastDetail?.Hide();
        SetBeastPreview(null);
    }

    private sealed class PortraitThumbnailInfo
    {
        public PortraitThumbnailInfo() { }
        [JsonPropertyName("path")] public string Path { get; set; } = "";
        [JsonPropertyName("source_mtime")] public ulong SourceModifiedTime { get; set; }
        [JsonPropertyName("source_bytes")] public ulong SourceBytes { get; set; }
    }
    private static Dictionary<string, PortraitThumbnailInfo>? _portraitThumbnails;

    private static Texture2D? LoadBakedPortraitThumbnail(string sourcePath)
    {
        const string manifestPath = "res://textures/ui/encyclopedia/portraits/manifest.json";
        if (_portraitThumbnails is null)
        {
            try
            {
                _portraitThumbnails = Godot.FileAccess.FileExists(manifestPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, PortraitThumbnailInfo>>(Godot.FileAccess.GetFileAsString(manifestPath)) ?? []
                    : [];
            }
            catch (JsonException) { _portraitThumbnails = []; }
        }
        if (!_portraitThumbnails.TryGetValue(sourcePath, out var thumbnail) ||
            thumbnail.Path != $"res://textures/ui/encyclopedia/portraits/{sourcePath.Sha256Text()[..16]}.png") return null;
        // The bake validates the full portrait. If a loose source changes, use the
        // existing full-image fallback until the derived thumbnail is regenerated.
        // Exported textures may be remapped and have no loose PNG to inspect.
        if (Godot.FileAccess.FileExists(sourcePath))
        {
            if (Godot.FileAccess.GetModifiedTime(sourcePath) != thumbnail.SourceModifiedTime) return null;
            using var source = Godot.FileAccess.Open(sourcePath, Godot.FileAccess.ModeFlags.Read);
            if (source is null || source.GetLength() != thumbnail.SourceBytes) return null;
        }
        return LoadTexture(thumbnail.Path);
    }
}
