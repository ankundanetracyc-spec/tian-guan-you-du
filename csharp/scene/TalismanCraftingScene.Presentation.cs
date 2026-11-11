using Godot;
using System.Collections.Generic;
using System.Linq;
using Service = TalismanCraftingService;

public partial class TalismanCraftingScene
{
    private TalismanCraftingPresentation? _presentation;
    private int _presentationGeneration;
    private readonly List<CanvasItem> _presentationHiddenUi = [];

    private void HideCraftingUiForPresentation()
    {
        foreach (var item in _canvas.GetChildren().OfType<CanvasItem>().Where(item => item.Visible))
        {
            _presentationHiddenUi.Add(item);
            item.Hide();
        }
    }

    private void RestoreCraftingUiAfterPresentation()
    {
        foreach (var item in _presentationHiddenUi)
            if (GodotObject.IsInstanceValid(item)) item.Show();
        _presentationHiddenUi.Clear();
    }

    private void StartCraft()
    {
        if (_busy || OwnerCharacter is null) return;
        _busy = true;
        var request = CurrentRequest();
        if (!OwnerCharacter.TryCraftTalisman(request, out var outcome, out string reason))
        { _busy = false; Notice(reason, true); Refresh(); return; }

        // The transaction has committed. Playing, skipping or leaving never settles it again.
        HoverInfoManager.HideAll();
        CloseModal();
        HideCraftingUiForPresentation();
        var stage = new Control { Name = "CraftingModal", Size = new Vector2(1672, 941), MouseFilter = MouseFilterEnum.Stop };
        _canvas.AddChild(stage); _modal = stage;
        Place(stage, new ColorRect { Color = new Color(.13f, .19f, .14f, .30f), MouseFilter = MouseFilterEnum.Ignore },
            0, 0, 1672, 941);
        var view = new TalismanCraftingPresentation
        {
            Name = "CraftingPresentationView", CraftMode = request.Mode, Artwork = CentralIllustration()
        };
        _presentation = view;
        int generation = ++_presentationGeneration;
        var status = Text(view.PhaseText, 29);
        status.Name = "CraftingPresentationStatus";
        status.HorizontalAlignment = HorizontalAlignment.Center;
        status.AddThemeColorOverride("font_color", new Color("eee5ce"));
        status.AddThemeColorOverride("font_shadow_color", new Color(.10f, .14f, .10f, .85f));
        status.AddThemeConstantOverride("shadow_offset_y", 1);
        Place(stage, status, 386, 786, 900, 52);
        view.PhaseChanged += caption => status.Text = caption;
        view.Completed += () => FinishCraftPresentation(view, generation, outcome);
        Place(stage, view, 336, 102, 1000, 672);
        var skip = Button("跳过演出", view.Skip, 22);
        skip.Name = "SkipCraftPresentation";
        Place(stage, skip, 1418, 18, 230, 46);
    }

    private void FinishCraftPresentation(TalismanCraftingPresentation view, int generation, Service.Outcome outcome)
    {
        if (generation != _presentationGeneration || _presentation != view || !_busy
            || !GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;
        _presentation = null;
        ++_presentationGeneration;
        view.Stop();
        _busy = false;
        RefreshHeaderValues();
        RestoreCraftingUiAfterPresentation();
        ShowOutcome(outcome);
    }

    public override void _ExitTree()
    {
        ++_presentationGeneration;
        if (_presentation is not null && GodotObject.IsInstanceValid(_presentation)) _presentation.Stop();
        _presentation = null;
        _busy = false;
        RestoreCraftingUiAfterPresentation();
    }
}
