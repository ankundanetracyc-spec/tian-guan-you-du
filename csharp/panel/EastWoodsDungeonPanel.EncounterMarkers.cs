using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class EastWoodsDungeonPanel
{
    private readonly Dictionary<string, Control> _beastEncounterMarkers = new();
    private Texture2D? _beastMarkerTexture;

    // Only a completed movement tween reaches this callback. Restoring a save,
    // opening the map, or cancelling a route must never start a new encounter.
    private void finish_player_arrival()
    {
        finish_pawn_step();
        if (!IsInsideTree() || !IsVisibleInTree() || _actionBusy || _npcEncounterBusy ||
            _actionModal?.Visible == true || GetTree().Paused) return;
        if (!_dynamicMode)
        {
            var content = available_actions(_current).OrderByDescending(action => action.Battle).FirstOrDefault();
            if (content is null) return;
            interrupt_travel();
            offer_arrival_narration(content);
            return;
        }
        if (ActionRequested is null) return;
        var encounter = available_actions(_current).FirstOrDefault(action => action.Battle);
        if (encounter is null) return;
        var node = _current;
        interrupt_travel();
        _actionBusy = true;
        try { ActionRequested.Invoke(node, encounter.Id); }
        finally { _actionBusy = false; if (IsInsideTree()) refresh_action_bar(); }
    }

    private void refresh_beast_markers()
    {
        foreach (var node in route_nodes)
        {
            // The action badge was resolved during this refresh. Reuse it rather
            // than scanning encounter history a second time for each map cell.
            var active = _dynamicMode && is_node_visible(node.Id) &&
                _actionBadgeCache.TryGetValue(node.Id, out var badge) && badge == "兽踪";
            if (!_beastEncounterMarkers.TryGetValue(node.Id, out var marker) || !IsInstanceValid(marker))
            {
                if (!active) continue;
                marker = create_beast_marker();
                _nodeButtons[node.Id].AddChild(marker);
                _beastEncounterMarkers[node.Id] = marker;
            }
            if (marker.Visible != active) marker.Visible = active;
        }
    }

    private Control create_beast_marker()
    {
        var marker = new PanelContainer { Name = "BeastEncounterMarker", MouseFilter = MouseFilterEnum.Ignore };
        marker.SetAnchorsPreset(LayoutPreset.TopRight);
        marker.OffsetLeft = -66; marker.OffsetRight = -5;
        marker.OffsetTop = 27; marker.OffsetBottom = 94;
        marker.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("e5ddc9"), BorderColor = new Color("955e48"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        });
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 0);
        marker.AddChild(content);
        if (_beastMarkerTexture is null)
        {
            var portrait = BattlePaperDoll.ForestWolfPortraitPath;
            var thumbnail = $"res://textures/ui/encyclopedia/portraits/{portrait.Sha256Text()[..16]}.png";
            _beastMarkerTexture = ResourceLoader.Load<Texture2D>(ResourceLoader.Exists(thumbnail) ? thumbnail : portrait);
        }
        content.AddChild(new TextureRect
        {
            Texture = _beastMarkerTexture, CustomMinimumSize = new Vector2(54, 42),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
        });
        var name = WorldHudStyle.Text("凶狼", 14);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.MouseFilter = MouseFilterEnum.Ignore;
        content.AddChild(name);
        return marker;
    }
}
