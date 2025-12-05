# Deffatest - AI-Powered Bug Detection for Unity

<p align="center">
  <img src="../Resources/DeffatestLogo.png" alt="Deffatest Logo" width="200"/>
</p>

<p align="center">
  <strong>Automatically test your Unity games with advanced AI and receive detailed bug reports with screenshots and reproduction steps.</strong>
</p>

<p align="center">
  <a href="https://deffatest.online">Website</a> •
  <a href="https://docs.deffatest.online/unity">Documentation</a> •
  <a href="https://discord.gg/deffatest">Discord</a> •
  <a href="https://deffatest.online/register">Get Started Free</a>
</p>

---

## ✨ Features

- 🤖 **AI-Powered Testing** - Autonomous bug detection using advanced ML algorithms
- 🎮 **Unity Integration** - Test directly from Unity Editor (Ctrl+Shift+D)
- 📊 **Real-Time Progress** - Live status updates via WebSocket
- 🐛 **Detailed Reports** - Bug reports with screenshots and steps to reproduce
- ⚡ **Fast Setup** - Get started in under 5 minutes
- 🔄 **Auto-Submit** - Automatically submit builds after successful compilation
- 📱 **Multi-Platform** - Android, iOS, WebGL, Windows, Mac, Linux

---

## 📋 Requirements

- Unity 2020.3 or higher
- Deffatest account (free tier available)
- Internet connection

---

## 🚀 Quick Start

### 1. Get API Key

1. Go to [deffatest.online/register](https://deffatest.online/register)
2. Create a free account
3. Navigate to **Settings → API Keys**
4. Generate a new API key (starts with `sk_live_` or `sk_test_`)

### 2. Open Deffatest Window

- Press **Ctrl+Shift+D** (Cmd+Shift+D on Mac)
- Or go to **Window → Deffatest**

### 3. Authenticate

1. Go to **⚙️ Settings** tab
2. Paste your API key
3. Click **🔐 Verify API Key**
4. You'll see your account info once verified

### 4. Submit Test

**Option A: Test Local Development Server**
1. Go to **🚀 Test** tab
2. Select **Web (URL)** or **Game (Build)**
3. Enter your localhost URL (e.g., `http://localhost:3000`)
4. Choose test duration
5. Click **🚀 Start AI Test**

**Option B: Build and Test APK**
1. Go to **🚀 Test** tab
2. Click **🔨 Build APK**
3. Build completes → Prompted to submit
4. Track progress in **📊 Status** tab

### 5. Track Progress

1. Switch to **📊 Status** tab
2. Watch real-time progress updates
3. See bugs as they're discovered
4. Get notification when test completes
5. View full report in browser

---

## 📖 Features Guide

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+D` | Open Deffatest window |
| `Ctrl+Shift+T` | Quick submit test |

### Auto-Submit After Build

Enable automatic test submission after successful builds:

1. Go to **⚙️ Settings** tab
2. Check **Auto-submit after build**
3. Save

Now every time you build, you'll be prompted to submit for testing.

### Build Integration

The plugin hooks into Unity's build pipeline:

- **Android** - APK files are automatically detected
- **WebGL** - Prompted with deployment instructions
- **Standalone** - Zipped and submitted
- **iOS** - Xcode project exported with instructions

### Real-Time Updates

Get live updates during testing via WebSocket:

- ⏳ Progress percentage
- 🐛 Bug count by severity (critical/high/medium/low)
- ✅ Completion notification
- 📊 Final report link

### Bug Severity Levels

| Severity | Description | Examples |
|----------|-------------|----------|
| 🔴 Critical | Game-breaking bugs | Crashes, progression blockers, data loss |
| 🟠 High | Major functionality issues | Broken features, softlocks, major UI bugs |
| 🟡 Medium | Minor issues affecting UX | Glitches, performance drops, minor bugs |
| 🟢 Low | Cosmetic issues | Visual glitches, typos, minor polish |

---

## 🎯 Use Cases

### Local Development Testing

```
1. Run your game in Play mode or standalone
2. Open Deffatest (Ctrl+Shift+D)
3. Test tab → Web (URL) → Enter: http://localhost:YOUR_PORT
4. Start Test → Track in Status tab
```

### Pre-Release Testing

```
1. Build your game (File → Build Settings → Build)
2. Auto-submit prompt appears (if enabled)
3. Or manually: Deffatest → Test → Build & Submit
4. Wait for completion → Review bug report
5. Fix issues → Re-test
```

### CI/CD Integration

```
1. Enable Auto-submit after build
2. Build via command line: Unity -batchmode -buildTarget Android
3. Plugin auto-submits to Deffatest
4. Get results via webhook (configure in dashboard)
```

---

## ⚙️ Settings

### Default Settings

| Setting | Options | Description |
|---------|---------|-------------|
| Default Test Type | Web, Mobile, Game | Type of test to run |
| Default Duration | 30m, 1h, 2h, 6h, 12h | How long tests run |
| Auto-submit | On/Off | Submit after build |
| Show Notifications | On/Off | Unity editor notifications |
| Auto-open Report | On/Off | Open browser on completion |

### Duration Guide

| Duration | Best For |
|----------|----------|
| 30 minutes | Quick smoke test |
| 1 hour | Standard test |
| **2 hours** | Thorough test (recommended) |
| 6 hours | Deep exploration |
| 12 hours | Exhaustive coverage |

---

## 📊 Test Reports

After test completion, you receive:

- **Bug Summary** - Total bugs by severity
- **Detailed Reports** - Each bug includes:
  - 📸 Screenshot showing the issue
  - 📝 Steps to reproduce
  - ✅ Expected vs ❌ Actual behavior
  - 📍 Location in game
- **Video Recording** - Full test session replay
- **Performance Metrics** - FPS, memory, load times

---

## 💡 Best Practices

1. **Test Early, Test Often** - Run tests during development
2. **Use Appropriate Duration** - Longer tests find more edge cases
3. **Review Reports Thoroughly** - Some bugs may be rare edge cases
4. **Fix and Re-test** - Verify fixes by running new tests
5. **Enable Auto-submit** - Integrate testing into your workflow

---

## 🔧 API Integration

The plugin uses Deffatest REST API:

- **Base URL**: `https://api.deffatest.online`
- **WebSocket**: `wss://api.deffatest.online/ws`
- **Authentication**: API Key (Bearer token)

### Endpoints Used

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/auth/me` | GET | Verify API key |
| `/api/tests` | POST | Submit test |
| `/api/tests/{id}` | GET | Get test status |
| `/api/tests/{id}/cancel` | POST | Cancel test |

---

## 🐛 Troubleshooting

### "API key verification failed"
- Check your API key is correct (starts with `sk_live_` or `sk_test_`)
- Ensure you have internet connection
- Verify your account is active at deffatest.online

### "Build failed"
- Ensure all build settings are configured
- Check Build Settings has at least one scene
- Verify target platform module is installed

### "Test submission failed"
- Check you have tests remaining in your plan
- Verify file size is within limits (free: 100MB, pro: 500MB)
- Ensure stable internet connection

### "WebSocket connection failed"
- Check firewall settings (allow WSS connections)
- Verify WebSocket URL is correct in Settings
- Try restarting Unity Editor

---

## 📈 Plan Limits

| Plan | Tests/Day | Max File Size | Features |
|------|-----------|---------------|----------|
| **Free** | 3 | 100 MB | Basic reports |
| **Pro** | 33 | 500 MB | Advanced reports, Priority |
| **Chaos** | 100 | 1 GB | Enterprise features |

[Upgrade your plan](https://deffatest.online/pricing)

---

## 📞 Support

- **Email**: support@deffatest.online
- **Documentation**: [docs.deffatest.online/unity](https://docs.deffatest.online/unity)
- **Discord**: [Join Community](https://discord.gg/deffatest)
- **GitHub**: [Report Issues](https://github.com/deffatest/unity-plugin/issues)

---

## 📄 License

This plugin is free to use with a Deffatest account.

- [Terms of Service](https://deffatest.online/terms)
- [Privacy Policy](https://deffatest.online/privacy)

---

## 🎉 What's Next?

After your first test:

1. ✅ Review the bug report on the dashboard
2. 🔧 Fix identified issues
3. 🔄 Run another test to verify fixes
4. 📅 Integrate testing into your workflow

**Happy testing! 🚀**

---

<p align="center">
  Made with ❤️ by <a href="https://deffatest.online">Deffatest</a>
</p>
