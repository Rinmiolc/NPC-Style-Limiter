# NPC Style Limiter - 项目文档

## 项目概述
**NPC Style Limiter** 是一款针对 RimWorld 的模组，让玩家能够精细控制 NPC（非玩家角色）的生成样式。它允许自定义发型、胡须、服装和体型，并能在生成过程中调整性别比例。

### 核心功能
- **样式过滤：** 启用或禁用特定的发型、胡须和服装。
- **权重调整：** 设置不同样式的生成权重，使其出现频率更高或更低。
- **性别差异化配置：** 可选择分别为男性和女性 Pawn 配置不同的样式权重。
- **体型控制：** 调整人类 Pawn 成年体型的分布比例。
- **性别比例自定义：** 覆盖游戏默认的生成性别比例。
- **预设管理：** 保存和加载不同的配置方案。
- **现代 UI：** 自定义的高性能游戏内设置菜单，支持搜索和按 Mod 过滤。

## 技术架构
- **语言：** C# 8.0
- **框架：** .NET Framework 4.72
- **游戏版本：** RimWorld 1.6
- **钩子引擎：** [Harmony](https://github.com/pardeike/HarmonyRimWorld) 2.3.3

### 核心组件
- **`CustomizerMod`**: 模组主入口，处理 UI 渲染和初始化。
- **`CustomizerSettings`**: 管理数据持久化（`Scribe`）、预设 XML 序列化，并通过基于 `shortHash` 索引的数组提供 O(1) 级别的运行时权重查询性能。
- **`Patches`**: 包含 Harmony 补丁：
    - `PawnGenerator.GeneratePawn`: 跟踪生成状态并调整性别比例。
    - `PawnStyleItemChooser.WantsToUseStyle`: 过滤发型和胡须。
    - `PawnStyleItemChooser.TotalStyleItemLikelihood`: 应用样式的自定义权重。
    - `PawnGenerator.GetBodyTypeFor`: 覆盖体型选择逻辑。
    - `PawnApparelGenerator.GenerateStartingApparelFor`: 跟踪特定 Pawn 的服装生成。
    - `ThingStuffPair.get_Commonality`: 应用服装选择的权重。
- **`PawnGenerationState`**: 实用工具类，用于检测游戏是否正在生成 Pawn，确保补丁仅对 NPC 生成生效。

## 编译与运行
项目使用标准的 C# 项目文件 (`NPCStyleLimiter.csproj`)。

### 编译前提
- .NET SDK (支持 .NET Framework 4.72 目标框架)
- RimWorld 引用程序集 (通过 `Krafs.Rimworld.Ref` NuGet 包引用)

### 编译命令
```powershell
dotnet build Source/NPCStyleLimiter.csproj
```
编译产物 (DLL 和 PDB) 会自动复制到 `1.6/Assemblies/` 目录。

### 运行
1. 确保模组文件夹位于 RimWorld 的 `Mods` 目录下。
2. 在游戏的模组菜单中启用 **Harmony** 和 **NPC Style Limiter**。
3. 通过 `选项 -> 模组设置 -> NPC Style Limiter` 进入设置界面。

## 开发规范
- **语言偏好：** 除非用户另有要求，否则所有计划书、设计文档和对话回复**必须默认使用中文**。
- **许可：** 项目采用 **GNU General Public License v3.0**。所有源文件需包含许可头。
- **双语支持：** UI 标签和注释通常提供中英双语。
- **性能：** 
    - 使用 `PawnGenerationState` 最小化补丁在正常游戏过程中的性能消耗。
    - 在高频调用的代码路径中使用基于 `shortHash` 的索引进行快速查找。
    - 避免在频繁调用的补丁（如 `GetWeightedBodyTypeFor`）中进行内存分配。
- **UI：** 使用在 `CustomizerMod.cs` 中定义的自定义 UI 主题。保持使用 `AccentColor` 和现代绘制辅助函数以确保视觉一致。
- **本地化：** 键值对存储在 `Languages/[Language]/Keyed/Keys.xml` 中。UI 文本始终使用 `.Translate()`。

## 目录结构
- `1.6/`: 包含针对 RimWorld 1.6 编译的程序集。
- `About/`: 模组元数据 (About.xml, 预览图)。
- `Languages/`: 简体中文、繁体中文和英文的本地化文件。
- `Source/`: C# 源代码和项目文件。
- `LICENSE`: GPL v3.0 完整许可协议。
