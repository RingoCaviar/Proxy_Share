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

## 🚀 使用

填写代理 IP 和端口，拨动“系统代理”开关即可应用；点击“测试连接”可检查代理是否可用。

> ⚠️ 程序会修改当前 Windows 用户的系统代理设置，请确认地址与端口正确。

## 🗺️ 维护

修改代码前请先阅读 [ARCHITECTURE.md](ARCHITECTURE.md)，完成后同步更新项目地图。
