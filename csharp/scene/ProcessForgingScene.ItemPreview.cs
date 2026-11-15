using Godot;
using System;

public partial class ProcessForgingScene
{
    // 独立展示样本，不经炼制公式生成、不进入库存或正式人物。
    private async void PreviewSwordItem()
    {
        try
        {
            var item = new ProcessForgingTrial.Artifact
            {
                InstanceId = "ui-sword-sample-only", Name = "修长薄脊剑", Shape = "修长薄脊剑", Tier = "法器",
                Sharpness = 5, Polarity = 1,
                Effects =
                [
                    new() { CarrierId = "base", Route = "基础加成", Description = "物理防御 +4", Value = 4 },
                    new() { CarrierId = "keyword", Route = "常驻效能", Special = true,
                        Description = "带有“剑”关键词的技能，每段物理伤害 +2。", Value = 2,
                        Scope = "带有“剑”关键词且含物理伤害的技能", Event = "每段物理伤害结算", Condition = "无需绑定" },
                    new() { CarrierId = "binding", Route = "化法", Special = true,
                        Description = "每段物理伤害 +3。", Value = 3,
                        Scope = "带剑关键词且造成物理伤害的技能", Event = "随绑定技能结算", Condition = "" }
                ],
                BondContributions = ["惊鸿 +2"]
            };
            LoadExample(1);
            var panel = Overlay("器物详情", 1460, 810);
            var icon = new TextureRect { Texture = ForgedArtifactPresentation.Icon(item),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            Put(panel, icon, 40, 82, 340, 110);
            Put(panel, Text(item.Name, 32), 416, 86, 870, 46);
            Put(panel, Text(ForgedArtifactPresentation.Subtitle(item), 22), 418, 143, 870, 34);
            var rule = new ColorRect { Color = new Color("a7a58b"), MouseFilter = MouseFilterEnum.Ignore };
            Put(panel, rule, 40, 208, 1380, 1);

            var left = ScrollList(panel, 42, 242, 650, 470);
            void Add(VBoxContainer column, string title, string body)
            {
                column.AddChild(Text(title, 27)); column.AddChild(Wrap(body, 23));
                column.AddChild(new Control { CustomMinimumSize = new(0, 10) });
            }
            var sections = ForgedArtifactPresentation.Sections(item);
            Add(left, sections[0].Title, sections[0].Body);
            Add(left, sections[1].Title, sections[1].Body);
            Add(left, "器物效能", item.Effects[1].Description + "\n无需绑定，自动作用于符合条件的技能。");

            var right = ScrollList(panel, 758, 242, 650, 470);
            Add(right, "化法", "适用范围：带剑关键词且造成物理伤害的技能。\n\n化法追加：\n" +
                item.Effects[2].Description);
            Add(right, "器蕴", "本件贡献：惊鸿 +2\n\n激活效果预览\n带有“剑”关键词的技能，造成的物理伤害提高10%。");
            Put(panel, Wrap("界面展示样例 · 所有数值含器蕴10%均未定为正式平衡 · 不入库存，未结算装备收益", 17), 42, 748, 1370, 36);
            HoverInfoManager.HideAll(); Input.WarpMouse(new(1880, 1040));
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            const string folder = "res://docs/美术规范/法器剑属性样例_20261023/";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
            using var screenshot = GetViewport().GetTexture().GetImage();
            if (screenshot.SavePng(folder + "01_法器剑详情.png") != Error.Ok) throw new InvalidOperationException("器物截图保存失败");
            GD.Print("SWORD_ITEM_PREVIEW_OK"); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
