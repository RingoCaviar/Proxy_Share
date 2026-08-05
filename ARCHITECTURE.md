# Proxy Share 项目地图

> 后续修改前先读本文件；完成修改后按实际结构同步更新。

## 1. 项目概览

Proxy Share 是一个零第三方依赖的 Windows 桌面小工具，通过当前用户注册表启停系统代理，并可测试代理连通性。

- 技术栈：C#、WinForms、GDI+、.NET Framework 4.0
- 程序入口：`Program.Main()`
- 主源码：`HelloMessageBox.cs`
- 构建入口：`compile.bat`
- 产物：`ProxyShare.exe`（不纳入版本控制）

## 2. 文件定位

| 路径 | 职责 | 修改提示 |
| --- | --- | --- |
| `HelloMessageBox.cs` | 全部运行时代码：主题、自绘控件、主窗体、代理读写和测试 | UI 或功能修改首先定位此文件 |
| `compile.bat` | 使用 .NET Framework 4.0 `csc.exe` 以 UTF-8 编译 | 新增系统程序集引用时同步修改 |
| `logo.ico` | EXE 与窗口图标 | 保持轻量；编译时嵌入 |
| `logo.jpg` | 未使用的历史图片 | 不参与构建与版本控制 |
| `.agent/skills/proxy-share-project/` | 项目维护 Skill | 工作流或提交规则改变时更新 |

## 3. 代码结构

`HelloMessageBox.cs` 按以下顺序组织：

1. `Program`：初始化 WinForms 并启动 `MainForm`。
2. `ThemePalette`：创建 Windows 11 风格浅色/深色调色板。
3. `DrawingTools`：提供圆角路径等公共绘制能力。
4. `CardPanel`、`ThemedTextBox`、`ToggleSwitch`、`AccentButton`：零素材自绘控件。
5. `MainForm`：创建布局、跟随系统主题、校验输入、读写代理并协调异步测试。
6. `ProxyEndpoint`、`TestResultKind`、`TestResult`：后台测试的数据载体。

## 4. 关键流程

### 系统代理

```text
拨动 ToggleSwitch
  → TryGetEndpoint 校验 IP 与端口
  → SetProxyStatus 写入 HKCU Internet Settings
  → InternetSetOption 通知系统刷新
  → RefreshProxyStatus 更新开关和状态文字
```

- 注册表路径：`HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings`
- 关键值：`ProxyEnable`、`ProxyServer`
- 开启前必须通过 IP 与 `1–65535` 端口校验；关闭时保留 `ProxyServer`。

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
- 窗口激活时检测主题变化并调用 `ApplyTheme()`。
- `LayoutControls()` 负责有限自适应；目标客户区为 `340×330` 至 `520×440`。
- 不使用按比例缩放字体，不引入图片背景或第三方 UI 库。

## 5. 修改检查表

- UI：同步浅色/深色状态，检查最小、默认、最大尺寸和控件状态。
- 代理：校验失败不得写注册表；异常需要给出用户可见反馈。
- 网络：保持后台执行、超时和资源释放。
- 构建：运行 `cmd /c compile.bat`，确保无额外 DLL 且 EXE 维持轻量。
- 结构：新增类型、文件、入口或数据流后更新本地图。
