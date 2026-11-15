using Godot;

/// <summary>与人物配招相同的外围手持示意，不创建可装配器物或战斗能力。</summary>
public partial class BattleBoardHandPreview : Control
{
    public float CellSize;
    public override void _Ready()=>MouseFilter=MouseFilterEnum.Ignore;
    public override void _Draw()
    {
        DrawTextureRect(BattleBoardSkin.Part("hand_socket"),new Rect2(0,0,CellSize,CellSize),false);
        DrawTextureRect(BattleBoardSkin.Part("hand_connector"),new Rect2(CellSize*1.02f,CellSize*.4f,CellSize*.32f,CellSize*.2f),false);
        DrawString(GetThemeDefaultFont(),new(0,CellSize+34),"手持",HorizontalAlignment.Center,CellSize,Mathf.RoundToInt(CellSize*19/110),BattleBoardSkin.PaperText);
    }
}
