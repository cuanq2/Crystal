<h1 align="center">
Legend of Mir 2 - Official Public Crystal Source
</h2>

本机中文开发说明：[构建、词表维护与回归检查](docs/LOCAL-DEVELOPMENT.zh-CN.md) · [提交准备](docs/COMMIT-PREPARATION.zh-CN.md)

多仓库部署入口：[cuanq2/mir2](https://github.com/cuanq2/mir2)。配套资源通过私有 [mir2-resources](https://github.com/cuanq2/mir2-resources) Release 分包发布；使用主库的版本清单获取匹配的源码、Jev 数据和资源包。本地多仓库布局下，本项目位于 `mir2/code`，下文命令均在该源码目录执行。

## Windows 本机部署

源码构建只生成程序、依赖库和语言文件。要进入游戏，还需要独立准备客户端素材、服务端数据库、地图和脚本。以下使用仓库根目录下的 `release` 作为运行目录，客户端与服务端均在本机运行。

### 1. 环境与构建

- Windows x64。
- 构建安装 **.NET 8 SDK x64**；仅运行安装 **.NET 8 Desktop Runtime x64**。
- 客户端使用 SlimDX，还需要 x64 的 `msvcr100.dll`、`msvcp100.dll`、`d3dx9_43.dll`、`D3DCompiler_43.dll`。可将这些依赖放在 `Client.exe` 同级；它们不会由本项目构建生成。
- 维护中文词表需要 Python 3.10 或以上；正常运行游戏不需要 Python。

在仓库根目录执行 PowerShell：

```powershell
# 显式选择 x64 SDK，避免 PATH 中的旧版 x86 dotnet。
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
& $dotnet --list-sdks
& $dotnet build .\Client\Client.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '客户端构建失败' }
& $dotnet build .\Server.MirForms\Server.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '服务端构建失败' }
```

构建输出分别位于 `Build/Client/Release` 和 `Build/Server/Release`，**不会自动部署到 `release`**。`LocalValidation.sln` 是回归检查方案，不包含服务端窗口程序；部署服务端需要构建上面的 `Server.MirForms/Server.csproj`。完整 `Legend of Mir.sln` 还包含旧 ASP.NET 网站，构建它应使用 Visual Studio 的 x64 MSBuild；本机游戏不依赖该网站。

### 2. 运行目录与程序部署

```text
release/
├─ Client/
│  ├─ Client.exe、Client.dll、依赖库及运行时 JSON
│  ├─ Mir2Config.ini
│  ├─ KeyBinds.ini
│  ├─ Localization/              客户端语言词表
│  ├─ runtimes/                  构建输出中的原生运行组件
│  ├─ Data/                      图库、着色器、光标等
│  │  └─ Localization/Chinese/   可选的中文 UI 图片覆盖层
│  ├─ Map/                       客户端地图，目录名是单数
│  └─ Sound/                     音效及 SoundList.lst
└─ Server/
   ├─ Server.exe、Server.dll、Server.Library.dll、依赖库及运行时 JSON
   ├─ log4net.config
   ├─ Localization/              服务端语言词表
   ├─ Configs/Setup.ini          服务端主配置，其他配置也保存在 Configs
   ├─ Server.MirDB               游戏内容数据库
   ├─ Server.MirADB              运行后生成的账号/角色存档，需单独备份
   ├─ Maps/                      服务端地图，目录名是复数
   └─ Envir/                     NPC、任务、掉落等脚本及配套数据
```

首次部署时创建 `release/Client`、`release/Server`，从对应构建输出中复制程序及依赖，包括 `.exe`、`.dll`、`.pdb`、`.deps.json`、`.runtimeconfig.json`、图标、`Localization`、`runtimes` 和服务端 `log4net.config`。目录存在时按实际内容处理，不要求每个程序都有 `runtimes`。仅复制 EXE 不足以运行。

资源和运行配置按后续步骤单独准备。`Build` 下启动程序后可能产生默认配置、空数据库或下载素材，不要把整个 `Build` 目录当成完整部署包直接覆盖已有运行环境。

更新已有环境前，正常关闭对应客户端/服务端，并备份配置、数据库和存档。只更新本次构建的程序、依赖和需要更新的语言文件；保留 `Data`、`Map`、`Sound`、`Maps`、`Envir`、`Configs`、账号存档及其他运行数据。`release` 是本地运行目录，不作为源码提交到 Git。

### 3. 资源来源与安装位置

| 内容 | 来源 | 放置位置 |
| --- | --- | --- |
| 图库、角色/怪物/装备图片、音效等 | [MirFiles Crystal 补丁目录](https://mirfiles.co.uk/resources/mir2/crystal/patch/)，文件清单为同目录的 `PList.gz` | 按原相对路径放入 `release/Client/Data`、`Sound` 等目录 |
| 游戏数据库、地图、NPC/任务/掉落脚本和基础配置 | [Suprcode/Crystal.Database](https://github.com/Suprcode/Crystal.Database) 的 `Jev` 数据集 | `Jev/Server.MirDB`、`Maps`、`Envir`、`Configs` 放入 `release/Server`；将配套 `Maps` 中的地图同时复制到客户端 `Map` |
| 中文文字 | 本仓库 `Client/Localization`、`Server.MirForms/Localization` | 随构建产物部署到各自的 `Localization` |
| 中文 UI 图片 | 本项目 `Tools/UiLocalizer` 生成的覆盖层，或已有运行环境中的覆盖层 | `release/Client/Data/Localization/Chinese`；保留原始图库 |
| VC++ 2010 x64 依赖 | [微软 VC++ 2010 SP1 x64 发行包](https://download.microsoft.com/download/1/6/5/165255e7-1014-4d0a-b094-b6a430a6bffc/vcredist_x64.exe) | `msvcr100.dll`、`msvcp100.dll` 放在 `release/Client`，或安装相应运行库 |
| DirectX 旧版依赖 | [微软 DirectX SDK June 2010](https://www.microsoft.com/en-us/download/details.aspx?id=6812)，已有运行环境使用从官方包提取的 x64 DLL | `d3dx9_43.dll`、`D3DCompiler_43.dll` 放在 `release/Client` |

已有试玩环境使用的数据库提交为 `a19f6dca8f5e238d4ed79801820777abbf0a9ca4`。可从 [固定版本的 Jev 目录](https://github.com/Suprcode/Crystal.Database/tree/a19f6dca8f5e238d4ed79801820777abbf0a9ca4/Jev) 获取；不要把不同数据集或版本的数据库、脚本和地图随意混用。首次安装才导入这套数据库和基础配置；已有存档的环境不可用下载包直接覆盖。

MirFiles 是社区资源站。这里记录的是下载来源，不代表已经确认每项素材的原始作者及授权。源码仓库中的许可也不能替代第三方素材的授权说明。

素材下载应保持目录结构，按清单解压和核对长度；`.part` 文件不是完成的资源，不能直接改名使用。使用下载器时只获取素材，不要让补丁器覆盖本项目编译的客户端和中文词表。

**已有资源位置（2026-10-22 核查）：** 可通过 `ssh lx108` 访问 `D:\zq-game-test\mir2-crystal-runtime`。其 `Client`、`Server` 下有现成配套资源。迁入时只选择需要的资源、原生依赖及经过核对的配置；不要顺带覆盖本地程序、词表或复制无关账号存档。

该机器的来源记录和素材下载工具位于：

```text
D:\zq-game-test\LegendOfMir3_Resources\2026-10-21\
├─ download-manifest.json        微软发行包等下载来源记录
└─ crystal/
   ├─ assets-index.json          解析后的补丁清单
   ├─ assets-bichon-result.json  比奇试玩素材下载结果与文件哈希
   ├─ fetch-assets.py            素材下载器，支持 bootstrap / bichon / all
   ├─ database-source/           固定版本的 Crystal.Database 仓库
   └─ vc2010/deployment.json     VC++ DLL 来源、哈希和签名检查记录
```

这些外部工具和资源**不随本仓库提供**。下载器按其所在目录定位原运行环境，不能直接在本仓库照抄执行并假定会写入 `release`。中文图片覆盖层的本地生成方式见[开发说明](docs/LOCAL-DEVELOPMENT.zh-CN.md#中文-ui-图库)。

### 4. 本机连接配置

关闭程序后编辑配置。以下是应合并到现有文件的关键项，不是完整配置文件；其他游戏规则、数据库配套参数及已有账号设置应保留。首次没有客户端配置时，可以先创建包含这些节的 `Mir2Config.ini`，程序会补充其他默认项。

客户端 `release/Client/Mir2Config.ini`：

```ini
[Launcher]
Enabled=False

[Network]
UseConfig=True
IPAddress=127.0.0.1
Port=7000

[Graphics]
FullScreen=False
Borderless=False
AlwaysOnTop=False
Resolution=1024

[Game]
Language=Chinese
```

关闭 Launcher 可直接进入游戏客户端，并避免默认补丁源替换自建版本。Release 默认读取 `Mir2Config.ini`；Debug 构建或带 `-tc` 参数启动会读取 `Mir2Test.ini`，注意不要改错文件。

服务端 `release/Server/Configs/Setup.ini`：

```ini
[General]
VersionPath=..\Client\Client.exe
CheckVersion=True
EnforceDBChecks=True
Language=Chinese

[Network]
IPAddress=127.0.0.1
Port=7000
StartHTTPService=False

[Permission]
AllowNewAccount=True
AllowLogin=True
AllowNewCharacter=True
AllowStartGame=True
```

`VersionPath` 必须指向本次实际运行的客户端 EXE，默认的 `.\Mir2.Exe` 不适用于此目录布局。更新客户端 EXE 后重启服务端，让它重新加载版本哈希。`AllowStartGame` 的源码默认值为 `False`，试玩时需明确开启。保留 `EnforceDBChecks=True` 并准备完整配套数据；关闭检查不能补出缺失的地图和出生点。

首次配置时另将 `[General] GMPassword` 设为自己的值，文档中不保存密码。客户端连接地址与服务端监听地址均使用 `127.0.0.1:7000`，此方案只供本机连接，不包含局域网或公网开放配置。

### 5. 启动与验收

在仓库根目录运行，启动时务必设置工作目录，因为配置、数据库和素材使用相对路径：

```powershell
$serverDir = (Resolve-Path .\release\Server).Path
Start-Process -FilePath (Join-Path $serverDir 'Server.exe') -WorkingDirectory $serverDir

# 等服务端日志出现 Envir Started / Network Started 后，再执行：
$clientDir = (Resolve-Path .\release\Client).Path
Start-Process -FilePath (Join-Path $clientDir 'Client.exe') -WorkingDirectory $clientDir
```

若服务端窗口尚未启动环境，使用窗口中的启动功能，并检查日志；只看到 Server 窗口不代表已经监听。首次通过客户端创建测试账号和角色；已有账号存档应备份保留，不在 README 中记录凭据。

按以下顺序检查：

1. 服务端成功读取数据库、地图和脚本，日志出现网络启动；端口可用 `Get-NetTCPConnection -LocalPort 7000 -State Listen` 检查，并核对所属进程确为本次服务端。
2. 客户端显示登录界面，未因 SlimDX / DirectX 缺少依赖退出。
3. 通过版本校验，登录、选角并进入地图。
4. 实际检查行走、战斗、音效、NPC 对话和中文显示。构建通过或启动器打开均不能替代此步骤。

自动回归可另按[开发说明](docs/LOCAL-DEVELOPMENT.zh-CN.md#构建与验证)执行，并以 `-NativeDependenciesDir` 指向 `release/Client`。这些检查不连接游戏账号，也不能替代实际进图验收。

### 当前资源完整性

2026-10-22 核对的远端记录显示，比奇试玩下载任务完成 467 项、下载错误为 0；这是试玩子集，不是补丁清单中约 9 GB 的全量素材。清单尚缺 `Data/Map/WemadeMir3/Snow/Object1c.Lib`、`Sound/22.wav`、`Sound/109.wav`，历史静态检查还记录了比奇地图部分帧引用不匹配。

本地 `Sound/22.wav` 可使用[脚步声生成脚本](Tools/Audio/README.md)制作替代素材，默认输出到 `release/Client/Sound/22.wav`。这是自行合成的洞穴右脚脚步声，不是原版资源；未同步到远端，仍需进游戏试听。

当前本地 `release` 已整理程序、配置和词表，**资源传输已停止，素材、地图、数据库尚未完整部署**。远端曾能试玩不代表本地已完成验收；后续补齐后应重新按上述步骤检查。

---

<h3 align="center">
  
[![Latest Release](https://img.shields.io/github/v/release/JevLOMCN/mir4?label=release&style=flat-square)](https://github.com/Suprcode/Crystal/releases/latest)
[![License: GPL v2](https://img.shields.io/badge/License-GPL%20v2-blue.svg?style=flat-square)](LICENSE)
![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white)
</h3>

<h2 align="center">
  
Crystal is the most widely used open-source server and client engine for The Legend of Mir 2, developed and maintained by the LOMCN community.
Originally created by Jamie and contributors, Crystal is a modern, fully managed C# implementation of the classic Mir 2 server architecture, designed to be stable, flexible, and easy to build upon.

The project faithfully recreates the gameplay systems of the original 1999 MMORPG by ActozSoft and Wemade Entertainment, while also introducing modern improvements such as:

🧰 Modular, readable C# codebase for both client and server

🗺️ Custom map editor and data tools

⚡ Performance improvements and bug fixes over legacy server files

🌍 Support for custom content, new features, and community expansions

Crystal has become the foundation for most private Legend of Mir 2 servers worldwide, serving as a cornerstone of the Mir development scene since its public release on LOMCN
</h2>

---

## 🔗 Quick Links

### LOMCN Community

- 🛠️ [Build Guide](https://www.lomcn.net/wiki/index.php/Getting_Started)
- 💬 [Wiki](https://www.lomcn.net/wiki/index.php/Crystal)
- 🐞 [Help](https://www.lomcn.net/forum/forums/crystalm2-help.663/)
- 📚 [Tutorials](https://www.lomcn.net/forum/forums/crystalm2-tutorials.634/)

### Project Links

- 🌐 [Databases](https://github.com/Suprcode/Crystal.Database)
- 🌐 [Map Editor](https://github.com/Suprcode/Crystal.MapEditor)

### Official Links

- <img src="https://web-cdn.mironline.co.kr/mir2/icon/mir2_favicon.ico" alt="Wemade" width="20"/> [Wemade Mir 2](https://mir2.mironline.co.kr/)

---

# Contributors:

> [Community Contributors](https://github.com/Suprcode/Crystal/graphs/contributors)

---

## <img src="https://mirfiles.co.uk/resources/mir2/users/Jev/favicon.png" width="40"> Other Projects

- <img src="https://github.com/JevLOMCN/mir4/blob/main/Tools/icons/mir1.png" alt="Mir1" width="20"/> [Mir 1](https://github.com/JevLOMCN/mir1/) | [Database](https://github.com/Suprcode/Carbon.Database) - Remake of ActozSoft's 1997 _The Legend Of Mir 1_
- <img src="https://github.com/JevLOMCN/mir4/blob/main/Tools/icons/mir2.png" alt="Mir2" width="20"/> [Mir 2](https://github.com/Suprcode/Crystal) | [Database](https://github.com/Suprcode/Crystal.Database) | [Map Editor](https://github.com/Suprcode/Crystal.MapEditor) - Remake of ActozSoft/Wemade Entertainment's 1999 _The Legend Of Mir 2_
- <img src="https://github.com/JevLOMCN/mir4/blob/main/Tools/icons/mir3.png" alt="Mir3" width="20"/> [Mir 3](https://github.com/Suprcode/Zircon) | [Database](https://mirfiles.com/resources/mir3/zircon/Database.7z) | [Map Editor](https://www.lomcn.net/forum/threads/map-editor.109317/)- Remake of Wemade Entertainment's 2003 _The Legend Of Mir 3_
- <img src="https://github.com/JevLOMCN/mir4/blob/main/Tools/icons/woool.png" alt="WoOOL" width="20"/> [WoOOL](https://www.lomcn.net/forum/forums/woool-development-project-onyx.857/) - Remake of Shanda Games' (now Shengqu Games) 2003 _The World Of Legend_
- <img src="https://github.com/JevLOMCN/mir4/blob/main/Tools/icons/mir3d.png" alt="Mir3D" width="20"/> [Mir 3D (Moon Spirit)](https://github.com/mir-ethernity/mir-eternal) | [Mir 3D (Holy Cow)](https://github.com/JevLOMCN/Eternal-Legend) - Remake of Shanda Games' (now Shengqu Games) 2016 _Legend Eternal_
- <img src="https://github.com/JevLOMCN/mir4/blob/main/Tools/icons/mir4.png" alt="Mir4" width="20"/> [Mir 4](https://github.com/JevLOMCN/mir4) - Remake of Wemade Entertainment's 2021 _Mir 4_

---
