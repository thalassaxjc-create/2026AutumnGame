# Project Overview
- Game Title: 当前项目 My project；工作名称「五柱绳索解谜」，不修改项目名称。
- High-Level Concept: 在五条平行柱道上操控胶囊，横向寻找随机线环的缺口。将右侧悬挂方块拉甩至左侧会形成横贯五柱的封路绳，玩家必须退回左侧下爬点，通过悬挂方块跳跃和攀爬绕过封路绳，连续搬运两个右侧方块。
- Players: 单人。
- Inspiration / Reference Games: 用户当前白盒关卡及描述；无指定参考游戏。
- Tone / Art Direction: 保留简单3D白盒柱道、胶囊、立方体；线环用清晰的绳状几何替代当前球体占位，功能用颜色区分，不引入外部美术资源。
- Target Platform: Windows PC，StandaloneWindows64，Unity 6000.3.27f1。
- Screen Orientation / Resolution: 横屏、适配当前窗口；现有默认1024×768，不强制改项目分辨率。
- Render Pipeline: 已检查 PC_RPAsset，URP。

# Game Mechanics
## Core Gameplay Loop
1. 前进观察障碍，横移到没有随机线环的柱道，再继续前进。
2. 到最右侧柱道的连接点，看到 Pull [E]，按E触发一次不可重复的固定轨迹拉甩。
3. 绳索带右侧方块完成大回环，方块最终悬挂在最左侧外缘下方，与左侧原有方块同高度并沿前进方向错开。定形绳横贯五柱，阻断本段柱面前进路线。
4. 玩家留在封路线的起始方向一侧，后退至本段左侧下爬点。E进入、前后方向爬下，到原有左侧方块上退出攀爬。
5. 在方块上用现有跳跃跳到刚甩来的方块，沿它的绳索爬回柱道。上岸点必须位于本次横线的前进方向一侧，不能爬回原来被挡的一侧。
6. 第二段重复；两个右侧方块都转到左侧后完成搬运目标。验证玩家也能继续到终点。

### 已确认的实现选择
- 固定轨迹动画：保证大回环表现、落点及解谜可读性；不采用真实自由物理绳索，避免撞柱、卡住或随机落点破坏解法。遵循MDA，通过确定的操作反馈形成可预测解谜动态。
- E进入攀爬、前后方向控制上下，优先保持玩家掌控感（SDT自主性）；平台跳跃仍由玩家完成，不用自动传送代替。
- 第一版两个右侧方块、至少两个原有左侧下爬方块。

### 柱道及通行规则
- 沿用实际场景世界坐标：X左右、Y高度、Z前进；现有柱子旋转90度后沿Z延伸，顶面约Y=0。五柱指五条平行纵向柱道，不是五根竖直攀爬柱。
- 五条柱道按统一间距扩展到左右两侧；保留最右道分段的视觉结构，但确保各Pull连接点的站立区可达。保留连续移动，不强制改成瞬移式换道。
- 胶囊能在障碍前左右移动及后退，遇障碍不能穿过，不能从柱阵外侧贴边绕过全宽横线，也不能直接跳过线环/全宽横线来跳过解谜。以可视绳索配合阻挡体和运动状态校验实现；必须有明确可读的阻挡范围。
- 普通跳跃保留用于下方方块之间；柱面跳跃可保留动作但不得跨越封锁。横移跨柱缝隙要可操作、不需精确卡帧，调节间距或提供可见连接面。

### 有约束的随机线环
- 每个障碍排可随机覆盖1至4条柱道，支持两根、三根或四根同时有线环；常规随机障碍不得覆盖全部五条。全宽障碍仅由拉甩事件生成。
- 不仅检查每排有空位，还按玩家体积、可横移区域、前后排间距及连接点运行可达性检查，确保从入口到Pull、到下爬点、从上岸到下一段均有连续路线。
- 支持可复现seed，进入游戏生成一次，游戏中不突然重排；编辑场景保留一个能看见五柱和更多线环的预览布局。
- 每个左侧下爬点、右侧Pull点、最终上岸点及它们的接近区域为保留空间。随机环不侵入这些区域或下层跳跃路线。
- 生成失败采取有限重试，再使用已验证的保底布局，不无限循环。

### 防止连续拉动软锁的必要布局
沿Z从起点到终点：起点 → L1下爬点 → R1拉动/封锁断面 → R1最终上岸点 → L2下爬点 → R2拉动/封锁断面 → R2最终上岸点 → 终点。
- L1比R1更靠起始方向，L2处在R1和R2之间，且在R1上岸后可达。
- 第i次拉动发生时，Li必须在第i道横线后方（起始侧）、第i-1道横线前方（已越过侧）的当前区段内。
- 拉动后方块落在横线另一侧的左下方；原左方块与转移方块的顶面等高，沿Z间距不超过现有移动/跳跃能力并留容错。不能只检查坐标顺序，还要检查胶囊碰撞体、跃迁轨迹和绳索上岸方向。
- 拉动前再次检查下爬点可达及目的方块落点安全；未满足时拒绝启动并报告具体配置错误，不制造无法继续的存档状态。
- 单个搬运状态为 Ready → Swinging → Transferred；全局同一时间仅一个Swinging，防止连按E重入。

### 大回环及绳索最终形态
- 固定轨迹在每个关卡断面的X-Y平面附近制作；定义从起始方向沿+Z看向关卡时为逆时针，先从右下方起摆，再绕开整个五柱截面，最终到左下方。将此视角作为计划审批的一部分；若与用户预期不同，先调整轨迹示意而不是宣称已匹配。
- 路径采用明确控制点/弧线与可调持续时间，按方块完整包围体留净空，不仅按方块中心避障。绳索随方块更新可视折线，大回环不能穿过五柱实体。
- 初始右侧边缘至方块顶面为下垂绳；最终包括跨越五柱的封路段、绕左缘的连接段及到左下方方块的垂绳。绳长允许设计化变化，不声明是严格守恒的物理模拟。
- 最终可攀爬垂绳引导到封锁断面前进侧的左缘上岸点；跨柱段是阻挡，不是柱面可走的桥。阻挡段和可攀爬段用不同颜色/提示区分。
- 拉动交互点放在封锁断面起始侧，动画期间防止玩家进入扫掠区；不把胶囊弹走或造成意外死亡。结束后开放明确的后退路线。

## Controls and Input Methods
- 已确认 PlayerSettings.activeInputHandler=1，新Input System；场景PlayerInput使用 Assets/_Game/PlayerControls.inputactions 的 Gameplay，但预制体仍引用模板输入，需要统一。
- Move：WASD及方向键，事件回调保存Vector2，在FixedUpdate应用运动；不再增加旧Input API或使用每帧键盘轮询。
- Jump：Space，通过InputAction.performed缓冲到物理步；保留当前防重复跳跃及落地检测，攀爬期间禁用普通跳跃，平台状态恢复正常跳跃。
- Interact：E，接近唯一合适目标时performed触发。Ready右侧点执行Pull；绳索上下端执行攀爬；攀爬中E在安全位置退出，不重复触发Pull。
- 攀爬状态：沿绳索路径参数移动，锁定其他自由运动并正确处理刚体重力/速度；到上下端安全落脚点自动退出。绑定W/上箭头向上、S/下箭头向下。
- 跌落提供安全重生到当前已解锁区段检查点，保留搬运状态；不把玩家复活到已封横线的错误一侧。

# UI
采用轻量世界空间交互标签（实现时按现有可用UI包选用uGUI），不新增菜单或复杂HUD。
```
柱道边缘    Pull [E]              原左侧连接点    Climb [E]
             |                                      |
          右侧方块                               左侧方块

靠近时：提示高亮；远处：保留小型Pull标签便于找到两处目标。
拉动时：Swinging…；完成后不再显示可拉动提示。
攀爬时：W/↑ 上爬 · S/↓ 下爬 · E 安全退出
两个转移均完成时：2/2 方块已移到左侧
```
- 标签面向相机，不被柱体长期遮挡，文字背景保证白盒环境中可读；交互范围与实际可操作范围一致。
- 提示只由实际目标状态控制，不能远距离Pull或同时触发多个邻近目标。
- 相机轻量跟随/取景调整，确保五柱、左右下层方块、攀爬与大回环都能看见；不改URP全局配置或烘焙灯光。

# Key Asset & Context
## 现有资源及检查结果
- Assets/Scenes/WhiteboxLevel.unity：当前已打开；包含4段ground_01（对应3条道）、5个string1占位、右侧2个block及左侧1个block、Player、相机、光源、Global Volume。
- 柱道顶面约Y=0，左/中道X=-5/0，右道X=5；下方方块约Y=-20，现有方块尺寸7×7×5，需重新定标位置以保证扩展后跳跃可达。
- Assets/_Game/Prefabs/Player.prefab：刚体、CapsuleCollider、PlayerInput，重复3个PlayerController；场景实例4个。清理为一份，避免多个物理控制器互相覆盖。
- Assets/_Game/Scripts/Player/PlayerController.cs：目前Move/Jump逐帧查询、FixedUpdate写linearVelocity，依赖Gameplay/Move/Jump、groundLayers及地面球检测；加入事件驱动输入与走路/攀爬状态，不同时叠加第二套独立运动控制。
- Assets/_Game/PlayerControls.inputactions：目前仅WASD Move与Space Jump；增加方向键及Interact。
- Assets/InputSystem_Actions.inputactions：模板输入，保持原文件不修改，只解除Player预制体对它的使用。
- Assets/Scenes/SampleScene.unity：不修改。现有构建列表仅SampleScene；本轮以WhiteboxLevel编辑器测试，不擅自切换项目启动场景。
- Dependencies已用AssetDatabase.GetDependencies核实：WhiteboxLevel依赖Player prefab/controller/PlayerControls；prefab依赖controller和模板InputSystem_Actions；controller被prefab和WhiteboxLevel引用。正式修改前重新检查依赖与对象状态。

## 拟新增文件（名称可按实际实现微调）
- Assets/_Game/Scripts/Level/FivePillarLevel.cs：五道布局、种子、障碍和机关段配置及入口/出口信息。
- Assets/_Game/Scripts/Level/RingObstacleGenerator.cs：受约束生成、保留空间、可达性校验和保底布局。
- Assets/_Game/Scripts/Interaction/PlayerInteraction.cs：最近有效目标选择与E事件分发。
- Assets/_Game/Scripts/Puzzle/PullBlockMechanism.cs：拉甩状态机、轨迹控制、方块最终状态与封锁激活。
- Assets/_Game/Scripts/Puzzle/ClimbableRope.cs：绳索路径、上下端落脚点及攀爬段定义。
- Assets/_Game/Scripts/Puzzle/RopeVisual.cs：绳状可视几何与同步阻挡段，LineRenderer本身不提供碰撞。
- Assets/_Game/Scripts/Puzzle/PuzzleProgress.cs：两个搬运目标与当前安全重生点。
- Assets/_Game/Scripts/UI/InteractionPrompt.cs：Pull/Climb等轻量提示。
- Assets/_Game/Scripts/Camera/PuzzleCamera.cs：跟随及下层视野，若现有静态镜头足够则不创建。
- Assets/_Game/Materials/：只创建必要URP白盒绳索/功能区材质。
- Assets/_Game/Prefabs/Puzzle/：必要的线环、下爬点和拉甩方块可复用组件。
- Assets/_Game/Tests/：根据项目可用Test Framework采用独立测试程序集，避免测试引用污染运行时代码。

# Implementation Steps
1. **整理玩家与输入**
   - 修改Player.prefab及WhiteboxLevel实例，保留一份PlayerController、一份PlayerInput，统一Gameplay输入资源；核对实例覆盖而不是删除整份Player。
   - 修改PlayerControls.inputactions添加Interact和方向键；PlayerController改事件订阅/解绑，提取受控运动状态，保留固定物理步及落地检测。
   - 依赖：重新核对场景、prefab、脚本、输入资源修改影响；不动Packages。
2. **重排五柱与两个解谜区段**
   - 在WhiteboxLevel扩展五条可通行柱道，整理层级；原有占位障碍改为功能明确的绳环。
   - 两个右侧Pull方块，各自有独立绳和触发点；左侧配置L1/L2，满足L1<R1<L2<R2与两次上岸的位置顺序。
   - 调节柱宽、跨道间距、立方体尺寸与下层跳跃间距；保留白盒艺术风格。
   - 依赖：步骤1；先用无随机障碍的确定性布局验证基础路线。
3. **建立绳索和攀爬运动**
   - 实现RopeVisual、ClimbableRope、PlayerInteraction并与PlayerController状态连接。
   - 绳端进入、上下爬、落脚退出、方块之间正常跳跃，防止普通移动与攀爬同时写刚体。
   - 依赖：步骤1、2；先测试一套静态L1→转移落点→前方上岸路线。
4. **实现Pull及完整大回环**
   - 实现PullBlockMechanism的Ready/Swinging/Transferred及受保护扫掠区域；曲线轨迹按指定视角逆时针，方块和绳索同步。
   - 到达后激活五柱全宽阻挡段，开放左侧目的方块及前进侧上岸绳；动作不可重复。
   - 拉动前校验退路，拉动后保持玩家在起始侧而不是自动移过障碍。
   - 依赖：步骤2、3；先完成R1再复制配置R2，不用两套不一致逻辑。
5. **受约束随机线环与防越界**
   - 实现生成器，支持1–4道组合、多个障碍排、seed和编辑预览。
   - 按有效胶囊净空和游戏状态检查路径、机关接近区、回退区与下层路径；保底布局。
   - 确保线环阻挡前进但允许在环前左右移动；防止跳过和从五柱外侧绕过机关横线。
   - 依赖：步骤2–4，避免随机布局先掩盖基础机制错误。
6. **提示、取景、重生和完成目标**
   - 添加Pull [E]/Climb [E]与攀爬操作提示；必要的相机跟随让下层不出画面。
   - PuzzleProgress记录搬运数量、当前区段安全重生，两个方块均转移到左侧后显示完成；玩家能继续向终点前进。
   - 依赖：步骤3–5；不增加无关菜单/联网/美术系统。
7. **整体检查与保存**
   - 完整走通两次拉动和两次绕行，保存WhiteboxLevel及必要prefab/input修改；确认场景编辑预览已显示五柱和更多线环。
   - 保持SampleScene及当前Build Settings不变，报告测试结果与修改清单。
   - 依赖：全部步骤与下述验证通过。

# Verification & Testing
## 编辑模式 / 自动化校验
- Player prefab及实例各只有一个PlayerController，PlayerInput正确引用Gameplay的Move、Jump、Interact；所有引用无Missing Script。
- 五条柱道顶面/间距可行，所有绳段可见，阻挡体匹配可视覆盖范围；URP材质无粉色。
- 对至少100个seed检查：每随机排1–4道阻挡、必有足够净空缺口；入口到两个Pull及各次左侧下爬/上岸路线均连通，保留区不被侵入。
- 注入失败生成配置：有限重试后回退保底；若保底也不满足则报明确错误并不允许启动Pull。
- 机关状态单调且重复E不重复增加进度；第2次拉动的L2位于两次封锁之间。
- 校验方块扫掠体与五柱有净空、两组下层跃迁高度/距离可达、上岸点位于各自封锁前方。

## Play Mode 手动 / 可用测试框架验证
- WASD/方向键、Space、E实际输入都有效；多次启停Play不会重复订阅输入。
- 接近一/二/三/四道同时有环的障碍：不能穿越或跳过，能左右换到缺口继续前进；斜向冲撞和边缘贴行不绕过封锁。
- E的距离、朝向/目标选择合适；远处按E无效，攀爬中不误触Pull。
- R1从右下逆时针大回环绕开五柱，结束左下；横线真正封住五道。玩家后退到L1，爬下、跳到目的方块、爬回前进侧。
- 越过R1后仍可走到L2和R2，拉R2后不需退穿R1横线就能到L2，第二次同样绕行成功。
- 攀爬中切换方向、顶端/底端退出、按住E、连续E、跳跃键均不产生穿柱或飞走；只在安全处恢复重力。
- 跌落后重生在当前可解区段；两个已转移方块不恢复右侧、不重复计数，不把玩家困在旧横线之后。
- 从相机画面清楚看到五柱、Pull标记、大回环和下层跳跃，不只从Scene视图看起来正确。
- 最终2/2右侧方块均移到左侧且玩家能到终点；记录真实通过/失败结果，不用静态检查冒充运行测试。

## 范围边界
只在审批后修改上述游戏资源。本计划阶段未修改场景、脚本、prefab或输入资源。无真实绳索模拟、无NavMesh、无外部资源导入、无联网、无全局渲染/构建配置变更。