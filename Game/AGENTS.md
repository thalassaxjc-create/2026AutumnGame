# 项目协作约定

## 内存与资源管理

- 保留 `Assets/Scripts/UI/InteractionPromptUI.cs` 的共享中文字体缓存及重载、退出时的释放逻辑。新 UI 继续调用 `InteractionPromptUI.EnsureChineseFont`，不要自行重复创建字体。
- `Update`、`LateUpdate`、`Show`、`Hide` 等高频路径不得反复调用 `Font.CreateDynamicFontFromOSFont`，或隐式创建 `renderer.material`、独立材质、纹理、RenderTexture、AudioClip。优先缓存、共用资源；仅修改颜色时优先使用 `MaterialPropertyBlock`。
- 创建 Unity 原生资源时明确归属、销毁时机与跨场景生命周期。静态缓存和事件订阅要考虑脚本重载、退出 Play Mode、重复进入场景以及关闭 Domain Reload 的情况。销毁临时物体，取消不再需要的事件订阅。
- UI/资源生命周期相关修改应运行字体回归测试，确认 10,000 次显示/隐藏后 Font 数量不增长；交互修改需检查重复操作、场景切换后的对象与资源数量。不要用不断调用 GC 或 UnloadUnusedAssets 掩盖泄漏。

## Unity 验证的资源限制

- 运行 Unity 或重编译前检查现有 Unity 进程与系统可用虚拟内存；同一项目只运行一个编辑器/测试进程。测试、构建串行执行，不同时启动多个 Unity 实例。不要关闭用户自己的编辑器或其他应用来腾内存。
- 本机曾因提交内存不足出现桌面进程崩溃和 `0x800705AF / 页面文件太小`。物理空闲内存不能代表剩余提交额度；分页配置可能改变，应读取当前状态，不把历史状态当成现状。
- 自动验证默认限制本次子进程的编译并发：PowerShell 设置 `$env:BEE_BUILD_THREADS = '2'`、`$env:DOTNET_PROCESSOR_COUNT = '2'`，Unity 参数使用 `-job-worker-count 2`。这些设置只用于当前进程及其子进程，不写系统环境变量。
- 出现 OOM、页面文件不足或资源持续增长时，停止加开工作进程，检查日志和提交余量后降低负载。只清理由当前任务启动且确认属于本项目的后台进程。不要自动修改分页文件、桌面缩放或要求重启系统。
- 后台启动 Unity 使用 `Start-Process -WindowStyle Hidden`。单纯编译/配置可用 `-batchmode -nographics -quit`；需要画面/声音的交互测试保留图形设备。`-runTests` 运行时不要带 `-quit`，由测试运行器退出。
- 功能修改完成后，运行完整项目 EditMode 测试（不加 `-testFilter`），包括 `Assets/Tests/Editor/InteractionPromptUITests.cs` 和 `Assets/Scripts/Editor/BambooHarvestTests.cs`。采竹集成测试会自行进入 Play Mode 验证真实输入、倒下、掉落和场景切换。
- 保存测试 XML 与日志到 `Logs/BambooHarvest/`，明确区分通过、断言失败、编译失败和环境故障；不要把编译通过当成运行验证通过。
- 已打开 Unity 时，优先使用菜单“宣纸帘/运行完整项目测试”复用当前编辑器。篾竹修改需同时通过 `Assets/Scripts/Editor/BambooWorkbenchTests.cs`，覆盖材料守恒、竹节判定、真实鼠标输入和反复开关界面的资源数量。
- 剑门与抽丝修改还需运行 `Assets/Scripts/Editor/BambooFinishingTests.cs`：验证四次剑门、四组抽丝（每组 25 根）、危险区预警、松手回弹、残次材料守恒及退出/切场景后的续作。刀孔与张力图形复用 UI 网格，碰刃音频只在创建界面时分配并在销毁时释放。

## 场景与已有修改

- 使用当前项目版本 Unity 6000.3.27f1。通过编辑器 API 保存场景，优先局部修改，不重新生成整个白盒地图。
- `BambooHarvestSetup.ConfigureWorldScene` 配置六根可砍竹子，不改变其摆放。场景需保存组件，确保从室内进入 WorldScene 后仍可交互。
- 多个聊天可能同时修改文件，编辑前重新读取目标内容并保留其他任务已完成的修复。关联聊天工具不可用时，明确说明并以用户提供的信息和本地文件核对。
- Unity 会清理 `Temp/`；需要跨启动保留的备份、测试日志不要放在该目录。
- 篾竹工位由 `BambooWorkbenchSetup.Configure` 局部挂载到 `BambooAreaMat`；`BambooCraftSession` 仅保存本局加工状态。只有编辑器“试玩篾竹前两环节”入口赠送一根测试青竹，正常游戏必须消耗采集所得材料。
