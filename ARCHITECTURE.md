# Proxy Share 项目地图

> 后续修改前先读本文件；完成修改后按实际结构同步更新。

## 1. 项目概览

Proxy Share 是一个零第三方依赖的 Windows 桌面小工具，通过当前用户注册表启停系统代理，并可测试代理连通性。

- 技术栈：C#、WinForms、GDI+、.NET Framework 4.0
- 程序入口：`Program.Main()`
- 主源码：`HelloMessageBox.cs`、`ProxyTakeoverLifecycle.cs`
- 构建入口：`compile.bat`
- 测试入口：`test.bat`
- 产物：`ProxyShare.exe`（不纳入版本控制）

## 2. 文件定位

| 路径 | 职责 | 修改提示 |
| --- | --- | --- |
| `HelloMessageBox.cs` | 主题、自绘控件、主窗体、代理配置值与连接测试 | UI、输入校验或连接测试修改首先定位此文件 |
| `ProxyTakeoverLifecycle.cs` | 代理接管生命周期及内部 Windows 注册表 / WinInet 适配器 | 接管、恢复、回滚或外部修改策略集中在此文件 |
| `ProxyTakeoverLifecycleTests.cs` | 通过生命周期接口验证接管安全行为 | 不得访问真实注册表或修改系统代理 |
| `compile.bat` | 使用 .NET Framework 4.0 `csc.exe` 以 UTF-8 编译 | 新增系统程序集引用时同步修改 |
| `test.bat` | 在临时目录编译并运行零依赖生命周期测试 | 修改代理接管逻辑后运行 |
| `CONTEXT.md` | 项目领域语言 | 领域术语明确后同步更新 |
| `logo.ico` | EXE 与窗口图标 | 保持轻量；编译时嵌入 |
| `logo.jpg` | 未使用的历史图片 | 不参与构建与版本控制 |
| `.agent/skills/proxy-share-project/` | 项目维护 Skill | 工作流或提交规则改变时更新 |

## 3. 代码结构

`HelloMessageBox.cs` 按以下顺序组织：

1. `Program`：初始化 WinForms 并启动 `MainForm`。
2. `ThemePalette`：创建 Windows 11 风格浅色/深色调色板。
3. `DrawingTools`：提供圆角路径等公共绘制能力。
4. `CardPanel`、`ThemedTextBox`、`ToggleSwitch`、`AccentButton`：零素材自绘控件。
5. `MainForm`：创建布局、跟随系统主题、校验输入、发送代理接管意图、呈现结果并协调异步测试。
6. `ProxyConfiguration`：读取、写入、比较、展示和持久化完整代理配置；存在标记保持私有，调用方通过配置转换表达启用或停用手动代理。
7. `ProxyEndpoint`、`TestResultKind`、`TestResult`：后台测试的数据载体。

`ProxyTakeoverLifecycle.cs` 提供五个生命周期操作：初始化、启用、停用或恢复、观察外部修改、标记正常退出。注册表与 WinInet 适配器是模块内部实现，MainForm 不直接处理恢复快照或回滚。

## 4. 关键流程

### 系统代理

```text
拨动 ToggleSwitch
  → TryGetEndpoint 校验 IP / 主机名与端口
  → ProxyTakeoverLifecycle.Enable 接收已验证的代理端点
  → 生命周期模块持久化原配置快照和预期配置
  → 内部适配器事务式写入 HKCU Internet Settings
  → InternetSetOption 通知系统刷新并复读确认
  → MainForm 根据统一结果更新开关、真实地址和状态文字
```

- 注册表路径：`HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings`
- 关键值：`ProxyEnable`、`ProxyServer`、`ProxyOverride`、`AutoConfigURL`、`AutoDetect`
- 应用状态路径：`HKCU\Software\ProxyShare`，保存最近目标、接管快照、预期配置和正常退出标记。
- 开启时合并原绕过项与本地/私网规则，暂停 PAC 和自动检测；关闭时恢复接管前的完整配置。
- 窗口重新激活时复读系统配置；检测到外部修改后放弃旧快照，不覆盖外部配置。
- 读取、快照持久化、写入、系统刷新通知或复读校验失败均返回中文错误；写入后失败必须回滚并复读确认，回滚失败时明确提示人工检查。
- 异常退出后仅在当前配置仍完整匹配预期配置（包括注册表值存在性）时询问是否恢复。

### 连接测试

```text
点击测试连接
  → 校验输入
  → BackgroundWorker 后台执行
  → Ping 检查主机
  → WebRequest 经指定代理访问 Google 204 地址
  → 主线程显示成功 / 警告 / 失败
```

测试中禁用按钮，禁止并发测试；不得把阻塞网络调用移回 UI 线程。

### 主题与布局

- 主题来源：`HKCU\...\Themes\Personalize\AppsUseLightTheme`。
- 窗口激活时检测主题变化并复读系统代理状态。
- `LayoutControls()` 负责有限自适应；目标客户区约为 `340×380` 至 `520×490`。
- 不使用按比例缩放字体，不引入图片背景或第三方 UI 库。

## 5. 修改检查表

- UI：同步浅色/深色状态，检查最小、默认、最大尺寸和控件状态。
- 代理：校验失败不得写注册表；异常需要给出用户可见反馈。
- 网络：保持后台执行、超时和资源释放。
- 构建：运行 `cmd /c compile.bat`，确保无额外 DLL 且 EXE 维持轻量。
- 测试：运行 `cmd /c test.bat`，覆盖启用、恢复、外部修改、读写/通知/复读失败、回滚失败和异常退出恢复，且不触碰真实系统代理。
- 结构：新增类型、文件、入口或数据流后更新本地图。
