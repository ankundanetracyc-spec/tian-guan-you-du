using Godot;

/// <summary>命令行验收：使用临时人物与临时资源，不接触正式旅程。</summary>
public partial class RealmProgressionSmokeScene : Control
{
    public override async void _Ready()
    {
        try
        {
            Theme = new Theme { DefaultFont = GD.Load<Font>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf") };
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            Check(CondensationOutcomeRules.DifficultySteps(104) == 0, "不足五分不计档");
            Check(CondensationOutcomeRules.DifficultySteps(105) == 1, "五分增加一档");
            Check(CondensationOutcomeRules.FlatAttributeBonus(124) == 0 && CondensationOutcomeRules.FlatAttributeBonus(125) == 1, "定额二十五分一档");
            Check(CondensationOutcomeRules.SpiritRequirementMultiplier(225) == 1.3125d && CondensationOutcomeRules.FoundationCloudBonus(225) == .625d, "超过二百分继续计档");
            var realms = GetNode<RealmManager>("/root/RealmManager");
            foreach (var realm in realms.realm_order.Where(r => r.StartsWith("云息")))
            {
                var config = realms.get_realm_config(realm)!;
                Check(!config.realm_fixed_bonuses.ContainsKey("comprehension") && !config.realm_multiplier_bonuses.ContainsKey("comprehension"), realm + "不奖励悟性");
            }
            var data = new HumanData
            {
                entity_name = "境界验收临时人物", cultivation_realm = "云息十层", has_spirit_core = true,
                condensation_final_score = 225, yuanxi_count = 8,
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
                spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
                main_gongfa_path = "res://resources/gongfa/黄庭养形篇.tres", ambient_lingqi_pressure = 180,
                current_world_stamina = 0, max_world_stamina = 100
            };
            var character = new Character { entity_data = data, ProcessMode = ProcessModeEnum.Disabled };
            AddChild(character); character.Hide();
            var expectedRealms=new[]{"凡人","云息一层","云息二层","云息三层","云息四层","云息五层","云息六层","云息七层","云息八层","云息九层","云息十层"};
            Check(realms.realm_order.SequenceEqual(expectedRealms),"六七层同为144缕仍按境界顺序排列");
            var expectedTotals=new[]{6,18,36,60,96,144,144,198,258,324};
            for(var i=1;i<expectedRealms.Length;i++)
                Check(realms.get_realm_config(expectedRealms[i])!.required_attributes["spirit_strands"].AsInt32()==expectedTotals[i-1],expectedRealms[i]+"基准门槛");
            var pillData = new HumanData
            {
                entity_name = "突破丹药验收临时人物", cultivation_realm = "云息三层", spirit_strands = 60,
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
                spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
                main_gongfa_path = "res://resources/gongfa/黄庭养形篇.tres",
                ambient_lingqi_pressure = 180, current_world_stamina = 100, max_world_stamina = 100
            };
            var pillCharacter = new Character { entity_data = pillData, ProcessMode = ProcessModeEnum.Disabled };
            AddChild(pillCharacter); pillCharacter.Hide();
            var threeToFourPill = ResourceLoader.Load<ItemData>(Character.YunxiThreeToFourPillPath);
            var nineToTenPill = ResourceLoader.Load<ItemData>(Character.YunxiNineToTenPillPath);
            Check(threeToFourPill?.definition_id.ToString() == "pill_yunxi_meridian_stabilizing" && nineToTenPill?.definition_id.ToString() == "pill_yunxi_return_origin", "两枚关口丹药使用稳定物品ID");
            Check(threeToFourPill is not null && ResourceLoader.Load<Texture2D>(threeToFourPill.icon_path) is not null && nineToTenPill is not null && ResourceLoader.Load<Texture2D>(nineToTenPill.icon_path) is not null, "两枚关口丹药均有可加载图标");
            Check(pillCharacter.grant_new_journey_yunxi_breakthrough_pills() && pillCharacter.get_inventory_total_count(Character.YunxiThreeToFourPillPath) == 1 && pillCharacter.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 1, "新旅程各发一枚突破丹");
            Check(pillCharacter.grant_new_journey_yunxi_breakthrough_pills() && pillCharacter.get_inventory_total_count(Character.YunxiThreeToFourPillPath) == 1 && pillCharacter.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 1, "重复初始化不会重复发放突破丹");
            Check(pillCharacter.remove_item_from_inventory(Character.YunxiThreeToFourPillPath) && !pillCharacter.start_breakthrough_retreat() && pillCharacter.cultivation_realm == "云息三层", "三至四缺丹时不得开始突破");
            var addedFirstPill = pillCharacter.add_item_to_inventory(Character.YunxiThreeToFourPillPath);
            var startedFirstPillBreakthrough = pillCharacter.start_breakthrough_retreat();
            Check(addedFirstPill && startedFirstPillBreakthrough && pillCharacter.cultivation_realm == "云息四层" && pillCharacter.get_inventory_total_count(Character.YunxiThreeToFourPillPath) == 0, "三至四成功晋层才消耗定脉丹");
            pillCharacter.cultivation_realm = "云息九层"; pillCharacter.spirit_strands = 324;
            Check(pillCharacter.remove_item_from_inventory(Character.YunxiNineToTenPillPath) && !pillCharacter.start_breakthrough_retreat() && pillCharacter.cultivation_realm == "云息九层", "九至十缺丹时不得开始突破");
            Check(pillCharacter.add_item_to_inventory(Character.YunxiNineToTenPillPath) && pillCharacter.start_breakthrough_retreat() && pillCharacter.cultivation_realm == "云息十层" && pillCharacter.get_inventory_total_count(Character.YunxiNineToTenPillPath) == 0, "九至十成功晋层才消耗归元定息丹");
            Check(pillCharacter.has_required_breakthrough_pill("云息五层"), "其他云息晋层不要求关口丹药");
            data.cultivation_realm="云息七层";data.condensation_final_score=100;character.spirit_strands=144;
            Check(character.get_next_realm_progress()["required"].AsInt32()==54&&character.get_strands_until_next_realm()==54,"七层只计算本层54缕");
            character.spirit_strands=197;
            Check(!realms.can_breakthrough(character),"少一缕不能突破");
            character.spirit_strands=198;
            Check(realms.can_breakthrough(character),"本层到量才可突破");
            data.condensation_final_score=125;
            Check(realms.get_effective_requirements_for_next_realm(character)["spirit_strands"].AsInt32()==202,"凝核倍率仅作用于新增54缕并向上取整");
            data.cultivation_realm="云息八层";character.spirit_strands=202;
            Check(character.get_next_realm_progress()["current"].AsInt32()==0&&character.get_next_realm_progress()["required"].AsInt32()==64,"八层扣除前层累计量后显示本层64缕");
            data.cultivation_realm="云息九层";character.spirit_strands=266;
            Check(character.get_next_realm_progress()["required"].AsInt32()==71,"九层按倍率需要本层71缕");
            data.cultivation_realm="云息十层";data.condensation_final_score=225;
            var timingData=(HumanData)data.Duplicate(true);
            timingData.yuanxi_count=0;timingData.yuanxi_active=false;timingData.yuanxi_phase=0;timingData.yuanxi_phase_hours=0;
            var timingCharacter=new Character{entity_data=timingData,ProcessMode=ProcessModeEnum.Disabled};AddChild(timingCharacter);timingCharacter.Hide();
            Check(Math.Abs(timingCharacter.get_yuanxi_efficiency()-1d)<.0001,"三灵根通用功法正常环境养息效率为一");
            Check(timingCharacter.start_yuanxi(9),"完整九息闭关可以开始");
            var timingClock=GetNode<GameTimeMgr>("/root/GameTimeManager");
            var timingStart=timingClock.get_total_game_hours();
            timingCharacter.advance_yuanxi(9503);
            Check(timingCharacter.yuanxi_count==8&&!timingCharacter.is_yunxi_complete(),"九息最后一小时之前不提前圆满");
            timingCharacter.advance_yuanxi(1);
            Check(timingCharacter.is_yunxi_complete()&&timingClock.get_total_game_hours()-timingStart==9504,"九息实际耗时9504小时即396日");
            timingCharacter.Free();
            data.cultivation_realm="云息六层";character.spirit_strands=144;
            Check(!character.start_breakthrough_retreat(),"六至七不能用普通闭关绕过凝核");
            data.cultivation_realm="云息十层";
            Check(character.get_condensation_flat_bonus("physique") == 7.5f && character.get_condensation_flat_bonus("soul") == 7.5f && character.get_condensation_flat_bonus("intelligence") == 5 && character.get_condensation_flat_bonus("comprehension") == 0, "敏捷定额分入体魄与神魂，悟性不变");
            var attribute = character.calculate_realm_base_attribute("physique", 5);
            character.recalculate_attributes(); character.recalculate_attributes();
            Check(character.calculate_realm_base_attribute("physique", 5) == attribute, "反复重算不重复奖励");
            const string path = "res://tmp/realm_progression_smoke.tres";
            Check(ResourceSaver.Save(data, path) == Error.Ok, "临时人物序列化");
            var restored = ResourceLoader.Load<HumanData>(path, cacheMode: ResourceLoader.CacheMode.Ignore);
            Check(restored?.condensation_final_score == 225 && restored.yuanxi_count == 8, "凝核结果与息数读回");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            var panel = new RealmPanel(); AddChild(panel); panel.bind_player(character);
            Check(!panel.Visible, "绑定既有进度不弹获得动画");
            Check(character.start_yuanxi(9), "零精力仍可养息");
            data.yuanxi_phase = 2; data.yuanxi_phase_hours = character.get_yuanxi_phase_required(2) - 1;
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var before = clock.get_total_game_hours();
            character.advance_yuanxi(1);
            Check(character.is_yunxi_complete() && character.yuanxi_count == 9, "平衡完成才得九息圆满");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(panel.Visible, "圆满自动打开实际境界面板");
            var visual = Find<RealmQiVisual>(panel);
            Check(visual?.IsTransitioning == true, "圆满实际播放获得动画");
            await ToSignal(GetTree().CreateTimer(3.6), SceneTreeTimer.SignalName.Timeout);
            Check(clock.get_total_game_hours() == before + 1, "动画不额外推进时间");
            Check(FindLabel(panel, "后续　准备铸玄基"), "圆满准备方向正确");
            Check(character.cultivation_realm == "云息十层", "九息不自动进入玄基");
            if (!DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase))
                GetViewport().GetTexture().GetImage().SavePng("res://tmp/realm_completion_smoke.png");
            GD.Print("REALM_PROGRESSION_SMOKE_OK"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static T? Find<T>(Node parent) where T : Node
    {
        foreach (var node in parent.GetChildren()) { if (node is T match) return match; if (Find<T>(node) is { } nested) return nested; }
        return null;
    }
    private static bool FindLabel(Node parent, string text) => parent.GetChildren().Any(n => n is Label l && l.Text == text || FindLabel(n, text));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("境界验收失败：" + message);
        GD.Print("REALM_CHECK_OK " + message);
    }
}
