# 🌐 Proxy Share

轻量的 Windows 系统代理开关，使用原生 WinForms 构建，无第三方运行时依赖。

## ✨ 特性

- 🎨 Windows 11 风格，自动跟随系统深浅色主题
- ⚡ 一键启停当前用户的系统代理
- 🛡️ 自动校验 IP 地址与端口
- 🧪 后台测试代理连接，界面保持流畅
- 📦 单文件 EXE，体积仅几十 KB

## 🛠️ 构建

需要 Windows 与 .NET Framework 4.0：

```bat
compile.bat
```

构建结果为 `ProxyShare.exe`。

当前版本记录在 [`VERSION`](VERSION) 中，构建产物的文件版本与程序集版本会自动采用该值。

## 📦 发布

发布新版本时，先更新 `VERSION`（格式为 `X.Y.Z`）并提交，然后创建并推送同版本 tag：

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions 会自动运行测试、编译 Windows EXE，并创建 GitHub Release。Release 包含单独的 `ProxyShare.exe`、ZIP 压缩包及 SHA-256 校验文件。tag 版本必须与 `VERSION` 完全一致。

## 🚀 使用

填写代理 IP 和端口，拨动“系统代理”开关即可应用；点击“测试连接”可检查代理是否可用。

> ⚠️ 程序会修改当前 Windows 用户的系统代理设置，请确认地址与端口正确。

## 🗺️ 维护

修改代码前请先阅读 [ARCHITECTURE.md](ARCHITECTURE.md)，完成后同步更新项目地图。
