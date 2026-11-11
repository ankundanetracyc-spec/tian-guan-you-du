using Godot;
using Kind = FormationGridModel.Kind;

public partial class FormationWorkbench
{
    private OptionButton _boardSize = null!, _compareMode = null!, _compareUnit = null!, _splitType = null!, _splitUnit = null!, _switchMode = null!;
    private SpinBox _compareLow = null!, _compareHigh = null!;
    private VBoxContainer _compareFields = null!, _highField = null!, _splitFields = null!, _splitUnitField = null!, _switchFields = null!;
    private static readonly FormationValueUnit[] NumericUnits = { FormationValueUnit.Percent, FormationValueUnit.Shield, FormationValueUnit.Mana, FormationValueUnit.Energy };
    private bool HasStateControl => Canvas.Model.Components.Values.Any(p => (int)p.Kind >= (int)Kind.StateInput || p.Kind == Kind.Split && p.Settings.SignalType != FormationSignalType.Event);

    private void BuildBoardSize(HBoxContainer row)
    {
        _boardSize = new OptionButton { Name = "FormationBoardSize" };
        _boardSize.AddItem("阵盘 4×4"); _boardSize.AddItem("阵盘 5×5"); StylePaperOption(_boardSize, 20);
        _boardSize.ItemSelected += index =>
        {
            var size = index == 1 ? 5 : 4;
            var accepted = Canvas.Model.TryResize(size);
            SyncBoardSize(); Canvas.QueueRedraw();
            Refresh(accepted ? $"已切换{size}×{size}阵盘，部件与线路保留。" : Canvas.Model.LastError);
        };
        row.AddChild(_boardSize);
    }

    private void SyncBoardSize() { if (_boardSize is not null) _boardSize.Select(Canvas.Model.BoardSize == 5 ? 1 : 0); }

    private void BuildAdvancedParameters(VBoxContainer box)
    {
        _compareFields = new VBoxContainer(); _compareFields.AddThemeConstantOverride("separation", 10); box.AddChild(_compareFields);
        var mode = Field(_compareFields, "比较方式"); _compareMode = Options("ControlCompareMode", "大于或等于", "小于或等于", "区间保持"); mode.AddChild(_compareMode);
        var unit = Field(_compareFields, "数值单位"); _compareUnit = UnitOptions("ControlCompareUnit"); unit.AddChild(_compareUnit);
        var low = Field(_compareFields, "阈值／下限"); _compareLow = Number("ControlCompareLow", 0, 1000000, 30); low.AddChild(_compareLow);
        _highField = Field(_compareFields, "上限"); _compareHigh = Number("ControlCompareHigh", 0, 1000000, 60); _highField.AddChild(_compareHigh);
        _compareMode.ItemSelected += _ => _highField.Visible = _compareMode.Selected == 2;
        _splitFields = new VBoxContainer(); _splitFields.AddThemeConstantOverride("separation", 10); box.AddChild(_splitFields);
        var type = Field(_splitFields, "传递信号"); _splitType = Options("ControlSplitType", "事件", "开关", "数值"); type.AddChild(_splitType);
        _splitUnitField = Field(_splitFields, "数值单位"); _splitUnit = UnitOptions("ControlSplitUnit"); _splitUnitField.AddChild(_splitUnit);
        _splitType.ItemSelected += _ => _splitUnitField.Visible = _splitType.Selected == 2;
        _switchFields = Field(box, "放行条件"); _switchMode = Options("ControlSwitchMode", "开时放行", "关时放行"); _switchFields.AddChild(_switchMode);
        HideAdvancedParameters();
    }

    private static OptionButton Options(string name, params string[] labels)
    {
        var option = new OptionButton { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var label in labels) option.AddItem(label); StylePaperOption(option); return option;
    }
    private static OptionButton UnitOptions(string name) => Options(name, "百分比", "护盾", "法力", "阵能");
    private void HideAdvancedParameters() => _compareFields.Visible = _splitFields.Visible = _switchFields.Visible = false;

    private void RefreshAdvancedParameters(FormationGridModel.Component part)
    {
        HideAdvancedParameters();
        switch (part.Kind)
        {
            case Kind.Compare:
                _compareFields.Show(); _compareMode.Select((int)part.Settings.Comparison);
                _compareUnit.Select(Math.Max(0, Array.IndexOf(NumericUnits, part.Settings.Unit)));
                _compareLow.Value = part.Settings.Low; _compareHigh.Value = part.Settings.High; _highField.Visible = _compareMode.Selected == 2; break;
            case Kind.Split:
                _splitFields.Show(); _splitType.Select((int)part.Settings.SignalType);
                _splitUnit.Select(Math.Max(0, Array.IndexOf(NumericUnits, part.Settings.Unit))); _splitUnitField.Visible = _splitType.Selected == 2; break;
            case Kind.Switch:
                _switchFields.Show(); _switchMode.Select(part.Settings.NormallyClosed ? 1 : 0); break;
        }
    }

    private bool TryAdvancedSettings(FormationGridModel.Component part, out FormationGridModel.Settings settings)
    {
        settings = part.Settings;
        switch (part.Kind)
        {
            case Kind.Compare:
                settings = settings with { Comparison = (FormationControlCircuit.CompareMode)_compareMode.Selected, Unit = NumericUnits[_compareUnit.Selected], Low = _compareLow.Value, High = _compareHigh.Value }; return true;
            case Kind.Split:
                var type = (FormationSignalType)_splitType.Selected;
                settings = settings with { SignalType = type, Unit = type == FormationSignalType.Number ? NumericUnits[_splitUnit.Selected] : FormationValueUnit.None }; return true;
            case Kind.Switch:
                settings = settings with { NormallyClosed = _switchMode.Selected == 1 }; return true;
            default: return false;
        }
    }

    private string AdvancedHint(FormationGridModel.Component part) => part.Kind switch
    {
        Kind.StateInput => $"固定感应{FormationBoardCanvas.StateName(part.Settings.StateKey)}；只读取真实状态，不手填数值。",
        Kind.Compare => part.Settings.Comparison == FormationControlCircuit.CompareMode.Band ? "区间保持：小于或等于下限开，大于或等于上限关，中间保留前次结果。" : "数值与阈值按所选方式比较，成立输出开，否则输出关。",
        Kind.Not => "开关取反，未知仍是未知。",
        Kind.Switch => "事件与许可分别接入口；仅本次许可成立才放行，不补发旧事件。",
        Kind.Guard => "开关控制持续护罩；停用只撤本器剩余护罩。试演每段耗2阵能、提供18护盾，维持负荷2；每共同回合续段。",
        Kind.Fire => "事件到达后核实供能和承载。试演每次耗3阵能、造成9伤害、负荷3，每共同回合限1次。",
        Kind.Split => "显式分送同类型信号，开关／事件／数值不能混接。",
        _ => ""
    };
}
