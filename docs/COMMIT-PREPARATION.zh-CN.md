# 提交准备

当前改动作为一个本地中文试玩与 Windows 兼容性提交审查，未执行提交或推送。

建议标题：`feat(client): improve Chinese localization and Windows compatibility`

建议说明：

> 补齐中文物品、NPC 和任务显示，统一维护所有物品类别的名称；保留服务端标识、任务动作和颜色标记。修复窗口分辨率枚举、Direct3D9Ex 纹理上传及密码框 Caps Lock 气泡导致的输入问题。
>
> 将词表生成器、图库工具和本机检查收进仓库，提供独立验证方案和统一运行入口。运行数据库、账号、配置、素材及编译产物留在仓库外。

## 审查范围

1. `Client/MirControls`、`Client/MirGraphics`、`Client/Resolution`：中文显示、输入框和图形兼容；原始 Text 值保持用于协议和比较。
2. `Client/MirObjects`、`Client/MirScenes`、`Client/Localization`：复合名字翻译后分行、守卫称呼、任务全文与词表。
3. `Tools`、`Checks`、`LocalValidation.sln`：可复现生成和无账号验证。工具和检查不引用个人盘符或兄弟运行目录。
4. `.gitignore`、`.gitattributes`、文档：排除运行数据，约定文本格式，说明维护与验证方式。

## 提交前命令

```powershell
python Tools/Localization/generate.py --check
powershell -NoProfile -File Tools/Validate-Local.ps1 -NativeDependenciesDir 'X:\game-runtime\Client'
git diff --check
git status --short
```

检查器提供的固定数据覆盖 1,628 条物品、1,226 个不同显示名、258 个 NPC 名、154 条任务、150 个任务进度字段和 690 条任务/引导对话整句。图形回归需要交互式 Windows 图形会话及原生依赖。

本机整理验证（2026-10-22）：统一验证完整执行通过，包含图形检查；数据库导出与固定样本一致，UI 单帧导出通过。构建 0 错误，保留 WebView2 带入 WPF 依赖产生的 WindowsBase 版本冲突警告。验证日志位于忽略目录 `artifacts/validation`，不加入提交。整理只重新生成源码和构建输出，未部署或重启独立运行环境。

不要暂存整个工作区或独立运行目录。只在此源码仓库内选择审查后的文件；保留上游已有的 Components 依赖及许可说明。本次不新增运行 DLL、账号信息、数据库、地图、图库、恢复包和迁移资料。未解决的问题和真实游戏验收边界见 [开发说明](LOCAL-DEVELOPMENT.zh-CN.md#已知限制)。
