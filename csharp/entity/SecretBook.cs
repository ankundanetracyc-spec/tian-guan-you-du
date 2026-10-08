using Godot;
#pragma warning disable CS8604

/// <summary>秘籍和百艺图纸的时间驱动研读状态机。</summary>
public partial class SecretBook : Item
{
    [Export] public GongfaResource? gongfa; [Export] public Resource? craft_data; [Export] public float required_study_progress; [Export] public bool can_be_interrupted=true;
    public GodotObject? studying_human; public bool is_studying_now; public float study_start_hour,last_study_update_hour,current_study_progress; public string studying_chapter_id=""; private bool completed,cancelled;
    public override void _Ready(){base._Ready();entity_type=EntityType.ITEM;item_category="秘籍";item_subcategory=gongfa is not null?"秘籍":craft_data is not null?"阵图/丹方":"";var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");if(time is not null)time.time_changed+=on_time_changed;}
    public override void load_from_data(EntityData data){base.load_from_data(data);if(data is ItemData item){gongfa=item.gongfa as GongfaResource;craft_data=item.craft_data;required_study_progress=item.required_study_progress;}update_ui();}
    private GameTimeMgr? time()=>GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
    private void on_time_changed(){ }
    public bool start_study(GodotObject human){if(is_studying_now||human is null||human.Get("combat_drafted").AsBool()||(gongfa is null&&craft_data is null))return false;var nextChapter=next_unread_chapter(human);if(gongfa is not null&&gongfa.chapters.Count>0&&string.IsNullOrEmpty(nextChapter))return false;studying_chapter_id=nextChapter;if(gongfa is not null&&gongfa.chapters.Count>0&&select_chapter(human)!=nextChapter)return false;studying_human=human;is_studying_now=true;completed=false;cancelled=false;study_start_hour=time()?.get_total_game_hours()??0;last_study_update_hour=study_start_hour;current_study_progress=string.IsNullOrEmpty(studying_chapter_id)?0:human.HasMethod("get_gongfa_chapter_study_progress")?human.Call("get_gongfa_chapter_study_progress",gongfa,studying_chapter_id).AsSingle():0;return true;}
    public void cancel_study(){if(completed)return;cancelled=true;is_studying_now=false;studying_human=null;study_start_hour=last_study_update_hour=current_study_progress=0;studying_chapter_id="";}
    public void pause_study(){if(!is_studying_now||completed||cancelled)return;var now=time()?.get_total_game_hours()??0;advance_study_hours(Mathf.Max(now-last_study_update_hour,0));is_studying_now=false;studying_human=null;study_start_hour=0;}
    public void complete_study(){if(cancelled||completed||!is_studying_now||!GodotObject.IsInstanceValid(studying_human))return;var human=studying_human!;var completedChapter=studying_chapter_id;completed=true;is_studying_now=false;studying_human=null;studying_chapter_id="";current_study_progress=0;if(!string.IsNullOrEmpty(completedChapter)&&human.HasMethod("_complete_gongfa_chapter_study"))human.Call("_complete_gongfa_chapter_study",gongfa,completedChapter,this);else if(human.HasMethod("_complete_learning"))human.Call("_complete_learning",gongfa,this);}
    public bool is_study_complete(float _) => is_studying_now&&!cancelled&&!completed&&current_study_progress>=required_progress();
    public float get_study_progress(float _) => !is_studying_now?0:Mathf.Clamp(current_study_progress/required_progress(),0,1);
    public void advance_study_hours(float hours){if(!is_studying_now||cancelled||completed||hours<=0)return;var rate=GodotObject.IsInstanceValid(studying_human)&&studying_human!.HasMethod("get_learning_rate")?Mathf.Max(studying_human.Call("get_learning_rate").AsSingle(),.01f):1;current_study_progress+=hours*rate;if(!string.IsNullOrEmpty(studying_chapter_id)&&GodotObject.IsInstanceValid(studying_human)&&studying_human!.HasMethod("set_gongfa_chapter_study_progress"))studying_human.Call("set_gongfa_chapter_study_progress",gongfa,studying_chapter_id,current_study_progress);if(current_study_progress>=required_progress())complete_study();}
    public override void update_ui(){if(label is null)return;label.Text=gongfa?.gongfa_name??(craft_data?.ResourcePath.GetFile()??"无名秘籍");}
    /// <summary>卷章是实际研读单位；秘籍总需求只兼容没有卷章的旧资源。</summary>
    public float get_required_study_progress()=>required_progress();
    private float required_progress()
    {
        var chapter = gongfa?.get_chapter_by_id(studying_chapter_id);
        if (chapter is not null && chapter.required_study_progress > 0) return chapter.required_study_progress;
        return Mathf.Max(required_study_progress > 0 ? required_study_progress : gongfa?.required_study_progress ?? 2, .1f);
    }
    public string get_next_unread_chapter(GodotObject human){if(gongfa is null||gongfa.chapters.Count==0)return "";foreach(var chapter in gongfa.get_sorted_chapters())if(!human.Call("get_learned_gongfa_chapter_ids",gongfa).AsGodotArray().Contains(chapter.get_id()))return chapter.get_id();return "";}
    private string next_unread_chapter(GodotObject human)=>get_next_unread_chapter(human);
    private string select_chapter(GodotObject human){if(gongfa is null||gongfa.chapters.Count==0)return "";foreach(var chapter in gongfa.get_sorted_chapters()){var id=chapter.get_id();if(!human.Call("get_learned_gongfa_chapter_ids",gongfa).AsGodotArray().Contains(id)&&(!human.Call("has_learned_gongfa",gongfa).AsBool()||human.Call("can_learn_gongfa_chapter",gongfa,chapter).AsBool()))return id;}return "";}
}
#pragma warning restore CS8604
