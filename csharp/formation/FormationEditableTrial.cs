using System;
using System.Collections.Generic;

/// <summary>
/// 当前草图的真实隔离试演。编译失败即拒绝，不替换成固定守攻示例；人物由调用页创建和释放。
/// 修改草图后调用者须销毁本次试演并以新编译图、新隔离人物重新开始。
/// </summary>
public sealed class FormationEditableTrial : IDisposable
{
    public FormationGridControlCompiler.Compilation Compilation { get; }
    public FormationGuardAssaultRuntime Runtime { get; }
    public IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> OutputStates => Runtime.OutputStates;

    public FormationEditableTrial(FormationGridModel model, IFormationGuardActor actor,
        FormationGuardAssaultRuntime.Settings settings)
        : this(FormationGridControlCompiler.Compile(model), actor, settings) { }

    public FormationEditableTrial(FormationGridControlCompiler.Compilation compilation,
        IFormationGuardActor actor, FormationGuardAssaultRuntime.Settings settings)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        Compilation = compilation;
        Runtime = new FormationGuardAssaultRuntime(settings, actor, compilation.Circuit,
            compilation.PoweredOutputIds, compilation.SourcePresent);
    }

    public FormationGuardAssaultRuntime.Result Apply(FormationGuardAssaultRuntime.Input input) => Runtime.Apply(input);
    public FormationGuardAssaultRuntime.Snapshot GetSnapshot() => Runtime.GetSnapshot();

    // 只停止本试演的持续输出，不Free/Dispose调用者的Character或其资料。
    public void Dispose() => Runtime.Dispose();
}
