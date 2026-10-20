# iPhone 库存助手 · Android 1.1.1

界面采用 **Kotlin + Jetpack Compose + Google Material 3 + Navigation Compose**；库存查询、配置和前台服务沿用 Java。不是网页套壳，不使用 Rust；手机独立查询，不依赖电脑或 Google Play 服务。

安装包：`dist/android/iPhone库存助手-Android-v1.1.1.apk`。Android 8.0（API 26）以上，compile / target SDK 36；包含 arm64-v8a、armeabi-v7a、x86、x86_64。沿用 1.0 的签名与包名，可直接覆盖安装，避免卸载导致配置丢失。

## 界面与操作

- 监控、目标、记录、设置使用 Material 3 底部导航独立呈现；支持系统深浅色和 Android 12+ 动态取色。
- 监控卡片和目标页显示对应机型与配色的 Apple 商品图。27 张离线图片覆盖 73 个商品编号，切换颜色即时更新预览。
- “在 Apple 查看”为卡片独立操作区内的按钮，打开对应款式与容量购买页；更换目标位于更多菜单或目标页。
- 仍为单目标监控。目标页按城市、直营店、机型、容量、颜色选择，保存替换原目标，运行中禁止修改。
- 运行记录按查询、异常、提醒、系统分类，可筛选、搜索、复制当前列表及确认后清空。本机最多保存最近 150 条，重启后仍可查看。
- 使用 Navigation Compose 的预测性返回支持，Manifest 开启 OnBackInvokedCallback。子页面返回监控页，取消手势保留当前页面；监控页返回桌面由系统处理。需要系统手势导航和相应系统版本支持。

## 安装与后台运行

1. 发送 APK 到手机，按系统提示允许此来源安装，然后覆盖安装。
2. 设置页测试到货提醒，允许通知，检查通知渠道及声音。
3. 根据需要放行省电限制；小米应用详情可检查后台运行和耗电策略。
4. 返回监控页开始监控。默认青岛万象城 R557 / iPhone Duo 512GB 星光白色 MK2P4CH/A，每分钟查询；常驻通知可停止监控。

有货、无货、未知分开显示。请求失败不当成无货；403 / 541 冷却 15 分钟，429 至少冷却 2 分钟。冷却和目标保存在本机；持续有货不重复提醒，离开有货后再次有货会重新提醒。

前台服务使用 dataSync，Android 15+ 通常有后台 24 小时合计 6 小时预算。回到应用前台会重置系统计时；服务处理系统超时并停止提醒。省电可能延后网络，不能承诺全天每分钟持续查询。重启、强制停止或服务结束后需手动重新开始。超级岛不会延长后台运行预算。

## 小米超级岛

已接入小米官方本地焦点通知 / 超级岛扩展参数，设置中有开关、系统权限状态及测试入口。OS2 为焦点通知，OS3 为超级岛；平时静默更新，实际到货才请求突出提示。岛图使用对应商品图片，通知点击回到应用；停止时清除监控通知及其岛状态。

**当前未配置小米 App ID，也未获得应用授权；不能承诺这份 APK 安装后直接上岛。** 未授权或非小米系统保留普通常驻通知和到货提醒，`filterWhenNoPermission=false`，不吞掉通知。测试有明确“非实际库存”标识。

用户已选择先完成 APK。后续取得小米 App ID、签名及场景授权后可通过构建参数接入。详情见 [SUPER_ISLAND.md](SUPER_ISLAND.md)。不使用媒体通知伪装库存监控。

## 构建与检查

需要 JDK 17+（本机 JDK 25）、Gradle 9.4.1、Android SDK 36、Build Tools 36.0.0。依赖固定为 AGP 9.2.0、Kotlin / Compose Compiler 2.3.21、Material 3 1.4.0、Navigation Compose 2.9.7。脚本在临时英文路径构建，规避 Windows 中文路径问题。

```powershell
./scripts/build-android.ps1 -Sdk D:/AndroidSDK -JavaHome '<JDK路径>' -Gradle '<Gradle 9.4.1的bin/gradle.bat>'
# 获得真实小米授权后：追加 -XiaomiAppId '<真实App ID>'
./scripts/check-android.ps1
```

签名密钥沿用 `.tools/android-signing`，不要丢失或公开；源码和 APK 包不包含私钥。脚本会检查 v2 / v3 签名及 APK 清单。27 项 JVM 协议检查（17 库存 + 10 超级岛参数）与模拟器交互回执位于 `artifacts/android-v11`。

本次模拟器为 AOSP Android 36，不能证明小米 17 Ultra 的真实岛外观、HyperOS 后台策略、锁屏长时稳定性或通知到达率；未连接该实体手机。商品图来源和图片权利见 NOTICE；代码 GPL-3.0-or-later。

官方依据：[预测性返回](https://developer.android.com/develop/ui/compose/system/predictive-back-setup)、[超级岛开发指南](https://dev.mi.com/xiaomihyperos/documentation/detail?pId=2131)、[超级岛授权流程](https://dev.mi.com/xiaomihyperos/documentation/detail?pId=2132)、[Android 前台服务时长](https://developer.android.com/develop/background-work/services/fgs/timeout)。
