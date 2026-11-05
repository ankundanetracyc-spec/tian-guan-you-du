using Godot;

public partial class Character
{
    /// <summary>One public first-volume license, with no gifted progress or learned effects.</summary>
    public bool try_select_baishi_first_volume(GongfaResource book)
    {
        if (human is null || book is null || !GongfaAvailability.IsEnabled(book) || !book.can_be_main) return false;
        var first = book.get_sorted_chapters().FirstOrDefault();
        if (first is null || !can_learn_gongfa_chapter(book, first)) return false;
        var oldPath = human.baishi_first_volume_path;
        if (!string.IsNullOrEmpty(oldPath))
        {
            if (oldPath == book.ResourcePath) return true;
            var old = load_gongfa(oldPath);
            var oldFirst = old?.get_sorted_chapters().FirstOrDefault();
            if (old is null || oldFirst is null || has_learned_gongfa(old) ||
                get_gongfa_chapter_study_progress(old, oldFirst.get_id()) > 0 || human.intro_gongfa_study_progress > 0)
                return false;
            foreach (var study in _study_books.Where(s => s.gongfa?.ResourcePath == oldPath).ToArray())
            { study.cancel_study(); _study_books.Remove(study); study.QueueFree(); }
        }
        // An already-owned book is never overwritten by the opening's grant.
        if (has_learned_gongfa(book)) return false;
        human.baishi_first_volume_path = book.ResourcePath;
        human.baishi_first_volume_only = true;
        human.intro_gongfa_path = book.ResourcePath;
        human.intro_gongfa_study_progress = 0;
        study_auto_next_chapter = false;
        return true;
    }
}
