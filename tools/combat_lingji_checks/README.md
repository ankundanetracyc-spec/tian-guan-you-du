# 灵机试玩公式独立检查

从项目根目录运行：

```powershell
dotnet run --project tools/combat_lingji_checks/CombatLingjiChecks.csproj
```

需要 .NET 8 SDK。此项目仅编译灵机计算器及检查，不读取角色、招式数据或正式战斗。
随机值由调用方提供；本工具不指定招式对应的基础属性和造诣，也不确定随机分布。
