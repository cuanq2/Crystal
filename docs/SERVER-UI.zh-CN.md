# 服务端管理界面汉化

服务端使用 `Configs/Setup.ini` 中已有的 `Language=Chinese` 设置。发布输出必须同时包含 `Server.dll` 和 `Localization/Interface.Chinese.json`，然后重新启动服务端。`Language=English` 保留英文界面。

覆盖主窗口、菜单、配置、数据库编辑、账号与角色管理、行会、商城、掉落编辑和地图管理窗口，以及提示框、悬浮说明、表头、状态文字和枚举选项。短属性标签中的“上 / 下”表示属性上限 / 下限。

界面翻译与游戏消息词表 `Localization/Chinese.json` 分开维护。数据库中的名称、玩家输入、账号资料、脚本命令、路径、配置键和诊断日志原文保留；文件扩展名、IP、HTTP、NPC、坐标和版本标识等技术记号保留。系统文件选择窗口的系统按钮语言由 Windows 决定。

## 维护

- `Server.MirForms/Localization/Interface.Chinese.json`：英文原文到中文的显示词典，可直接编辑；不需要额外生成器。
- `Server.MirForms/ServerUi.cs`：统一应用显示翻译、对话框和动态控件处理。新增窗口在 `InitializeComponent()` 后调用 `ServerUi.Apply(this)`。
- 不要翻译数据库字段值或替换下拉框实际对象。按按钮文字、菜单文字或表头判断功能时，使用 `ServerUi.OriginalText(...)`，避免中文导致条件失效。
- 新增列表状态等直接写入 `ListViewItem` 的显示文字时，显式调用 `ServerUi.T(...)`。动态组合文字优先增加完整的格式或短语，避免只翻译零散单词。

## 构建与检查

在源码仓库根目录，使用 .NET 8 x64 SDK：

```powershell
dotnet publish Server.MirForms/Server.csproj -c Release --no-self-contained -o artifacts/server-publish
dotnet run --project Tools/ServerUiAudit -- Server.MirForms artifacts/server-ui-audit.json --check
dotnet run --project Checks/ServerUiCheck -c Release -- artifacts/server-ui-check
```

截图检查输出目录必须不存在；再次运行请换一个目录名。检查在独立目录中创建测试配置，禁用窗口的启动/保存回调，不读取实际账号存档，不启动游戏服务。它验证主界面与 29 个可直接构造的管理窗口，并输出 PNG、窗口文案清单和回归结果。检查还覆盖英文回退、动态状态、枚举原值、表格绑定和按钮模式判断。

静态审计检查已识别的界面文案都有词典条目；新增复杂插值、日志或数据驱动的文字仍需人工检查。截图验证不能代替实际数据库编辑、玩家在线管理和不同 DPI 下的操作验收。

地图窗口的实际加载、页签切换、鼠标操作及关闭由独立的[地图编辑器回归检查](MAP-EDITOR.zh-CN.md)覆盖。

更新已有运行目录前正常退出服务端。备份旧程序和词典，只覆盖本次发布中改变的程序文件与界面词典，保留配置、数据库及账号存档。主库 `tools/setup.py deploy --local-code` 会保留当前源码发布出的语言文件，避免被旧资源包覆盖。
