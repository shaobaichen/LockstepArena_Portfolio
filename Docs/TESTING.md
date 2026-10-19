# 测试与验收（Testing）

## 开发与测试环境

使用 Unity `6000.3.10f1`（需安装 Windows Build Support）与 `global.json` 指定的 .NET SDK `8.0.419`。使用完整 Windows 发行包的普通玩家**不需要**安装这些开发工具。

## .NET 自动化测试

在仓库根目录中**依次、串行**执行：

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
```

完成依赖还原后，本地串行验证可以附加 `--no-restore -p:UseSharedCompilation=false -p:BuildInParallel=false`；首次在干净环境中运行时，应先允许项目完成依赖还原。

**注意：启动测试命令不代表测试已经通过。** 必须检查测试运行器报告的失败数量及进程退出码。

| 测试集 | 主要验证内容 |
| --- | --- |
| Simulation | 帧与状态的确定性、整数坐标移动和碰撞、投射物伤害与生命周期、回合/胜负结算、StateDigest。 |
| DemoFlow | 真实 CONTROL / 房间 / 准备 / 开始 / 结算 / 大厅生命周期、已验证回放的保留与离线播放、调试面板。 |
| Client Prediction | 有界预测时间线、完整输入帧 Dirty 检测、回滚与重新演算。 |
| Live Prediction | 实际 TCP 权威逻辑与客户端预测/回放的状态收敛，以及快照采集。 |

`Tests/` 中的其他工程还分别验证协议、流式消息分帧，以及服务器的调度与网络传输。判断真正依赖哪些构建模块，应以实际工程引用关系为准，而不是根据历史名称推断。

## Unity EditMode 测试

在 Unity 中打开 **Window → General → Test Runner**，清空搜索框，选择 **EditMode → Run All**，并检查完整测试结果。如有需要，可以通过 **Export Results** 保存当前运行的 XML 结果文件。

表现层主要测试以下内容：

- 已保存的场景、URP 材质、机器人/武器模型、鼠标与相机绑定关系，以及可编辑的表现层锚点。
- HP 血条、提示公告的显示优先级、结算与大厅按钮行为，且不存在始终占据画面的正式操作提示栏。
- 当同一时刻出现投射物移除与 HP 下降时，不能错误地把所有移除都判断为命中玩家。
- **Watch Replay** 只能在结算已验证且存在有效回放快照时使用。
- 回放禁用实时战斗输入、使用离线状态、结束时保持画面、能返回原结算界面并干净地重新播放。
- 完整重置表现层后，相同投射物/HP 反馈可以再次出现，并移除该表现层自己创建的临时特效对象。
- 成功恢复实时战斗或回放表现后，先前已解决的场景绑定诊断错误会被清除。

测试必须针对**当前源码**执行，不能用旧版本的测试计数或预览构建作为最终验证证据。

## Windows 构建验证

先关闭所有正在运行的游戏客户端。在 Unity 菜单中选择 **Lockstep Arena → Build v3-A Windows Player**。菜单沿用了历史命名，但实际会构建当前保存的 Lobby/Battle 场景，并包含可独立运行的 Windows x64 DemoHost。

检查构建结果与 `.artifacts/V3APlayer/` 中新生成的文件。发行包应保持可执行文件、`_Data`、Unity 运行时库、Mono 运行时、D3D12 相关文件和 `Server/` 目录完整。最终演示包还包含 QUICK_START 和第三方声明。标记为 `DoNotShip` 的调试信息目录不是运行依赖。

## 双客户端人工验收

使用**最终完整 Windows 发行包**，两个客户端分别使用不同昵称：

1. 房主从 **Main Menu → Create LAN** 创建局域网房间；加入者从 **Join LAN** 连接（同机填写 `127.0.0.1`，局域网双机填写房主 IPv4）。
2. 加入指定房间，双方点击 **Ready**，房主点击 **Start**。
3. 验证移动、鼠标朝向/瞄准、投射物、HP 下降、死亡反馈、回合重置及率先赢得两回合的结算流程。
4. 战斗中按 **F1**，检查权威/预测 Tick、Dirty Frame 计数、同 Tick 状态摘要、回放和结算状态。
5. 结算验证完成后选择 **Watch Replay**，检查角色、投射物、HP 和死亡反馈是否能够正确重演。
6. 使用 **Space** 暂停/继续，确认 WASD 和鼠标开火不会控制回放中的战斗角色。
7. 回放期间按 **F1**，检查 `AUTHORITATIVE`、`OFFLINE`、当前帧进度和回放 StateDigest。
8. 按 **Escape** 返回原结算界面，再次选择 **Watch Replay**，确认音效和特效依然能够正常出现。
9. 让回放播放到末尾，确认画面保持在最后一帧，直到点击 **Back To Result**；不应自动返回大厅。
10. 选择 **Return To Lobby**，不重启客户端或服务器，直接开始第二场真实比赛。
11. 确认第二场不存在上一场残留的回放模式、过期视听反馈或已经修复的错误提示。

## 独立源码副本验收

可移植的源码副本，必须**在它自己的独立目录中**完成以下检查，才能认定有效：Unity 能正常打开；导入时不丢失脚本、材质、贴图和 Prefab；通过所选 .NET 测试及完整 EditMode 测试；成功构建带独立服务器的 Windows 版本；完成双客户端、回放及大厅流程冒烟测试。

不要复制开发目录中的 `Library/Temp` 缓存，也不能用原开发目录的构建结果冒充副本验证。

针对本次真实改动范围执行 `git diff --check`，并将工作区已有的无关改动与新增空白字符错误分别处理。
