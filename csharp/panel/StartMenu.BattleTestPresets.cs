using Godot;
using System.Text.Json;

public partial class StartMenu
{
    private const string BattlePresetsPath = "user://battle_test_presets.json";
    private sealed class TestSidePreset
    {
        public string Deck { get; set; } = "";
        public string MainBook { get; set; } = "";
        public string SubBook { get; set; } = "";
        public string[] Skills { get; set; } = [];
        public bool CustomResources { get; set; }
        public int StaminaCap { get; set; } = 6;
        public int ManaCap { get; set; }
    }
    private sealed class TestBattlePreset
    {
        public string Name { get; set; } = "";
        public TestSidePreset Player { get; set; } = new();
        public TestSidePreset Enemy { get; set; } = new();
        public int EnemyKind { get; set; }
        public int EnemyCount { get; set; } = 1;
    }
    private sealed class TestPresetFile
    {
        public int Version { get; set; } = 1;
        public List<TestBattlePreset> Presets { get; set; } = [];
    }

    private void AddBattleTestPresets(VBoxContainer root,TestLoadoutControls player,TestLoadoutControls enemy,
        OptionButton enemyKind,OptionButton enemyCount,(string name,string path)[] books,TestSkillOption[] skills)
    {
        var status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=Ink()};
        TestPresetFile? file;
        try
        {
            var path=ProjectSettings.GlobalizePath(BattlePresetsPath);
            file=System.IO.File.Exists(path)
                ? JsonSerializer.Deserialize<TestPresetFile>(System.IO.File.ReadAllText(path))
                : new TestPresetFile();
            if(file is null || file.Version!=1 || file.Presets is null ||
                file.Presets.Any(p=>p is null || p.Player is null || p.Enemy is null))
                throw new System.IO.InvalidDataException("方案文件版本或内容无效");
        }
        catch(Exception ex)
        {
            status.Text="对练方案读取失败，原文件已保留："+ex.Message;
            root.AddChild(status);return;
        }
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",8);root.AddChild(row);
        // 插入双方配置上方，进入测试前即可发现存读入口。
        root.MoveChild(row,2);
        var select=new OptionButton{CustomMinimumSize=new Vector2(180,38),SizeFlagsHorizontal=SizeFlags.ExpandFill,ClipText=true};row.AddChild(select);
        var name=new LineEdit{PlaceholderText="对练方案名称",MaxLength=40,CustomMinimumSize=new Vector2(180,38),SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(name);
        var load=PlainButton("载入");row.AddChild(load);
        var save=PlainButton("保存方案");row.AddChild(save);
        root.AddChild(status);root.MoveChild(status,3);
        status.Text="保存双方配置，重启后仍可载入；同名保存会覆盖，旧文件另留备份。";
        void Refresh(string selected="")
        {
            select.Clear();
            foreach(var preset in file.Presets)
            {
                select.AddItem(preset.Name);
                if(preset.Name==selected)select.Select(select.ItemCount-1);
            }
            load.Disabled=file.Presets.Count==0;
            if(file.Presets.Count==0)select.AddItem("暂无对练方案");
        }
        select.ItemSelected+=i=>{if(i<file.Presets.Count)name.Text=file.Presets[(int)i].Name;};
        save.Pressed+=()=>
        {
            var title=name.Text.Trim();
            if(title.Length==0){status.Text="请先填写方案名称。";return;}
            var preset=new TestBattlePreset{Name=title,Player=CaptureTestSide(player),Enemy=CaptureTestSide(enemy),
                EnemyKind=enemyKind.Selected,EnemyCount=enemyCount.Selected+1};
            var next=new TestPresetFile{Presets=file.Presets.Where(p=>p.Name!=title).Append(preset).ToList()};
            try
            {
                var path=ProjectSettings.GlobalizePath(BattlePresetsPath);
                System.IO.File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next,new JsonSerializerOptions{WriteIndented=true}));
                if(System.IO.File.Exists(path))System.IO.File.Copy(path,path+".bak",true);
                System.IO.File.Move(path+".tmp",path,true);
                file=next;Refresh(title);status.Text="已保存："+title;
            }
            catch(Exception ex){status.Text="保存失败，原方案保留："+ex.Message;}
        };
        load.Pressed+=()=>
        {
            if(select.Selected<0 || select.Selected>=file.Presets.Count)return;
            var preset=file.Presets[select.Selected];
            var missing=RestoreTestSide(player,preset.Player,books,skills)+RestoreTestSide(enemy,preset.Enemy,books,skills);
            enemyKind.Select(Mathf.Clamp(preset.EnemyKind,0,enemyKind.ItemCount-1));
            enemyCount.Select(Mathf.Clamp(preset.EnemyCount-1,0,enemyCount.ItemCount-1));
            SetTestLoadoutEnabled(enemy,enemyKind.Selected==0);
            name.Text=preset.Name;status.Text="已载入："+preset.Name+(missing>0?$"；{missing}项资源已失效，已清空对应选择，请重新配置。":"");
        };
        Refresh();
    }

    private static TestSidePreset CaptureTestSide(TestLoadoutControls controls)
    {
        controls.StaminaCap.Apply();controls.ManaCap.Apply();
        return new TestSidePreset{Deck=SelectedPath(controls.Deck,TestDeckOptions()),
            MainBook=SelectedPath(controls.MainBook,TestBookOptions()),SubBook=SelectedPath(controls.SubBook,TestBookOptions()),
            Skills=SelectedSkillPaths(controls.Skills),CustomResources=controls.CustomResources.ButtonPressed,
            StaminaCap=(int)controls.StaminaCap.Value,ManaCap=(int)controls.ManaCap.Value};
    }

    private static int RestoreTestSide(TestLoadoutControls controls,TestSidePreset data,
        (string name,string path)[] books,TestSkillOption[] skills)
    {
        var missing=0;
        void SelectPath(OptionButton target,(string name,string path)[] options,string path)
        {
            var index=System.Array.FindIndex(options,p=>p.path==path);
            target.Select(Math.Max(0,index));
            if(index<0&&!string.IsNullOrWhiteSpace(path))missing++;
        }
        SelectPath(controls.Deck,TestDeckOptions(),data.Deck);
        SelectPath(controls.MainBook,books,data.MainBook);SelectPath(controls.SubBook,books,data.SubBook);
        for(var i=0;i<controls.Skills.Length;i++)
        {
            var path=data.Skills is not null&&i<data.Skills.Length?data.Skills[i]:"";
            var index=System.Array.FindIndex(skills,s=>s.Path==path);
            controls.Skills[i].Select(index+1);
            if(index<0&&!string.IsNullOrWhiteSpace(path))missing++;
        }
        controls.CustomResources.ButtonPressed=data.CustomResources;
        controls.StaminaCap.Value=Math.Max(0,data.StaminaCap);controls.ManaCap.Value=Math.Max(0,data.ManaCap);
        controls.StaminaCap.Editable=data.CustomResources;controls.ManaCap.Editable=data.CustomResources;
        RefreshTestLoadoutChoices(controls.MainBook,controls.SubBook,controls.Skills,books,skills);
        return missing;
    }
}
