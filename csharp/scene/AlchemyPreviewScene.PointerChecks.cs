using Godot;
using System;

public partial class AlchemyPreviewScene
{
    // 经由视口实际命中控件，避免直接发送Pressed信号绕过遮挡。
    public async void CheckFurnacePointer()
    {
        try
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            StartUtilityLesson(0,true);_relationGraph._Process(1);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            foreach(var hand in new[]{AlchemyPreviewModel.Hand.Raise,AlchemyPreviewModel.Hand.Lower,AlchemyPreviewModel.Hand.Hold})
            {
                var button=_hands[hand];var point=button.GetGlobalRect().GetCenter();
                GetViewport().PushInput(new InputEventMouseMotion{Position=point,GlobalPosition=point},true);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                GD.Print("ALCHEMY_POINTER_TARGET "+hand+" "+GetViewport().GuiGetHoveredControl()?.GetPath());
                int before=_model.Breath;double heat=_model.Heat;
                GetViewport().PushInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=point,GlobalPosition=point},true);
                GetViewport().PushInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=point,GlobalPosition=point},true);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                double expected=heat+(hand==AlchemyPreviewModel.Hand.Raise?1:hand==AlchemyPreviewModel.Hand.Lower?-1:0);
                if(_model.Breath!=before+1||_model.Heat!=expected)throw new InvalidOperationException("实际指针未能点击"+AlchemyPreviewModel.HandNames[(int)hand]);
            }
            GD.Print("ALCHEMY_POINTER_CHECKS_PASSED 3");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
