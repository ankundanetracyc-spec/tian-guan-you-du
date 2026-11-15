using Godot;

public partial class InfoPanel
{
    /// <summary>The lecture uses the same character/manual picker and detail popup as equipment and study.</summary>
    public void show_baishi_first_volume_picker(Character reader, Func<GongfaResource, string> requirements, Action<GongfaResource> confirm)
    {
        show_entity(reader);
        show_page(3);
        var books = GongfaAvailability.EnabledBooks().Where(b => b.can_be_main).ToArray();
        void ListBooks()
        {
            CharacterChoose("白石 · 选读首卷", books.Select(book => book.gongfa_name), name =>
            {
                var book = books.First(b => b.gongfa_name == name);
                var first = book.get_sorted_chapters().FirstOrDefault();
                CharacterClear(_gongfaModalChoices!);
                _gongfaModalTitle!.Text = "《" + book.gongfa_name + "》";
                _gongfaModalBody!.Text = book.description;
                var details = CharacterText($"首卷：{first?.chapter_name}\n{first?.description}\n\n{requirements(book)}\n\n本期仅支持一部首卷，后续卷册另寻来源。", 20);
                details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _gongfaModalChoices!.AddChild(details);
                var choose = CharacterButton("选读这一卷");
                choose.Disabled = first is null || !reader.can_learn_gongfa_chapter(book, first);
                _gongfaModalChoices.AddChild(choose);
                var confirming = false;
                choose.Pressed += () =>
                {
                    if (!confirming) { confirming = true; choose.Text = "确认选读这一卷"; return; }
                    if (first is null || !reader.can_learn_gongfa_chapter(book, first)) return;
                    choose.Disabled = true;
                    confirm(book);
                    if (GodotObject.IsInstanceValid(choose)) choose.Disabled = false;
                };
                var back = CharacterButton("返回书目"); back.Pressed += ListBooks; _gongfaModalChoices.AddChild(back);
                _gongfaModal!.Show(); _gongfaModal.MoveToFront();
            });
        }
        ListBooks();
    }
}
