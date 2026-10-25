using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Independent local recipe notebook; no inventory or character state.</summary>
public static class AlchemyRecipeStore
{
    public sealed record Entry(string MaterialId, int Dose, int Slot);
    public sealed record Recipe(string Id, string Name, Entry[] Ingredients, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int PillCount = 0);
    public sealed record Notebook(int Version, List<Recipe> Recipes);
    public static List<Recipe> Load(string path)
    {
        if (!File.Exists(path)) return [];
        var book = JsonSerializer.Deserialize<Notebook>(File.ReadAllText(path))
            ?? throw new InvalidDataException("丹方文件为空。");
        if (book.Version != 1 || book.Recipes is null)
            throw new InvalidDataException("不支持的丹方文件版本。");
        Validate(book.Recipes);
        return book.Recipes.Select(r => r with { PillCount = 0 }).ToList();
    }
    public static void Save(string path, List<Recipe> recipes)
    {
        Validate(recipes);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Notebook(1, recipes.Select(r => r with { PillCount = 0 }).ToList()),
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
    public static void Validate(List<Recipe> recipes)
    {
        if (recipes.Any(r => r is null || string.IsNullOrWhiteSpace(r.Id) ||
            string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 40 || r.Ingredients is null))
            throw new InvalidDataException("丹方名称或内容无效。");
        if (recipes.Select(r => r.Id).Distinct().Count() != recipes.Count ||
            recipes.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count() != recipes.Count)
            throw new InvalidDataException("丹方存在重复名称或编号。");
        foreach (var r in recipes)
        {
            var inputs = r.Ingredients;
            if (inputs.Length is < 1 or > 8 || inputs.Any(i => i is null ||
                string.IsNullOrWhiteSpace(i.MaterialId) || i.Dose < 1 || i.Slot is < -1 or > 6) ||
                inputs.Count(i => i.Slot == -1) != 1 ||
                inputs.Select(i => i.MaterialId).Distinct().Count() != inputs.Length ||
                inputs.Select(i => i.Slot).Distinct().Count() != inputs.Length)
                throw new InvalidDataException("丹方材料、药位或份数无效。");
        }
    }
}
