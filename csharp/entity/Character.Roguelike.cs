using Godot;
using System.Linq;

public partial class Character
{
    /// <summary>Completes one ordered chapter inside the isolated run's quiet room, using the chapter's existing reward data.</summary>
    public bool apply_roguelike_gongfa_chapter(GongfaResource gongfa, string chapterId)
    {
        if (human is null || gongfa is null || !GongfaAvailability.IsEnabled(gongfa) || string.IsNullOrWhiteSpace(chapterId)) return false;
        var chapter = gongfa.get_chapter_by_id(chapterId);
        if (chapter is null) return false;
        var learned = human.learned_gongfa_chapters.GetValueOrDefault(gongfa.ResourcePath, new Godot.Collections.Array<string>()).AsGodotArray();
        if (learned.Contains(chapterId)) return true;
        var next = gongfa.get_sorted_chapters().FirstOrDefault(candidate => !learned.Contains(candidate.get_id()));
        if (next is null || next.get_id() != chapterId || chapter.required_chapter_ids.Any(required => !learned.Contains(required))) return false;

        learned.Add(chapterId);
        human.learned_gongfa_chapters[gongfa.ResourcePath] = learned;
        human.gongfa_chapter_study_progress.Remove(gongfa.ResourcePath + ":" + chapterId);
        if (!human.learned_gongfa_paths.Contains(gongfa.ResourcePath)) human.learned_gongfa_paths.Add(gongfa.ResourcePath);

        var appliedBonuses = apply_gongfa_chapter_permanent_bonuses(gongfa, chapterId);
        refresh_gongfa_effects();
        refresh_shared_basic_skill_pool();
        GongfaChapterCompleted?.Invoke(this, gongfa.gongfa_name, chapterId, chapter.chapter_name, appliedBonuses);
        return true;
    }

    /// <summary>Optional meditation-room attribute growth; realm progression remains on mandatory floor gates.</summary>
    public bool apply_roguelike_meditation_attribute(string growthPath)
    {
        if (human is null) return false;
        switch (growthPath)
        {
            case "physique": base_physique += 1f; break;
            case "intelligence": base_intelligence += 1f; break;
            case "soul": base_soul += 1f; break;
            default: return false;
        }
        refresh_gongfa_effects();
        refresh_shared_basic_skill_pool();
        return true;
    }
}
