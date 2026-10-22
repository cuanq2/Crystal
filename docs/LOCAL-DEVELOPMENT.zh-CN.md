# 本机开发与中文维护

本分支基于 Crystal `0e315fe327192afe52c3d7357ddd1f5b7e26c5b8`，包含中文显示补齐、窗口分辨率兼容、Direct3D9Ex 和密码输入框修复。保留上游 README、版权与依赖说明。

## 目录

| 目录 | 内容 |
| --- | --- |
| `Client`、`Shared`、`Server` | 游戏源码 |
| `Client/Localization` | 客户端语言文件和任务整句翻译 |
| `Tools/Localization` | 统一中文词表来源及生成器 |
| `Tools/ItemNameAudit` | 数据库物品/怪物/任务元数据只读导出 |
| `Tools/UiLocalizer` | UI 图库中文覆盖层制作工具及帧补丁定义 |
| `Checks` | 无需登录账号的本机回归检查及固定数据样本 |
| `artifacts/validation` | 本机验证日志，不提交 |

运行数据库、账号、配置、地图、音效、图库和恢复包放在独立运行目录。不要把编译输出整体复制覆盖运行环境；更新前退出客户端，只替换所需程序或词表，并保留运行数据。源码整理不会更新正在运行的客户端。

## 构建与验证

环境：Windows x64、.NET 8 SDK；生成中文词表使用 Python 3.10 或以上，无第三方 Python 包。NuGet 依赖沿用项目声明的版本。

```powershell
dotnet build Client/Client.csproj -c Release
python Tools/Localization/generate.py --check
powershell -NoProfile -File Tools/Validate-Local.ps1
```

`LocalValidation.sln` 仅构建本次改动涉及的客户端、共享库、服务端解析库、检查与工具；原 `Legend of Mir.sln` 保持不变。统一验证脚本不启动游戏服务、不登录账号、不部署程序。

实际客户端控件检查需要 SlimDX 所需的 x64 VC++ 2010 和 DirectX 原生依赖：`msvcr100.dll`、`msvcp100.dll`、`d3dx9_43.dll`、`D3DCompiler_43.dll`；图形检查还需要可用的交互式 Windows 图形会话。依赖 DLL 不作为本次新增提交内容。默认从 `Build/Client/Release` 复制到检查输出目录，也可指定已有的依赖目录：

```powershell
powershell -NoProfile -File Tools/Validate-Local.ps1 -NativeDependenciesDir 'X:\game-runtime\Client'
```

可用 `-Dotnet`、`-Python` 指定可执行文件，用 `-PackageSource` 指定本机 NuGet 源。没有图形环境时可加 `-SkipGraphics`，结果会明确标记图形检查已跳过，不能将其视为完整图形回归通过。

验证覆盖：全部物品名称、内部别名、数量与拾取提示、英语回退；NPC 名称、任务标题和进度、任务颜色/链接/变量；真实任务窗口解析；窗口分辨率；图库像素与遮罩、密码气泡和输入框绘制。不会使用游戏账号连接服务端。

## 中文名称的唯一维护入口

- `Tools/Localization/Data/ItemNames.zh-CN.json`：全部物品显示名。经典名称优先，无对应名称者按传奇风格适配。
- `Tools/Localization/Data/Terms.zh-CN.json`：非物品词条，包括 NPC、地图、任务标题、任务进度及常用文字。不得重复定义物品原名或别名。
- `Client/Localization/Chinese.Quests.json`：任务和引导对话整句，保留原文中的动作、颜色和变量标记。
- `Client/Localization/Chinese.json`：通用 UI、枚举与系统提示。
- `Client/Localization/Chinese.Trial.json`：生成结果，历史文件名保留以兼容现有客户端。不要在此单独修改翻译。

修改来源后生成并检查：

```powershell
python Tools/Localization/generate.py --write
python Tools/Localization/generate.py --check
```

生成器覆盖 `Checks/Fixtures/item-name-audit.json` 中所有物品类型，不使用装备类型白名单。新增物品没有中文、大小写键冲突、任务动作标记被改动时会失败。生成只写源码词表，不会自动写入独立运行环境。客户端仅翻译显示值，服务端原始物品名和脚本匹配值保留；更新运行词表后需正常退出并重启客户端。

固定样本来源：公开的 `Suprcode/Crystal.Database`，版本 `a19f6dca8f5e238d4ed79801820777abbf0a9ca4` 的 Jev 数据。仅包含物品/NPC/任务元数据及翻译，无账号、密码和角色存档。扩展数据库后，使用工具显式导出到新的 JSON，并替换相应样本；不提交整个数据库：

```powershell
dotnet run --project Tools/ItemNameAudit -c Release -- 'X:\game-runtime\Server\Server.MirDB' 'artifacts/items.json'
python Tools/Localization/generate.py --check --records artifacts/items.json
```

可另传 `monsters.json`、`quests.json` 输出路径。导出器不启动服务，仅解析任务二进制元数据，不加载本地任务脚本。

## 中文 UI 图库

`Tools/UiLocalizer/chinese-ui-patches.json` 记录原始图库的帧号、文字及绘制位置。生成覆盖层时指定独立输出目录，保留原图库：

```powershell
dotnet run --project Tools/UiLocalizer -c Release -- build 'X:\game-runtime\Client\Data' 'artifacts/ChineseUI' Tools/UiLocalizer/chinese-ui-patches.json
```

工具另支持 `frame` 和 `sheet` 导出图片用于检查。覆盖层部署到运行环境 `Data/Localization/Chinese`；实际图库、地图和音效不纳入本次提交。

## 已知限制

NPC 彩色对话使用 `DialogTextLayout` 按翻译后的文字逐段排版，行标签只保存完整文本，不再重复绘制彩色内容。普通文字、颜色和点击区域采用相同字体与测量参数。图形检查包含重复颜色、点击选项、链接、行距、刷新及长列表滚动。

合入 108 的增量词表后，源码维护 3,401 个大小写不重复的词条；既有译文保留，新增管理员对白纳入同一生成流程。旧版 `trial-2026-10-22-r1` 自定义资源包的 3,402 项中包含 `Dead Mine` / `Dead mine` 两个大小写变体，客户端兼容读取并以最后一项为准；源码生成检查仍拒绝大小写重复，避免新词表继续产生冲突。回归检查覆盖旧资源词表的加载兼容性。

管理员组对白与提示维护在 `Terms.zh-CN.json`，覆盖管理子菜单、同组商店与传送员。带日期、城主、装备等动态值的句子通过固定语句片段翻译，保留服务器填入的数据；脚本动作、颜色、变量、URL 与可输入命令保持原样。`Checks/Fixtures/gm-dialogue-source.json` 保存来自 Jev 数据库 GM 目录的 348 条去重样本（扫描 26 个脚本，其中 25 个含对白），由 `GraphicsProbe` 检查可见英文残留和标记保留。可用 `python Tools/Localization/audit_npc_dialogue.py <NPC脚本目录> <输出JSON>` 重新只读导出样本。

自动文字和图形检查不等同于全部真实游戏场景验收。部分旧帮助图片和其他 NPC 自由对话仍可能含英文。运行环境另有 `MonsterObject.Load`、`MapObject.AddBuffEffect` 数组越界及素材缺口记录，本次中文与兼容性工作未解决这些问题。
