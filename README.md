# iPhone库存助手

监控 Apple 中国大陆直营店的 iPhone 到店取货库存，支持 Windows 与 Android。到货后提醒，并跳转到对应款式的 Apple 购买页。

Windows 使用 **C# / .NET 10 / WinUI 3**，Android 使用 **Kotlin / Jetpack Compose / Material 3**，查询和后台服务由 Java 实现。两个客户端独立运行，不使用 Rust 或网页套壳。

## 下载与使用

前往 **[Releases 下载](https://github.com/zhxil/iPhoneStock/releases/latest)**：

| 平台 | 当前版本 | 下载文件 | 使用方式 |
| --- | --- | --- | --- |
| Windows x64 | 1.3.1 | `iPhone库存助手-Windows-x64.zip` | 解压后运行目录内的 `IphoneStock.exe`，保留整个文件夹；无需另装 .NET |
| Android 8.0+ | 1.1.0 | `iPhone库存助手-Android-v1.1.0.apk` | 安装 APK，允许通知，然后开始后台监控 |

默认目标为青岛万象城的 iPhone Duo 512GB 星光白色，可在目标页修改城市、直营店和商品。内置目录来自参考项目快照，不在线更新；商品是否可售及实际库存以 Apple 官网为准。工具不自动下单、不预留库存。

## 界面预览

### Windows · 原生 WinUI 3

监控、添加目标、运行记录和设置独立分页；支持系统深浅色实时切换。

| 深色监控 | 浅色监控 |
| --- | --- |
| ![Windows 深色监控](docs/images/windows-dark.png) | ![Windows 浅色监控](docs/images/windows-light.png) |

| 添加目标 | 分类运行记录 |
| --- | --- |
| ![Windows 添加目标](docs/images/windows-target.png) | ![Windows 分类记录](docs/images/windows-logs.png) |

Windows 截图为实际运行画面；部分功能截图拍摄于更名前版本，当前名称为“iPhone库存助手”。

### Android · Google Material 3

底部导航、商品款式图、分类记录、动态取色与预测性返回。以下为 AOSP Android 36 模拟器截图，不代表 HyperOS 超级岛实机效果。

<p>
  <img src="docs/images/android-monitor.png" alt="Android 监控" width="24%">
  <img src="docs/images/android-target.png" alt="Android 目标选择" width="24%">
  <img src="docs/images/android-logs.png" alt="Android 分类记录" width="24%">
  <img src="docs/images/android-dark.png" alt="Android 深色模式" width="24%">
</p>

## 主要功能

- 有货、无货、未知三态显示，请求失败不会显示成无货；到货提醒去重。
- 对应机型与配色的离线商品图，以及匹配商品编号的 Apple 购买入口。
- 每分钟请求节流、异常退避与冷却：403 / 541 冷却 15 分钟，429 至少冷却 2 分钟。
- Windows 支持多目标、同城合并查询、托盘后台运行、108 DIP 任务栏胶囊、开机启动、提示音和可选 Bark / 飞书推送。
- Android 支持单目标、前台服务监控、常驻通知、到货提醒、最近 150 条持久记录和预测性返回。

### 后台运行与通知

Windows 关闭窗口后收进托盘；在设置中可启用“开机启动并开始监控”。配置保存在 `%LOCALAPPDATA%\QingdaoIphoneStock`，为兼容已有版本保留原目录名。移动程序目录后请重新保存开机启动设置。电脑需要联网且不休眠。

Android 需允许通知并按需调整省电策略。Android 15+ 的 `dataSync` 前台服务通常受后台 24 小时合计 6 小时预算限制；服务超时后停止并提示，不能承诺全天每分钟持续监控。重启或强制停止后需重新开始。

小米焦点通知 / 超级岛参数和测试入口已接入；**当前没有小米 App ID 和官方授权，不能保证安装后直接上岛**，仍可使用普通常驻通知与到货提醒。详见 [Android 说明](android/README.md) 和 [超级岛接入说明](android/SUPER_ISLAND.md)。

## 从源码构建

### Windows

需要 .NET 10 SDK、Visual Studio Windows 应用开发工具与 Windows SDK：

```powershell
./scripts/build.ps1
./scripts/package.ps1
```

可用 `-Dotnet '<dotnet.exe路径>'` 指定 SDK，`-FallbackPackages '<NuGet缓存目录>'` 指定依赖缓存。默认输出 `dist/青岛iPhone库存助手`，保留此路径以兼容已有启动配置；公开压缩包名称为 `iPhone库存助手-Windows-x64.zip`。

### Android

需要 JDK 17+、Gradle 9.4.1、Android SDK 36 / Build Tools 36.0.0：

```powershell
./scripts/build-android.ps1 -Sdk '<Android SDK路径>' -JavaHome '<JDK路径>' -Gradle '<gradle.bat路径>'
./scripts/check-android.ps1
./scripts/package-android.ps1
```

构建脚本首次运行时生成本地签名密钥，保存在被 Git 忽略的 `.tools/android-signing`；请自行备份，切勿提交。自行构建的 APK 与 Release 签名不同，不能覆盖安装官方发布包。依赖与操作详情见 [Android README](android/README.md)。

## 验证与限制

Windows 已做 21 项核心检查、界面交互与后台生命周期检查，以及监控、添加、记录、设置四页深色→浅色→深色切换验证。Android 已做 27 项 JVM 检查与 AOSP Android 36 模拟器交互验证。验证概况见 [本版发布说明](docs/releases/v1.3.1.md)。

未完成干净 Windows 虚拟机、实体小米 17 Ultra、长时间锁屏、实际开机重启和外部 Bark / 飞书送达验收。库存与通知送达受网络、系统策略和 Apple 接口影响。无自动更新、在线目录更新或自动下单。

## 项目结构

- `native/Stock.Core`：查询、解析、目录、调度、推送和配置。
- `native/Stock.WinUI`：WinUI 3 界面、托盘与任务栏胶囊。
- `native/Stock.Checks`：离线核心检查；`--live` 可发起真实查询。
- `android`：Android 应用、JVM 检查及接入文档。
- `scripts`：构建、打包及交互验证脚本。

## 来源与许可

基于 [ENCHIGO/apple-pickup-watcher](https://github.com/ENCHIGO/apple-pickup-watcher) 的库存查询协议和中国大陆目录重新实现，参考提交 `66c1da29941ba30cfa7cc13ba1b8f95a65f5f692`。遵循 **GPL-3.0-or-later**，保留上游版权与修改声明，详见 [LICENSE](LICENSE) 和 [NOTICE](NOTICE)。

商品图片来自 Apple 中国大陆公开购买页，来源与款式映射记录在 `native/Stock.WinUI/Assets/Products/index.json`。图片权利归 Apple 所有，代码许可不授予图片权利。本项目与 Apple 无隶属关系。
