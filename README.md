# Deffatest Unity Plugin

<p align="center">
  <img src="Assets/Deffatest/Resources/DeffatestLogo.png" alt="Deffatest Logo" width="128"/>
</p>

<p align="center">
  <strong>AI-Powered Bug Detection for Unity Games</strong>
</p>

<p align="center">
  <a href="https://deffatest.online">Website</a> •
  <a href="https://docs.deffatest.online/unity">Documentation</a> •
  <a href="https://deffatest.online/register">Get Started Free</a>
</p>

---

## ✨ Features

- 🤖 **AI-Powered Testing** - Autonomous bug detection using advanced ML
- 🎮 **Unity Integration** - Test directly from Editor (Ctrl+Shift+D)
- 🔐 **Secure Storage** - Encrypted API key storage (machine-specific)
- 🔨 **Build Pipeline** - Auto-submit after successful builds
- 📊 **Real-Time Progress** - Live WebSocket status updates
- 📱 **Multi-Platform** - Android, iOS, WebGL, Windows, Mac, Linux

---

## 📋 Requirements

- Unity 2020.3 or higher
- Deffatest account ([free tier available](https://deffatest.online/register))

---

## 🚀 Installation

### Method 1: Unity Package Manager (Recommended)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL**
3. Enter: `https://github.com/KOKRIG/unity-plugin.git?path=Assets/Deffatest`
4. Click **Add**

### Method 2: Clone Repository

```bash
git clone https://github.com/KOKRIG/unity-plugin.git
```

Copy `Assets/Deffatest` folder to your Unity project's `Assets` folder.

### Method 3: Download ZIP

1. Download ZIP from [Releases](https://github.com/KOKRIG/unity-plugin/releases)
2. Extract to your project's `Assets` folder

---

## ⚡ Quick Start

1. **Open Deffatest**: Press `Ctrl+Shift+D` or go to **Window → Deffatest**
2. **Get API Key**: Visit [deffatest.online/dashboard/settings/api-keys](https://deffatest.online/dashboard/settings/api-keys)
3. **Authenticate**: Go to ⚙️ Settings tab → Paste API key → Click **Verify**
4. **Submit Test**: Go to 🚀 Test tab → Enter URL → Click **Start AI Test**

---

## 📖 Usage

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+D` | Open Deffatest window |
| `Ctrl+Shift+T` | Quick submit test |

### Tabs

| Tab | Description |
|-----|-------------|
| 🚀 **Test** | Submit new test (URL or build) |
| 📊 **Status** | Real-time progress & bugs |
| 📋 **History** | Recent tests list |
| ⚙️ **Settings** | Authentication & config |

### Menu Items

- **Window → Deffatest** - Open main window
- **Deffatest → Build → Android APK** - Build APK
- **Deffatest → Build → WebGL** - Build WebGL
- **Deffatest → Build → Current Platform** - Build for current platform
- **Deffatest → Open Builds Folder** - Open builds directory

---

## 🔧 Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Default Test Type | Game | web, mobile, or game |
| Default Duration | 2h | 30m, 1h, 2h, 6h, 12h |
| Auto-submit after build | Off | Submit builds automatically |
| Show notifications | On | Unity editor notifications |

---

## 📊 Plan Limits

| Plan | Tests/Day | Max File Size |
|------|-----------|---------------|
| **Free** | 3 | 100 MB |
| **Pro** | 33 | 500 MB |
| **Chaos** | 100 | 1 GB |

[Upgrade Plan](https://deffatest.online/pricing)

---

## 🐛 Troubleshooting

**"API key verification failed"**
- Ensure key starts with `sk_live_` or `sk_test_`
- Check internet connection

**"Build failed"**
- Add scenes to Build Settings
- Install platform build support via Unity Hub

**"WebSocket connection failed"**
- Check firewall settings
- Restart Unity Editor

---

## 📞 Support

- **Email**: support@deffatest.online
- **Documentation**: [docs.deffatest.online/unity](https://docs.deffatest.online/unity)
- **Issues**: [GitHub Issues](https://github.com/KOKRIG/unity-plugin/issues)

---

## 📄 License

MIT License - See [LICENSE](LICENSE) for details.

---

<p align="center">
  Made with ❤️ by <a href="https://deffatest.online">Deffatest</a>
</p>
