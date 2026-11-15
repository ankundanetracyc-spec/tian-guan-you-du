using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Fixed three-book loadouts and chapter stages for the isolated run's quiet rooms.</summary>
public static class RoguelikeManualRules
{
    public const string MainBookPath = "res://resources/gongfa/玄枢含章经.tres";
    public const string SharedBookPath = "res://resources/gongfa/太和感物养元宝册.tres";
    public const string QingheSwordBookPath = "res://resources/gongfa/舒卷天章.tres";
    public const string AlchemyBookPath = "res://resources/gongfa/斫基损契玄经.tres";
    public const string ForgingBookPath = "res://resources/gongfa/百骸衍枝化生宝册.tres";

    public static IReadOnlyList<string> BookPaths(string archetype)
    {
        var roleBook = archetype switch
        {
            "alchemy" => AlchemyBookPath,
            "forging" => ForgingBookPath,
            _ => QingheSwordBookPath,
        };
        return [MainBookPath, SharedBookPath, roleBook];
    }

    public static string ItemPathForBook(string bookPath)
    {
        var name = bookPath.GetFile().GetBaseName();
        return $"res://resources/entities/items/{name}秘籍.tres";
    }

    public static int ChapterStageForQuietRoom(int layer) => layer switch
    {
        1 => 1,
        13 => 2,
        25 => 3,
        >= 27 => 4,
        _ => 0,
    };

    public static bool EnsureBooks(Character player, string archetype, out string message)
    {
        message = "";
        if (player.entity_data is not HumanData human) { message = "本局角色资料不可用。"; return false; }
        human.max_sub_slots = Math.Max(2, human.max_sub_slots);
        var missing = new Dictionary<string, int>(StringComparer.Ordinal);
        var ownedPaths = player.get_accessible_spatial_inventory().Select(entry => entry.ItemPath)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var bookPath in BookPaths(archetype))
        {
            var itemPath = ItemPathForBook(bookPath);
            if (ownedPaths.Contains(itemPath)) continue;
            var item = ResourceLoader.Load<ItemData>(itemPath);
            if (item is null || !GongfaAvailability.IsItemEnabled(item))
            { message = "秘境传承暂不可用。"; return false; }
            missing[itemPath] = 1;
        }
        if (missing.Count > 0 && !player.try_exchange_inventory(new Dictionary<string, int>(), missing, announceAcquisition: true))
        { message = "行囊暂时无法收纳全部秘籍，请整理行囊后再领取。"; return false; }
        if (string.IsNullOrWhiteSpace(human.main_gongfa_path) && ResourceLoader.Load<GongfaResource>(MainBookPath) is { } mainBook)
        {
            if (!human.learned_gongfa_paths.Contains(MainBookPath)) human.learned_gongfa_paths.Add(MainBookPath);
            player.switch_main_gongfa(mainBook);
        }
        return true;
    }

    public static bool CanStudyLoadout(string archetype, out string message)
    {
        foreach (var path in BookPaths(archetype))
        {
            var book = ResourceLoader.Load<GongfaResource>(path);
            if (!GongfaAvailability.IsEnabled(book) || book!.get_sorted_chapters().Count != 4)
            { message = "本局秘籍传承尚未备齐，请稍后重试。"; return false; }
        }
        message = "";
        return true;
    }

    public static int StudyBookThroughStage(Character player, string bookPath, int chapterStage)
    {
        if (chapterStage is < 1 or > 4 || ResourceLoader.Load<GongfaResource>(bookPath) is not { } book) return 0;
        var learned = player.get_learned_gongfa_chapter_ids(book);
        var completed = 0;
        foreach (var chapter in book.get_sorted_chapters())
        {
            if (chapter.order > chapterStage || learned.Contains(chapter.get_id())) continue;
            if (!player.apply_roguelike_gongfa_chapter(book, chapter.get_id())) break;
            completed++;
        }
        if (player.entity_data is HumanData human && bookPath != human.main_gongfa_path &&
            !human.sub_gongfa_paths.Contains(bookPath))
            player.add_sub_gongfa(book);
        return completed;
    }

    public static int StudyAllThroughStage(Character player, string archetype, int chapterStage)
    {
        var total = 0;
        foreach (var bookPath in BookPaths(archetype)) total += StudyBookThroughStage(player, bookPath, chapterStage);
        return total;
    }

    /// <summary>Settles missed checkpoints in older saves using the route choices already recorded.</summary>
    public static bool ReconcilePastProgress(Character player, GDictionary instance)
    {
        if (!DynamicDungeonExpedition.IsRoguelikeInstance(instance) ||
            instance.GetValueOrDefault("map_snapshot", new GDictionary()).AsGodotDictionary()
                .GetValueOrDefault("layers", 0).AsInt32() != DynamicDungeonExpedition.RoguelikeLayerCount) return false;
        var route = DynamicDungeonExpedition.GetRoute(instance);
        var current = DynamicDungeonExpedition.GetNode(instance, route.GetValueOrDefault("current", "").AsString());
        if (current is null) return false;
        var currentLayer = DynamicDungeonExpedition.RoguelikeLayerCount - current.GridPosition.Y + 1;
        var visited = route.GetValueOrDefault("visited", new Godot.Collections.Array()).AsGodotArray();
        var actions = route.GetValueOrDefault("actions", new GDictionary()).AsGodotDictionary();
        var checkpoints = visited.Select(value => DynamicDungeonExpedition.GetNode(instance, value.AsString()))
            .Where(node => node is not null && node.ContentType == "cultivation")
            .OrderBy(node => DynamicDungeonExpedition.RoguelikeLayerForNodeId(node!.Id));
        var changed = false;
        foreach (var node in checkpoints)
        {
            var layer = DynamicDungeonExpedition.RoguelikeLayerForNodeId(node!.Id);
            if (layer >= currentLayer || layer is < 3 or > 27 || layer % 3 != 0 ||
                RealmManager.get_cloud_realm_tier(player.cultivation_realm) != layer / 3) continue;
            var path = node.Feature switch
            {
                "cultivation_physique" => "physique",
                "cultivation_intelligence" => "intelligence",
                "cultivation_soul" => "soul",
                _ => "",
            };
            if (path.Length == 0 || !player.apply_roguelike_cultivation(layer, path, out _)) continue;
            actions[node.Id + ":rogue_cultivation_" + path] = 1;
            changed = true;
        }
        if (currentLayer > 27 && RealmManager.get_cloud_realm_tier(player.cultivation_realm) == 10)
        {
            var archetype = instance.GetValueOrDefault("archetype", "sword").AsString();
            if (CanStudyLoadout(archetype, out _) && StudyAllThroughStage(player, archetype, 4) > 0)
                changed = true;
        }
        if (changed)
        {
            route["actions"] = actions;
            DynamicDungeonExpedition.StoreRoute(instance, route);
        }
        return changed;
    }

    public static int LearnedChapterCount(Character player, string bookPath)
        => ResourceLoader.Load<GongfaResource>(bookPath) is { } book
            ? player.get_learned_gongfa_chapter_ids(book).Count
            : 0;

    public static string BookName(string bookPath)
        => ResourceLoader.Load<GongfaResource>(bookPath)?.gongfa_name ?? bookPath.GetFile().GetBaseName();

    public static int BookChapterCount(string bookPath)
        => ResourceLoader.Load<GongfaResource>(bookPath)?.chapters.Count ?? 0;
}
