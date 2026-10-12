using Godot;

public partial class MainScene
{
    private JournalPanel.Entry? get_active_yuxu_journal_task()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId || string.IsNullOrEmpty(human.sect_active_yuxu_duty_id)) return null;
        var (title, body, place) = human.sect_active_yuxu_duty_id switch
        {
            YuxuBitterMossRootDutyId => ("苦苔根交付", $"行囊苦苔根 {player.get_inventory_total_count(BitterMossRootItemPath)}/{YuxuSectPrototypeBalance.BitterMossRootSubmissionCount}。可在东林地探查采集；已有存货也可交付。回和光峰交两份，奖励5贡献。", "东林地 → 和光峰"),
            YuxuEastWoodsPatrolDutyId => ("东林地巡查", human.sect_active_yuxu_duty_ready ? "静冠洼地复核完成。从林缘离开后，回和光峰宗门事务处回报领取贡献。" : $"沿路线行进 {Math.Min(human.sect_active_yuxu_duty_stage, YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves)}/{YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves} 格，并在静冠洼地复核；无需探查、采集或杀怪。", "东林地 → 和光峰"),
            YuxuBeastBoneSubmissionDutyId => ("上缴兽骨", $"行囊兽骨 {Math.Min(1, player.get_inventory_total_count(BeastBoneItemPath))}/1。携带一份回和光峰事务处交付；交付才扣除物品并核发贡献。", "和光峰·宗门事务处"),
            YuxuYaopuWatchDutyId => ("瑶圃药园值守", "前往瑶圃峰完成已接取的药园值守。", "瑶圃峰"),
            YuxuYaopuCultivationDutyId => ("培育雾铃花", human.sect_active_yuxu_duty_stage > 0 ? "苗株已照料，按事务周期等待后回瑶圃峰采收。" : "前往瑶圃峰照料苗株；采收后大部分交宗门、少部分自留。", "瑶圃峰"),
            YuxuStudyDutyId => ("静室研读一卷", "在身份对应的静室完成一卷当前可研读的秘籍。", "所属静室"),
            _ => ("宗门事务", "返回和光峰查看已接取的事务要求。", "和光峰")
        };
        return IdentifyJournalTask(new(title, body, "玉虚仙宗·宗门公事", place, "进行中", Id: "yuxu.duty." + (string.IsNullOrEmpty(human.sect_active_yuxu_duty_instance_id) ? human.sect_active_yuxu_duty_id : human.sect_active_yuxu_duty_instance_id)));
    }
}
