using Godot;

/// <summary>离线图标审阅；不写人物、库存或存档。</summary>
public partial class ForgedSwordIconPreview : Control
{
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--capture-sect-icons"))
            {
                var exchange = new YuxuSectExchangePanel(); AddChild(exchange); exchange.open_panel(0);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var buttons = (Dictionary<string, Button>)typeof(YuxuSectExchangePanel).GetField("_categoryButtons", flags)!.GetValue(exchange)!;
                buttons["器物"].EmitSignal(BaseButton.SignalName.Pressed);
                for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var capture = GetViewport().GetTexture().GetImage();
                capture.SavePng("res://docs/美术规范/器物斜放图标_20261025/02_宗门兑换.png");
                GD.Print("SECT_ICONS_CAPTURE_OK"); Input.SetCustomMouseCursor(null); GetTree().Quit(); return;
            }
            Theme = QuietRoomStyle.MakeTheme();
            var bg = new ColorRect { Color = new Color("eae2cc") }; AddChild(bg); bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var canvas = QuietRoomStyle.Canvas(this);
            void TextAt(string value, float x, float y, int size = 24)
            { var label = QuietRoomStyle.Text(value, size); canvas.AddChild(label); label.Position = new(x, y); }
            void Icon(Texture2D texture, float x, float y, float size)
            {
                var view = new TextureRect { Texture = texture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
                canvas.AddChild(view); view.Position = new(x, y); view.Size = new(size, size);
            }
            TextAt("剑器 · 斜放图标", 90, 60, 36);
            string[] shapes = ["修长薄脊剑", "宽展剑", "厚脊剑"];
            for (var i = 0; i < shapes.Length; i++)
            {
                var artifact = new ProcessForgingTrial.Artifact { InstanceId = "icon-preview-" + i, Name = shapes[i], Shape = shapes[i], Category = "剑", Tier = "法器" };
                var path = ForgedArtifactPresentation.IconPath(artifact);
                var texture = ForgedArtifactPresentation.Icon(artifact);
                var body = ForgedArtifactPresentation.BodySpritePath(artifact);
                if (texture is null || texture.GetWidth() != texture.GetHeight() || !body.Contains("20261023_v1")) throw new System.Exception("图标与战斗道具未正确分离");
                var stored = ForgedArtifactInventory.Create(artifact); stored.form!.icon_path = body;
                if (ForgedArtifactInventory.IconPath(stored) != path) throw new System.Exception("旧存档图标没有更新");
                var x = 90 + i * 520;
                var paper = new Panel { Position = new(x, 150), Size = new(470, 650) };
                paper.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(padding: 0, plain: true)); canvas.AddChild(paper);
                TextAt(shapes[i], x + 30, 178, 26); Icon(texture, x + 60, 225, 340);
                TextAt("行囊", x + 35, 600, 19); Icon(texture, x + 35, 640, 64);
                TextAt("装备", x + 155, 600, 19); Icon(texture, x + 155, 630, 96);
                var slot = GD.Load<PackedScene>("res://tscn/panel/StorageItemSlot.tscn").Instantiate<StorageItemSlot>();
                canvas.AddChild(slot); slot.Position = new(x + 305, 625); slot.Size = new(120, 140); slot.set_artifact(stored);
                TextAt("物品格", x + 305, 600, 19);
            }
            TextAt("左下握柄 · 右上剑尖", 90, 840, 22);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://docs/美术规范/器物斜放图标_20261025/01_图标实机对照.png") != Error.Ok) throw new System.Exception("截图失败");
            GD.Print("FORGED_SWORD_ICONS_OK"); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (System.Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
