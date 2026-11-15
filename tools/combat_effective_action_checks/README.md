# 有效行动事务独立检查

在项目根目录运行：

```powershell
dotnet run --project tools/combat_effective_action_checks/CombatEffectiveActionChecks.csproj
```

需要 .NET 8 SDK。成功时输出 `COMBAT_EFFECTIVE_ACTION_TRANSACTION_SMOKE_OK` 和检查数量。
此项目只编译独立事务原型及其测试，不加载正式战斗、招式数据或秘籍。首次实际拼点双方均付费，同一付款身份的后续交锋或行动不重复付款；连续闪避复用同一身份。云岚各段由调用方指定共享付款身份，起式付费后后续段免费；其他未来多段的收费方式不由此默认推定。
