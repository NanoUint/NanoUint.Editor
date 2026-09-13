# NanoUintEditor 分析报告

> 分析对象：`E:\WindowsFile\TheFallonOkabe\NanoUintEditor`（WPF 场景编辑器，net10.0-windows）
> 配套引擎：`NanoUint`（自研 WPF 视觉小说引擎）；游戏项目：`SteinsGateX`
> 分析方法：全量源码阅读 + 引擎 API 逐项对照 + `dotnet build` 验证 + 同类工具横向对比

---

## 1. 项目概览

**定位**：NanoUint 引擎的可视化场景编辑器（Unity 风格三栏布局：层级 / 场景视图 / Inspector）。
**工作流**：在 1920×1080 画布上摆放 GameObject → 添加组件（SpriteRenderer、TextRenderer、按钮、对话框等）→ 保存 JSON 场景 → **一键生成 C# 代码**（`ScreenBase` 子类）交给 SteinsGateX 编译运行。

**规模**：`MainWindow.xaml.cs` 1044 行（全部逻辑），`MainWindow.xaml` 147 行，`App.xaml` 65 行；无测试项目；无 README。

**组件系统**（编辑器侧 `ComponentRegistry`，MainWindow.xaml.cs:88-104）：Transform / SpriteRenderer / TextRenderer / SpriteButton / PassiveButton / BackgroundRenderer / DialogueBox / ChoiceGroup / AudioSource / FlashOverlay / AdvanceIndicator / LineRenderer / HintRenderer / SDFTextRenderer，共 14 种。

---

## 2. 问题清单

### 🔴 P0 —— 正确性（崩溃 / 数据损坏 / 生成物不可用）

#### P0-1 代码生成器与引擎 API 严重脱节，生成的 C# 代码无法编译

生成器（MainWindow.xaml.cs:933-1043）输出的代码与 NanoUint 实际 API 逐项对照：

| 生成的代码 | 引擎实际 API | 结论 |
|---|---|---|
| `public override void Build()` | `ScreenBase.Build()` 非 virtual；子类应重写 `protected override void OnBuild()`（SteinsGateX/Screen/ScreenBase.cs:23-30） | ❌ 编译错误 |
| `tr.SetText("...")` | `TextRenderer.Content` 属性（Components/TextRenderer.cs:14） | ❌ 编译错误 |
| `tr.Color = ...` | `TextRenderer.TextColor` 属性（:26） | ❌ 编译错误 |
| `Color.FromHex("...")` | `Drawing/Color.cs` 无 `FromHex`，只有 `FromRgb(byte,byte,byte)`/`FromRgba` | ❌ 编译错误 |
| `sr.Color = Color.FromRgb(/*#FFFFFF*/)` | `SpriteRenderer.Tint` 属性（Components/SpriteRenderer.cs:37）；且 `FromRgb` 无参调用、颜色值只写在注释里没进参数 | ❌ 编译错误 |
| `sb.SetSprite("...")` / `SetHoverSprite` | `SpriteButton.Sprite` / `HoverSprite`（Sprite 类型属性，需 `AssetDatabase.Load<Sprite>`）（Components/SpriteButton.cs:26,38） | ❌ 编译错误 |
| `bg.SetBackground("...")` | `BackgroundRenderer.Sprite` 属性（Components/BackgroundRenderer.cs:12） | ❌ 编译错误 |
| `db.SetText("...")` | `DialogueBox.Show(speaker, text)`（Components/DialogueBox.cs:54） | ❌ 编译错误 |
| `cg.SetOptions(new[]{...})` | `ChoiceGroup.Show(string[])`（Components/ChoiceGroup.cs:40） | ❌ 编译错误 |
| `aud.Clip = "path"` | `AudioSource.Clip` 为 `AudioClip` 类型（需 `AssetDatabase.Load<AudioClip>`） | ❌ 类型错误 |
| `aud.Loop = ...` | `AudioSource.IsLooping`（Components/AudioSource.cs:19） | ❌ 编译错误 |
| 组件分支仅 9/14 种 | LineRenderer / HintRenderer / SDFTextRenderer / AdvanceIndicator / Slider / VideoPlayer / PhoneScreen 无生成分支 | ⚠️ 数据静默丢弃 |

**影响**：核心卖点"生成代码"完全不可用；用户粘贴生成的代码后编译失败，且失败点遍布全文件，难以手动修正。
**根因**：编辑器与引擎分别演进，`GenerateCode` 从未与引擎 API 同步验证（无编译验证环节）。

#### P0-2 父子环无防护 → 无限递归栈溢出

`WorldX/WorldY`（MainWindow.xaml.cs:73-84）递归累加父坐标且无环检测。三处可建立父子环：
- 层级树拖拽 reparent（:495-511）：`Drop` 仅检查 `childId != obj.Id`，未排除后代
- Inspector Parent 下拉（:620-632）：可把对象设为自己的任意后代
- `LoadFromFile`（:902-925）：不校验 ParentId

**影响**：任一途径成环后，移动/渲染/命中测试立即栈溢出崩溃，且场景文件已被写入不可恢复。

#### P0-3 删除父对象 → 子对象悬挂（幽灵对象）

`DeleteSelected`（:845-848）仅移除对象自身，不处理 `ParentId == 被删Id` 的子对象。子对象残留但父级消失：
- 渲染时 `WorldX` 把其当 root 处理（父查找失败返回 0），位置错乱
- 保存的 JSON 含悬挂 ParentId，加载后永久污染

#### P0-4 保存/加载零容错

- `LoadFromFile`（:902-925）对 JSON 直接 `GetProperty`，缺字段/类型不符即抛异常崩溃；无 try/catch
- 保存写 `version = 3`（:894）但加载从不读取，无迁移逻辑
- `SaveToFile`（:891-899）无异常处理（磁盘满、权限、目录不存在直接崩）
- 默认保存目录 `BaseDirectory/saves`，而项目根有 `saves/` 空目录，路径语义混乱

#### P0-5 拖动时每帧重建 Inspector → 输入焦点丢失 + 卡顿

`Scene_MouseMove`（:387-430）在拖动/缩放时每帧调用 `UpdateInspector(_selected)`（:412/:428），而 `UpdateInspector`（:607-668）无条件 `InspectorContent.Children.Clear()` 后全量重建。
**影响**：用户正在输入的属性框被销毁（TextChanged 期间重建）、焦点丢失；每帧全量重建控件造成明显卡顿。

### 🟠 P1 —— 架构 / 可维护性

#### P1-1 单文件巨石
全部逻辑（数据模型、渲染、拖拽、Inspector 构建、资源浏览器、代码生成、IO）挤在 `MainWindow.xaml.cs` 1044 行，无法单元测试、难以维护。

#### P1-2 Transform 双重存储
`SceneObject.X/Y/Width/Height/Opacity/SortingOrder`（:48-70）与 Transform 组件 `Props["X"]...` 并存，靠 `SyncTransformFromData/ToData`（:149-172）手动同步，两处写路径（`BackgroundRenderer`/`FlashOverlay` 分支 :224-225/:297）绕过同步直接改字段，易失配。

#### P1-3 字符串魔法类型 + 无类型 Props
`HasComponent("SpriteRenderer")` 等字符串散落 40+ 处；`Props` 为 `Dictionary<string, object?>`，JSON 往返时 number→double、bool/string 需手工判别（:917），类型错误静默吞掉。

#### P1-4 `GetComponent<T>()` 是死代码且有缺陷
`:62-66`：所有组件实例化时都是基类 `ComponentData`，`as T` 恒为 null（除非 T==ComponentData）。当前无调用，但保留即陷阱。

#### P1-5 图片解码无缓存
`GetNativeImageSize`/`RenderObject` 每次属性变化都重新 `new BitmapImage` 解码磁盘大图（:194/:218/:237/:305/:774-786），拖拽/缩放 Sprite 时卡顿明显；同步解码阻塞 UI 线程。

#### P1-6 `ResourcesRoot` 硬编码脆弱路径
`:35-36` 用 `..\..\..\..\SteinsGateX\Resources` 相对 bin 目录上溯 4 级，依赖部署目录布局，找不到时 AssetTree 静默为空无任何提示。

### 🟡 P2 —— 体验 / 细节

| # | 问题 | 位置 |
|---|---|---|
| P2-1 | 状态栏硬编码 `1280×720`，与画布 1920×1080 不符 | MainWindow.xaml:143 |
| P2-2 | 无脏标记/关闭确认，误关窗口丢失全部工作 | — |
| P2-3 | `DuplicateSelected` 不复制 `ParentId`（:850-855），复制品脱离层级 | :850-855 |
| P2-4 | Inspector Parent 下拉索引计算 `TakeWhile(...).Count()+1` 在对象位于父对象之前时错位（:625） | :625 |
| P2-5 | 无撤销/重做、无多选/框选、无对齐工具 | — |
| P2-6 | 只支持内部 AssetTree 拖拽，不支持从资源管理器拖入图片（`DragOver` 仅接受 StringFormat，:442） | :442 |
| P2-7 | 无最近文件、无场景列表、无多场景 | — |
| P2-8 | 无资产变更检测（Resources 目录改动不刷新） | — |
| P2-9 | UI 文案中英文混杂（层级/资源/属性 vs NewScreen/GameObject），注释亦混用 | — |
| P2-10 | 关闭窗口不保存/不提示；`Build()` 代码生成对话框为模态，无法边看边改 | :1042 |

---

## 3. 同类项目对比

| 维度 | NanoUintEditor | Unity / Godot | Ren'Py (+RVNE) | TyranoBuilder | Naninovel (Unity) |
|---|---|---|---|---|---|
| 布局方式 | ✅ 可视化画布 + 层级 | ✅ 场景编辑器 | ❌ 代码为主 | ✅ 节点/画布 | ✅ 场景编辑器 |
| Inspector 属性编辑 | ✅（组件式，弱类型） | ✅ 强类型 | ❌ | ✅ | ✅ |
| 播放模式实时预览 | ❌ **缺失** | ✅ | ✅（运行游戏） | ✅ | ✅ |
| 撤销/重做 | ❌ **缺失** | ✅ | ✅（文本历史） | ✅ | ✅ |
| 多选/框选/对齐 | ❌ **缺失** | ✅ | ❌ | 部分 | ✅ |
| 模板/Prefab | ❌ **缺失** | ✅ | ❌ | ✅（素材库） | ✅ |
| 事件/脚本绑定 | 手写字符串（脆弱） | ✅ 引用式 | ✅ 脚本语言 | ✅ 可视化 | ✅ 命令式 |
| 动画编辑 | ❌ **缺失**（引擎已有 Tweener/Ease） | ✅ Timeline | 脚本 | ✅ 简易 | ✅ |
| 本地化预览 | ❌ **缺失**（引擎已有 LocalizationManager） | ✅ 扩展 | ✅ | 部分 | ✅ |
| 多分辨率适配 | ❌ 固定 1920×1080（引擎已有 CanvasScaler） | ✅ | ✅ | ✅ | ✅ |
| 资产导入 | 仅内部目录浏览 | ✅ 导入管线 | ✅ | ✅ | ✅ |
| 场景流程/分支图 | ❌ | 部分 | 流程图工具 | ✅ 节点图 | 部分 |

**结论**：NanoUintEditor 已具备 VN 场景编辑器最核心的骨架（三栏布局 + 组件式对象 + 场景序列化 + 代码生成），但作为"编辑器"的基本盘（撤销、多选、播放预览、模板）全部缺失，且代码生成这一端到端链路已断裂（P0-1）。修复 P0 后，其短板集中在**工作流效率**与**所见即所得**两个方向。

---

## 4. 新功能建议（按 价值/成本 排序）

### 高价值 / 低成本（本次实施）
1. **撤销/重做（Ctrl+Z / Ctrl+Y）** — 编辑器基本盘；命令栈覆盖 移动/缩放/增删/属性编辑/父子关系
2. **多选 + 框选 + 对齐/分布/统一尺寸工具栏** — 排版效率提升最大（VN 场景大量规则排列的按钮/选项）
3. **从资源管理器拖入图片（FileDrop）** — 与 AssetTree 拖拽统一走同一导入路径
4. **Prefab 模板系统** — 对话框、按钮、选项组等常用结构存模板，资源面板拖出实例化（含组件树与层级）
5. **事件绑定可视化** — onClick 从 `NanoUint.Scripting.ScriptCommandRegistry` 命令列表下拉选择，杜绝手写字符串
6. **脏标记 + 关闭确认 + 未保存提示**（同时修复 P2-2）
7. **最近文件列表 / 场景快速切换**

### 高价值 / 高成本（后续迭代）
8. **播放模式（Play Mode）** — 内嵌 NanoUint 引擎实例运行当前场景：按钮 hover/点击、打字机、背景交叉淡入、动画实时预览；编辑器 VN 化最关键的一步
9. **动画时间轴编辑器** — 可视化编辑 Tweener 补间（引擎已有 `Animation/Ease.cs`、`Components/Tweener.cs`），对齐 Unity Timeline / TyranoBuilder
10. **场景流程/分支图** — 章节与屏幕间导航可视化（对齐 TyranoBuilder 节点图），可与 `ScriptEngine` 命令联动

### 中价值 / 低成本
11. **本地化预览** — 利用 `LocalizationManager` 切换语言实时预览文本（对齐 Ren'Py 翻译工作流）
12. **多分辨率预览** — 利用 `CanvasScaler` 切换 1920×1080 / 1280×720 等预览适配
13. **资产热刷新** — `FileSystemWatcher` 监听 Resources 目录自动刷新 AssetTree
14. **资产导入管理器** — 复制外部文件进 Resources 并自动建引用

---

## 5. 与实施计划的对应关系

> 实施状态（2026-08-07 更新）：阶段 1~5 全部完成 ✅（播放模式与小功能已补做），交付文档见 README.md。

| 计划阶段 | 覆盖问题 / 建议 | 状态 |
|---|---|---|
| 阶段 2 修复 P0 | P0-1 ~ P0-5，P2-1、P2-2、P2-3、P2-4 | ✅ 已完成（代码生成器 API 校准+样例编译通过；三方防环+级联删除+加载校验；索引修复；保存/加载容错；Inspector 节流；脏标记+关闭确认；状态栏修正） |
| 阶段 3 架构 | P1-1 ~ P1-6 | ✅ 已完成（MainWindow 1044→933 行，拆出 SceneModel/ComponentRegistry/EditorUtils/AssetCache/CodeGenerator；图片解码缓存；Transform 属性 setter 自动同步） |
| 阶段 4 新功能 | 建议 1~7（撤销/多选/拖入/Prefab/事件绑定/最近文件） | ✅ 已完成（撤销重做+Ctrl+Z/Y；多选+框选+9 种对齐/分布/尺寸+Alt 快捷键；资源管理器拖入复制进 Resources；Prefab 模板存/取/拖放；onClick 命令下拉；最近文件 MRU 下拉） |
| 阶段 5 播放模式 | 建议 8 | ✅ 已完成（引擎 ScenePreviewHost + 编辑器 SceneRuntime 转换器 + ▶ 播放按钮/预览窗口；按钮/打字机/动画实时预览；STA 集成测试） |
| 后续（报告级建议） | 建议 9~14 | ⏳ 未实施（动画时间轴/场景流程图/本地化/多分辨率/资产热刷新/导入管理器） |

**测试基础设施**（本次新增）：`NanoUintEditor.Tests`（xunit，30 个测试）覆盖 SceneModel 层级逻辑、CodeGenerator 输出 API 对齐、EditorUtils 工具函数、UndoManager 快照语义。
