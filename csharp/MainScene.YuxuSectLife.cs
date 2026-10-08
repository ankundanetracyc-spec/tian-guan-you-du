using Godot;

public partial class MainScene
{
    private const string YuxuSectId = "yuxu_xianzong";
    private const string SpiritStoneItemPath = "res://resources/entities/items/灵石.tres";
    private const string TianwenPracticeBeastPath = "res://resources/beast_templates/林地凶狼.tres";
    private const string YuxuStudyDutyId = "study_chapter";
    private const string YuxuYaopuWatchDutyId = "yaopu_watch";
    private const string YuxuYaopuCultivationDutyId = "yaopu_cultivate_mist_bell";
    private const string YuxuEastWoodsPatrolDutyId = "east_woods_patrol";
    private const string YuxuBeastBoneSubmissionDutyId = "beast_bone_submission";
    private const string MistBellFlowerItemPath = "res://resources/entities/items/雾铃花.tres";
    private const string BeastBoneItemPath = "res://resources/entities/items/兽骨.tres";
    private const int YuxuStudyDutyContributionReward = YuxuSectPrototypeBalance.OfficialStudyDutyReward;

    private string _player_current_yuxu_peak_id = "";
    private bool _yuxu_tianwen_practice_confirm_open;
    private readonly List<JournalPanel.Entry> _discovered_yuxu_peak_entries = [];

    private static string get_yuxu_peak_name(string peakId) => peakId switch
    {
        "heguang" => "和光峰",
        "guanfu" => "观复峰",
        "zifu" => "紫府峰",
        "xuanlan" => "玄览峰",
        "tianwen" => "天问峰",
        "tiangong" => "天工峰",
        "zhizhong" => "执中峰",
        "yaopu" => "瑶圃峰",
        _ => "玉虚仙宗"
    };

    private void on_yuxu_peak_selected(string peakId)
    {
        _player_current_yuxu_peak_id = peakId;
        refresh_yuxu_life_context();
        if (GetTree().GetFirstNodeInGroup("cell_character_list") is CellCharacterList roster)
            roster.show_world_context("yuxu_xianzong_core", peakId, $"interior:yuxu_xianzong:{peakId}");

        var peakInterior = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        if (peakInterior is null || !peakInterior.HasMethod("get_peak_journal_entry")) return;
        var details = peakInterior.Call("get_peak_journal_entry", peakId).AsGodotDictionary();
        var title = details.GetValueOrDefault("title", "").AsString();
        var subtitle = details.GetValueOrDefault("subtitle", "").AsString();
        var body = details.GetValueOrDefault("body", "").AsString();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body) ||
            _discovered_yuxu_peak_entries.Any(entry => entry.Title == title)) return;

        _discovered_yuxu_peak_entries.Add(new JournalPanel.Entry(
            title,
            string.IsNullOrWhiteSpace(subtitle) ? body : $"{subtitle}\n\n{body}",
            "玉虚仙宗峰内见闻",
            $"玉虚仙宗·{title}",
            "已记"));
        refresh_journal_entries();
    }
    private void clear_yuxu_peak_location() => _player_current_yuxu_peak_id = "";

    private string get_current_profile_location()
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (!string.IsNullOrEmpty(_player_current_yuxu_peak_id) && overview?.Visible == true &&
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible == true)
            return "玉虚仙宗·" + get_yuxu_peak_name(_player_current_yuxu_peak_id);
        var locationName = node_map?.map_data?.get_node(_player_world_node_id)?.node_name;
        if (!string.IsNullOrWhiteSpace(locationName)) return locationName;
        return _player_world_node_id == "home_village" ? "青溪里" : "位置未详";
    }

    public string get_profile_location_for(WorldEntity entity)
    {
        if (!ReferenceEquals(entity, player)) return "位置未详";
        var location = get_current_profile_location();
        if (string.IsNullOrWhiteSpace(location) || location == "位置未详") location = "百川";
        return $"{location} · 格位（{_player_world_grid_cell.X}, {_player_world_grid_cell.Y}）";
    }

    private static bool is_yuxu_true_disciple(string rank)
        => rank is "真传" or "真传弟子";

    private static bool is_supported_yuxu_life_rank(string rank)
        => rank is "杂役" or "杂役弟子" or "外门" or "外门弟子" or "内门" or "内门弟子" or "真传" or "真传弟子";

    private static int get_yuxu_monthly_stipend(string rank)
        => YuxuSectPrototypeBalance.monthly_stipend(rank);

    public void on_yuxu_membership_state_changed()
    {
        mark_save_dirty();
        refresh_yuxu_life_context();
    }

    private static int get_game_month_index(GameTimeMgr time)
    {
        var index = ((long)Math.Max(1, time.year) - 1L) * 12L + Math.Clamp(time.month, 1, 12) - 1L;
        return (int)Math.Clamp(index, 0L, int.MaxValue);
    }

    private void refresh_yuxu_life_context()
    {
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        if (overview is null || player?.entity_data is not HumanData human) return;
        var isMember = human.sect_affiliation_id == YuxuSectId;
        if (isMember && !string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_id) &&
            !is_supported_yuxu_duty(human.sect_active_yuxu_duty_id))
        {
            clear_active_yuxu_duty(human);
            mark_save_dirty();
        }
        overview.Call("set_life_context",
            isMember,
            human.sect_current_rank,
            get_yuxu_monthly_stipend(human.sect_current_rank),
            human.sect_unclaimed_spirit_stones);

        var peakInterior = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        peakInterior?.Call("set_sect_duty_context",
            isMember,
            isMember ? human.sect_active_yuxu_duty_id : "");
        peakInterior?.Call("set_contribution_balance",
            isMember ? human.get_yuxu_contribution_total() : 0L);
        if (_east_woods_dungeon is not null)
            _east_woods_dungeon.set_active_duty_context(
                isMember ? human.sect_active_yuxu_duty_id : "",
                isMember ? human.sect_active_yuxu_duty_stage : 0);
    }

    private static bool is_supported_yuxu_duty(string dutyId)
        => dutyId is YuxuStudyDutyId or YuxuYaopuWatchDutyId or YuxuYaopuCultivationDutyId or YuxuEastWoodsPatrolDutyId or YuxuBeastBoneSubmissionDutyId;

    private static void clear_active_yuxu_duty(HumanData human)
    {
        human.sect_active_yuxu_duty_id = "";
        human.sect_active_yuxu_duty_instance_id = "";
        human.sect_active_yuxu_duty_started_hour = -1;
        human.sect_active_yuxu_duty_stage = 0;
    }

    private void handle_yuxu_yaopu_action(string actionId, HumanData human)
    {
        if (player is null) return;
        if (actionId == "yaopu_watch")
        {
            if (human.sect_active_yuxu_duty_id != YuxuYaopuWatchDutyId)
            {
                show_message("药园值守", "先到和光峰事务值房接取药园值守，再来瑶圃峰当值。");
                return;
            }
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            var staminaCost = YuxuSectPrototypeBalance.YaopuWatchStaminaCost;
            if (time is null || !player.can_spend_world_stamina(staminaCost))
            {
                show_message("暂不能当值", time is null ? "游戏历法尚未就绪。" : $"本次值守需 {staminaCost:0.#} 点体力，当前体力不足；可休息后再来。");
                return;
            }

            run_time_transition(
                "药园值守",
                $"在瑶圃峰药园完成 {YuxuSectPrototypeBalance.YaopuWatchHours} 个游戏时辰的值守；消耗体力 {staminaCost:0.#} 点。",
                "当值结束。药架、灵土与苗圃均已巡看完毕，可向执事回报。",
                () =>
                {
                    if (player is null || player.entity_data is not HumanData current ||
                        current.sect_active_yuxu_duty_id != YuxuYaopuWatchDutyId ||
                        !player.try_spend_world_stamina(staminaCost))
                    {
                        show_message("值守未完成", "事务状态或体力已变化，未推进历法，也未发放贡献。");
                        return;
                    }
                    time.add_hours(YuxuSectPrototypeBalance.YaopuWatchHours);
                    settle_yuxu_official_duty(
                        current,
                        YuxuYaopuWatchDutyId,
                        "瑶圃药园值守",
                        current.sect_active_yuxu_duty_instance_id,
                        $"完成药园值守；消耗体力 {staminaCost:0.#} 点并当值四个游戏时辰。",
                        "药园值守已核验。",
                        null);
                });
            return;
        }

        if (actionId != "yaopu_cultivate") return;
        if (human.sect_active_yuxu_duty_id != YuxuYaopuCultivationDutyId)
        {
            show_message("培护灵植", "先到和光峰事务值房接取雾铃花培育，再来瑶圃峰照料苗株。");
            return;
        }

        var cultivationTime = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (cultivationTime is null)
        {
            show_message("培护未完成", "游戏历法尚未就绪。");
            return;
        }

        if (human.sect_active_yuxu_duty_stage <= 0 || human.sect_active_yuxu_duty_started_hour < 0)
        {
            var startHours = YuxuSectPrototypeBalance.YaopuCultivationStartHours;
            var startCost = YuxuSectPrototypeBalance.YaopuCultivationStartStaminaCost;
            if (!player.can_spend_world_stamina(startCost))
            {
                show_message("体力不足", $"照料苗株需 {startCost:0.#} 点体力，当前体力不足；可休息后再来。");
                return;
            }
            run_time_transition(
                "照料雾铃花",
                $"前往雾铃园培护一株幼苗，用时 {startHours} 个游戏时辰，消耗体力 {startCost:0.#} 点。成熟后再回来采收。",
                "幼苗已妥善照料。待其生长满一个游戏昼夜，再来采收。",
                () =>
                {
                    if (player is null || player.entity_data is not HumanData current ||
                        current.sect_active_yuxu_duty_id != YuxuYaopuCultivationDutyId ||
                        !player.try_spend_world_stamina(startCost))
                    {
                        show_message("照料未完成", "事务状态或体力已变化，未推进历法。");
                        return;
                    }
                    cultivationTime.add_hours(startHours);
                    current.sect_active_yuxu_duty_started_hour = cultivationTime.get_total_game_hours();
                    current.sect_active_yuxu_duty_stage = 1;
                    mark_save_dirty();
                    refresh_yuxu_life_context();
                });
            return;
        }

        var elapsed = Math.Max(0, cultivationTime.get_total_game_hours() - human.sect_active_yuxu_duty_started_hour);
        var required = YuxuSectPrototypeBalance.YaopuCultivationHours;
        if (elapsed < required)
        {
            var remaining = required - elapsed;
            show_message("雾铃花尚未成熟", $"苗株已生长 {elapsed} / {required} 个游戏时辰；还需 {remaining} 个游戏时辰。你可以先处理其他事务，之后再回来。");
            return;
        }

        var yieldCount = YuxuSectPrototypeBalance.YaopuCultivationYieldCount;
        var deliveryCount = YuxuSectPrototypeBalance.YaopuCultivationSectDeliveryCount;
        var keptCount = Math.Max(0, yieldCount - deliveryCount);
        if (!player.add_item_to_inventory(MistBellFlowerItemPath, yieldCount))
        {
            show_message("行囊已满", $"雾铃花已成熟，但行囊无法容纳 {yieldCount} 株；整理行囊后再来采收。任务进度会保留。");
            return;
        }
        if (!player.remove_item_from_inventory(MistBellFlowerItemPath, deliveryCount))
        {
            player.remove_item_from_inventory(MistBellFlowerItemPath, yieldCount);
            show_message("交收未完成", "雾铃花交收数量发生变化，采收已撤回；事务进度保留，可稍后重试。");
            return;
        }
        if (!settle_yuxu_official_duty(
                human,
                YuxuYaopuCultivationDutyId,
                "培育雾铃花",
                human.sect_active_yuxu_duty_instance_id,
                $"培护并采收雾铃花 ×{yieldCount}；交宗门 ×{deliveryCount}，弟子自留 ×{keptCount}；等待二十四个游戏时辰。",
                $"雾铃花 ×{deliveryCount} 已交宗门，×{keptCount} 留归弟子。",
                rollback: () => player?.remove_item_from_inventory(MistBellFlowerItemPath, keptCount),
                rewardOverride: YuxuSectPrototypeBalance.OfficialYaopuCultivationReward,
                itemMessage: $"雾铃花 ×{keptCount} 已留入行囊。 "))
            return;
    }

    private void on_east_woods_route_node_entered(string nodeId)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            human.sect_active_yuxu_duty_id != YuxuEastWoodsPatrolDutyId)
            return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null)
        {
            show_message("巡线暂停", "游戏历法尚未就绪，本次节点行进没有计入巡线进度。");
            return;
        }
        human.sect_active_yuxu_duty_stage++;
        mark_save_dirty();
        refresh_yuxu_life_context();
        if (nodeId != "quiet_grove") return;
        if (human.sect_active_yuxu_duty_stage < YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves)
        {
            show_message("巡线尚未完成", "已到静冠洼地，但标定路线尚未走完；继续按路线巡回后再回此处复核。");
            return;
        }
        settle_yuxu_official_duty(
                human,
                YuxuEastWoodsPatrolDutyId,
                "东林地巡查",
                human.sect_active_yuxu_duty_instance_id,
                $"沿标定路线推进 {human.sect_active_yuxu_duty_stage} 个节点并完成复核；节点耗时按基础值与当前遁速结算，不消耗物资。",
                "巡线各处未见异常，记录已回报。",
                null);
    }

    private bool settle_yuxu_official_duty(
        HumanData human,
        string dutyId,
        string title,
        string transactionSuffix,
        string costSummary,
        string resultSummary,
        Action? rollback = null,
        int rewardOverride = 0,
        string itemMessage = "")
    {
        if (player is null) return false;
        if (human.sect_active_yuxu_duty_id != dutyId)
        {
            rollback?.Invoke();
            return false;
        }
        var reward = rewardOverride > 0 ? rewardOverride : YuxuSectPrototypeBalance.OfficialYaopuDutyReward;
        var record = new YuxuSectContributionRecord
        {
            record_id = Guid.NewGuid().ToString("N"),
            source_transaction_id = $"yuxu:official:{dutyId}:{(string.IsNullOrWhiteSpace(transactionSuffix) ? Guid.NewGuid().ToString("N") : transactionSuffix)}",
            transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward,
            source_title = $"宗门公事·{title}",
            occurred_at = get_yuxu_contribution_timestamp(),
            contribution_delta = reward,
            cost_summary = costSummary,
            result_summary = $"{resultSummary} 核发 {reward} 点贡献。"
        };
        if (!human.record_yuxu_contribution(record))
        {
            rollback?.Invoke();
            show_message("事务未结算", "这项事务的流水无法登记；物品奖励已回退，未发放贡献。可检查事务状态后再试。");
            return false;
        }
        clear_active_yuxu_duty(human);
        mark_save_dirty();
        refresh_yuxu_life_context();
        var itemText = itemMessage;
        show_message("事务完成", $"{itemText}{resultSummary}宗门核发 {reward} 点贡献，已记入个人档案。");
        return true;
    }

    private void update_yuxu_monthly_allowance()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId)
            return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null) return;

        var currentMonth = get_game_month_index(time);
        var previousMonthCursor = human.sect_allowance_month_cursor;
        var previousBalance = human.sect_unclaimed_spirit_stones;
        if (human.sect_allowance_month_cursor < 0 || currentMonth < human.sect_allowance_month_cursor)
        {
            // 旧记录首次接入时不追溯发放；只从当前游戏月开始累计。
            human.sect_allowance_month_cursor = currentMonth;
        }
        else if (currentMonth > human.sect_allowance_month_cursor)
        {
            var elapsedMonths = (long)currentMonth - human.sect_allowance_month_cursor;
            var accrued = elapsedMonths * get_yuxu_monthly_stipend(human.sect_current_rank);
            human.sect_unclaimed_spirit_stones = (int)Math.Min(int.MaxValue, (long)Math.Max(0, human.sect_unclaimed_spirit_stones) + accrued);
            human.sect_allowance_month_cursor = currentMonth;
        }
        if (previousMonthCursor != human.sect_allowance_month_cursor || previousBalance != human.sect_unclaimed_spirit_stones)
            mark_save_dirty();
        refresh_yuxu_life_context();
    }

    private void on_yuxu_life_action_requested(string actionId, string peakId)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            !is_supported_yuxu_life_rank(human.sect_current_rank))
            return;

        if (peakId == "heguang")
        {
            handle_yuxu_heguang_action(actionId, human);
            return;
        }

        if (peakId == "yaopu" && (actionId is "yaopu_watch" or "yaopu_cultivate"))
        {
            handle_yuxu_yaopu_action(actionId, human);
            return;
        }

        if (peakId == "tianwen" && actionId == "tianwen_practice")
        {
            request_yuxu_tianwen_practice();
            return;
        }

        var isTrueDisciple = is_yuxu_true_disciple(human.sect_current_rank);
        var correctPeak = isTrueDisciple ? "zifu" : "guanfu";
        if (peakId != correctPeak) return;

        update_yuxu_monthly_allowance();
        switch (actionId)
        {
            case "meditation":
                cultivation_ui?.show_entity(player);
                break;
            case "rest":
                request_yuxu_rest(isTrueDisciple ? "紫府峰居所" : "观复峰居所");
                break;
            case "teaching":
                cultivation_ui?.show_teaching_for(player);
                break;
            case "claim_allowance":
                claim_yuxu_monthly_allowance(human);
                break;
            case "promotion":
                if (peakId == "guanfu") request_yuxu_rank_promotion(human);
                break;
        }
    }

    private void handle_yuxu_heguang_action(string actionId, HumanData human)
    {
        const string submitDutyPrefix = "heguang_submit_duty:";
        if (actionId.StartsWith(submitDutyPrefix, StringComparison.Ordinal))
        {
            var dutyId = actionId[submitDutyPrefix.Length..];
            if (dutyId != YuxuBeastBoneSubmissionDutyId || human.sect_active_yuxu_duty_id != dutyId) return;
            if (player is null || !player.has_inventory_item(BeastBoneItemPath))
            {
                show_message("尚缺交付物", "上缴兽骨需携带一份现有物品“兽骨”；可通过击败已有妖兽取得，再回此处提交。");
                return;
            }
            if (!player.remove_item_from_inventory(BeastBoneItemPath))
            {
                show_message("提交未完成", "兽骨数量发生变化，未扣除物品或发放贡献。");
                return;
            }
            settle_yuxu_official_duty(
                human,
                YuxuBeastBoneSubmissionDutyId,
                "上缴兽骨",
                human.sect_active_yuxu_duty_instance_id,
                "提交现有妖兽掉落物兽骨 ×1，由宗门登记。",
                "兽骨已收讫并完成登记。",
                rollback: () => player?.add_item_to_inventory(BeastBoneItemPath),
                rewardOverride: YuxuSectPrototypeBalance.OfficialBeastDropSubmissionReward,
                itemMessage: "兽骨 ×1 已交宗门。 ");
            return;
        }

        const string acceptDutyPrefix = "heguang_accept_duty:";
        if (actionId.StartsWith(acceptDutyPrefix, StringComparison.Ordinal))
        {
            var dutyId = actionId[acceptDutyPrefix.Length..];
            if (!is_supported_yuxu_duty(dutyId)) return;
            if (dutyId == YuxuStudyDutyId && !has_available_yuxu_study_chapter())
            {
                refresh_yuxu_life_context();
                show_message("暂不可接", "你目前没有符合条件、尚未研读的秘籍章节。取得或安排可研读的秘籍后，再来承接这项事务。");
                return;
            }
            if (!string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_id) && human.sect_active_yuxu_duty_id != dutyId)
            {
                show_message("宗门事务", "你已有一项事务在办；可先打开该事务并取消接取。");
                return;
            }
            if (!string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_id))
            {
                refresh_yuxu_life_context();
                return;
            }
            human.sect_active_yuxu_duty_id = dutyId;
            human.sect_active_yuxu_duty_instance_id = Guid.NewGuid().ToString("N");
            human.sect_active_yuxu_duty_started_hour = -1;
            human.sect_active_yuxu_duty_stage = 0;
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("接取事务", dutyId switch
            {
                YuxuStudyDutyId => "已接取静室研习。按弟子身份前往所属静室，完成一卷当前可研读的秘籍即可交由宗门核验；可随时回和光峰取消。",
                YuxuYaopuWatchDutyId => "已接取药园值守。前往瑶圃峰，在药园当值四个游戏时辰；完成后核发贡献。",
                YuxuYaopuCultivationDutyId => "已接取雾铃花培育。前往瑶圃峰照料苗株，等待一个游戏昼夜后采收；四株中三株交宗门，一株自留。",
                YuxuEastWoodsPatrolDutyId => "已接取东林地巡查。进入东林地副本，沿路线至少推进四个节点，并在静冠洼地复核；节点耗时按当前遁速动态结算，不采集资源。",
                YuxuBeastBoneSubmissionDutyId => "已接取兽骨上缴。携带一份现有妖兽掉落物兽骨，回到此处打开详情并提交。",
                _ => "已接取宗门事务。"
            });
            return;
        }

        const string cancelDutyPrefix = "heguang_cancel_duty:";
        if (actionId.StartsWith(cancelDutyPrefix, StringComparison.Ordinal))
        {
            var dutyId = actionId[cancelDutyPrefix.Length..];
            if (human.sect_active_yuxu_duty_id != dutyId) return;
            clear_active_yuxu_duty(human);
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("取消接取", "已取消这项事务。贡献和物品均未发生变化。");
            return;
        }
    }

    private bool has_available_yuxu_study_chapter()
    {
        if (player is null) return false;
        var books = player.get_known_gongfas();
        var currentBook = player.get_current_study_gongfa();
        if (currentBook is not null && !books.Any(book => book.ResourcePath == currentBook.ResourcePath))
            books.Add(currentBook);

        foreach (var book in books)
        {
            if (book is null) continue;
            foreach (var chapter in book.get_sorted_chapters())
            {
                if (chapter is null || player.is_gongfa_chapter_learned(book, chapter.get_id())) continue;
                if (player.can_learn_gongfa_chapter(book, chapter)) return true;
            }
        }
        return false;
    }

    private void on_yuxu_gongfa_chapter_completed(Character character, string bookName, string chapterId, string chapterName)
    {
        if (character != player || player?.entity_data is not HumanData human ||
            human.sect_affiliation_id != YuxuSectId || human.sect_active_yuxu_duty_id != YuxuStudyDutyId)
            return;

        var expectedPeak = is_yuxu_true_disciple(human.sect_current_rank) ? "zifu" : "guanfu";
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (_player_current_yuxu_peak_id != expectedPeak || overview?.Visible != true ||
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true)
            return;

        var bookPath = player.get_known_gongfas()
            .FirstOrDefault(book => book is not null && book.gongfa_name == bookName)?.ResourcePath ?? bookName;
        var transactionId = $"yuxu:official:{YuxuStudyDutyId}:{bookPath}:{chapterId}";
        var record = new YuxuSectContributionRecord
        {
            record_id = Guid.NewGuid().ToString("N"),
            source_transaction_id = transactionId,
            transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward,
            source_title = $"宗门公事·静室研习《{bookName}》·{chapterName}",
            occurred_at = get_yuxu_contribution_timestamp(),
            contribution_delta = YuxuStudyDutyContributionReward,
            cost_summary = "未消耗物品；完成一卷静室研读。",
            result_summary = $"研读完成并经宗门事务核验，核发{YuxuStudyDutyContributionReward}点贡献。"
        };

        clear_active_yuxu_duty(human);
        if (!human.record_yuxu_contribution(record))
        {
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("事务已核验", "这卷研读对应的贡献流水已经存在，本次不会重复发放。");
            return;
        }

        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("事务完成", $"《{bookName}·{chapterName}》研读完成，宗门核发 {YuxuStudyDutyContributionReward} 点贡献。贡献已记入个人档案。");
    }

    private static string get_yuxu_contribution_timestamp()
    {
        if (Engine.GetMainLoop() is SceneTree tree && tree.Root.GetNodeOrNull<GameTimeMgr>("GameTimeManager") is { } time)
            return $"{time.year}年{time.month}月{time.day}日 {time.hour:D2}时";
        return "游戏时间未载";
    }

    private void request_yuxu_rest(string restPlace)
        => open_general_rest_dialog(restPlace, update_yuxu_monthly_allowance);

    private void claim_yuxu_monthly_allowance(HumanData human)
    {
        var amount = Math.Max(0, human.sect_unclaimed_spirit_stones);
        if (amount <= 0)
        {
            show_message("月例", "当前没有尚未领取的月例。月例按月累积，未领部分不会清空，也不生利息。");
            return;
        }
        if (player is null || !player.add_item_to_inventory(SpiritStoneItemPath, amount))
        {
            show_message("行囊已满", "灵石未能放入行囊，未领取月例仍完整保留。");
            return;
        }
        human.sect_unclaimed_spirit_stones = 0;
        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("领取月例", $"你领取了 {amount} 枚灵石。灵石已放入行囊，可用于交易。");
    }

    private void request_yuxu_rank_promotion(HumanData human)
    {
        var rule = YuxuSectPrototypeBalance.promotion_rule(human.sect_current_rank);
        if (rule is null)
        {
            show_message("身份晋升", "当前身份没有开放的晋升路径。真传只由天灵根入门取得，不可通过晋升成为。");
            return;
        }

        var service = human.get_yuxu_official_service_total();
        var realmManager = GetNodeOrNull<RealmManager>("/root/RealmManager");
        var realmReady = realmManager?.is_realm_after_or_equal(player?.cultivation_realm ?? "", rule.RequiredRealm) == true;
        var serviceReady = service >= rule.RequiredOfficialService;
        var progress = $"当前身份：{human.sect_current_rank}　→　晋升后：{rule.TargetRank}\n" +
            $"境界：{player?.cultivation_realm ?? "未详"} / 需达到 {rule.RequiredRealm}\n" +
            $"累计公事履职贡献：{service} / {rule.RequiredOfficialService}\n\n" +
            "晋升只核验境界与已完成宗门公事的累计履职记录；兑换花费不会减少履职累计。以上门槛为首轮测试值。";
        if (!realmReady || !serviceReady)
        {
            show_message("晋升条件", progress + "\n\n条件尚未齐备，可继续修行并完成宗门事务。");
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = "申请晋升",
            DialogText = progress + "\n\n是否向观复峰登记晋升？",
            OkButtonText = "确认申请",
            CancelButtonText = "暂不申请"
        };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(dialog);
        dialog.Confirmed += () =>
        {
            dialog.GetParent()?.RemoveChild(dialog);
            dialog.QueueFree();
            if (player?.entity_data is not HumanData current || current.sect_affiliation_id != YuxuSectId ||
                current.sect_current_rank != human.sect_current_rank)
                return;

            var currentRule = YuxuSectPrototypeBalance.promotion_rule(current.sect_current_rank);
            var currentRealmManager = GetNodeOrNull<RealmManager>("/root/RealmManager");
            if (currentRule is null || currentRule.TargetRank != rule.TargetRank ||
                currentRealmManager?.is_realm_after_or_equal(player.cultivation_realm, currentRule.RequiredRealm) != true ||
                current.get_yuxu_official_service_total() < currentRule.RequiredOfficialService)
            {
                show_message("晋升未完成", "确认时晋升条件已发生变化，请重新查看当前要求。");
                return;
            }

            update_yuxu_monthly_allowance();
            current.sect_current_rank = currentRule.TargetRank;
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("晋升登记", $"观复峰已登记你的新身份：{currentRule.TargetRank}。入门起始身份与既有履职记录均予保留。");
        };
        dialog.Canceled += () => dialog.QueueFree();
        dialog.PopupCentered();
    }

    private void on_yuxu_exchange_purchase_requested(string itemPath, int requestedCost)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId)
        {
            resolve_yuxu_exchange_purchase(false, 0, "只有玉虚仙宗成员可以使用善功堂。");
            return;
        }

        var item = GD.Load<ItemData>(itemPath);
        if (item is null || !YuxuSectPrototypeBalance.is_exchangeable_item(item))
        {
            resolve_yuxu_exchange_purchase(false, Math.Max(0L, human.get_yuxu_contribution_total()), "这件物品不在善功堂现行目录中，贡献未扣除。");
            return;
        }

        var expectedCost = YuxuSectPrototypeBalance.exchange_base_cost(item);
        if (expectedCost <= 0 || requestedCost != expectedCost)
        {
            resolve_yuxu_exchange_purchase(false, Math.Max(0L, human.get_yuxu_contribution_total()), "物品标准价已变化，请重新打开善功堂确认，贡献未扣除。");
            return;
        }

        var balance = human.get_yuxu_contribution_total();
        if (balance < expectedCost)
        {
            resolve_yuxu_exchange_purchase(false, Math.Max(0L, balance), $"贡献不足；需要 {expectedCost} 点，当前为 {Math.Max(0L, balance)} 点。");
            return;
        }

        if (player is null || !player.add_item_to_inventory(itemPath))
        {
            resolve_yuxu_exchange_purchase(false, Math.Max(0L, balance), "行囊空间不足，物品未兑换，贡献未扣除。");
            return;
        }

        var transactionId = $"yuxu:exchange:{Guid.NewGuid():N}";
        var record = new YuxuSectContributionRecord
        {
            record_id = Guid.NewGuid().ToString("N"),
            source_transaction_id = transactionId,
            transaction_kind = YuxuSectContributionRecord.KindExchangeSpend,
            source_title = $"善功堂兑换·{item.entity_name}",
            occurred_at = get_yuxu_contribution_timestamp(),
            contribution_delta = -expectedCost,
            cost_summary = $"支出 {expectedCost} 点贡献；未消耗灵石。",
            result_summary = $"{item.entity_name} ×1 已放入行囊；累计公事履职贡献不变。"
        };

        if (!human.record_yuxu_contribution(record))
        {
            player.remove_item_from_inventory(itemPath);
            resolve_yuxu_exchange_purchase(false, Math.Max(0L, balance), "贡献流水未能登记，兑换已撤回且未扣除贡献。");
            return;
        }

        mark_save_dirty();
        refresh_yuxu_life_context();
        var updatedBalance = Math.Max(0L, human.get_yuxu_contribution_total());
        var displayName = item.item_category == "秘籍" ? $"《{item.entity_name}》" : item.entity_name;
        resolve_yuxu_exchange_purchase(true, updatedBalance, $"{displayName} ×1 已放入行囊，扣除 {expectedCost} 点贡献。累计公事履职记录不变。");
    }

    private void resolve_yuxu_exchange_purchase(bool success, long balance, string message)
    {
        var overview = GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview");
        overview?.Call("resolve_exchange_purchase", success, balance, message);
    }

    private void request_yuxu_tianwen_practice()
    {
        if (_yuxu_tianwen_practice_confirm_open || player is null) return;
        var dialog = new ConfirmationDialog
        {
            Title = "天问峰 · 演法台",
            DialogText = "以当前人物与技能配置，和按现有兽种资料生成的林地凶狼（凡兽）进行一场演练。战斗沿用正式战斗规则，战斗结果会照常回传；本场不掉落物品，也不结算宗门贡献。战斗中按 Esc 可结束并返回峰内。",
            OkButtonText = "开始演练",
            CancelButtonText = "暂不前往"
        };
        _yuxu_tianwen_practice_confirm_open = true;
        GetNode<CanvasLayer>("CanvasLayer").AddChild(dialog);
        dialog.Confirmed += () =>
        {
            _yuxu_tianwen_practice_confirm_open = false;
            dialog.GetParent()?.RemoveChild(dialog);
            dialog.QueueFree();
            start_yuxu_tianwen_practice();
        };
        dialog.Canceled += () =>
        {
            _yuxu_tianwen_practice_confirm_open = false;
            dialog.GetParent()?.RemoveChild(dialog);
            dialog.QueueFree();
        };
        dialog.PopupCentered();
    }

    private void start_yuxu_tianwen_practice()
    {
        if (player is null || GetTree().CurrentScene is null) return;
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var playerScene = player.pack_for_battle();
        var template = ResourceLoader.Load<BeastTemplate>(TianwenPracticeBeastPath);
        if (transition is null || playerScene is null || template is null)
        {
            show_message("演法未能开始", "战斗场景、主角或林地凶狼的现有配置未能载入；没有消耗物品或贡献。");
            return;
        }

        // 临时加入场景仅为复用正式 Beast 初始化与战斗快照；打包后立即移除，
        // 演练对象不会留在峰内或进入世界人物/NPC 数据。
        var opponent = new Beast
        {
            Name = "TianwenPracticeOpponent",
            entity_name = "林地凶狼",
            unique_id = $"temp_enemy_tianwen_{GD.Randi()}",
            faction = (int)WorldEntity.Faction.ENEMY,
            beast_template = template,
            entity_data = new LivingData(),
            Visible = false
        };
        AddChild(opponent);
        var opponentScene = opponent.pack_for_battle();
        var opponentSnapshot = opponent.get_snapshot_data();
        RemoveChild(opponent);
        opponent.Free();

        if (opponentScene is null || opponentSnapshot is null)
        {
            show_message("演法未能开始", "林地凶狼的演练快照未能建立；没有消耗物品或贡献。");
            return;
        }

        transition.set_battle_return_context(new Godot.Collections.Dictionary
        {
            ["kind"] = "yuxu_tianwen_practice"
        });
        transition.start_team_battle(
            new Godot.Collections.Array<PackedScene> { playerScene },
            new Godot.Collections.Array<PackedScene> { opponentScene },
            new Godot.Collections.Array { player.get_snapshot_data() },
            new Godot.Collections.Array { opponentSnapshot },
            GetTree().CurrentScene.SceneFilePath);
    }

    public void show_yuxu_tianwen_practice_result(Godot.Collections.Dictionary summary)
    {
        var attackersAlive = summary.GetValueOrDefault("attackers_alive", 0).AsInt32();
        var defendersAlive = summary.GetValueOrDefault("defenders_alive", 0).AsInt32();
        var outcome = summary.GetValueOrDefault("player_victory", false).AsBool()
            ? "你在本场演法中取胜。"
            : summary.GetValueOrDefault("is_draw", false).AsBool()
                ? "双方同时倒下，本场演法以平局结束。"
                : attackersAlive > 0 && defendersAlive > 0
                    ? "本场演法已由你提前结束。"
                    : "本场演法结束，你未能取胜。";
        show_message("演法结束", outcome + "本场不发放物品或宗门贡献。");
    }
}
