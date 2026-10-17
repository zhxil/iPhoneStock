# 小米超级岛接入状态

本地客户端方案，无 MiPush 服务器或应用秘密。当前版本已构建原生通知、对应商品位图和 miui.focus.param / miui.focus.pics；未配置真实小米 App ID，未申请到正式权限，也未进行实体机上岛验收。

## 后续所需资料

- 包名：local.qingdao.iphonestock
- 应用名：iPhone 库存助手
- 当前发布签名证书 SHA-256：b30174670d92c07aab8206e8748d5363874d62846479083e60038e7a80200498
- 运营场景键：stock_monitor（库存监控，申请时需与小米确认场景与键值）
- 正式签名构建标记：com.xiaomi.xms.BUILD_TYPE_DEBUG=false
- 小米分配的真实 App ID：待提供，通过 build-android.ps1 -XiaomiAppId 写入 com.xiaomi.xms.APP_ID；不填虚假 ID。

官方流程要求应用资料、签名证书、场景预审和正式方案；调试设备白名单及上线权限由小米授予。本次未代发邮件、注册账号、提交审核或获取权限。

## 状态与展示

| 节点 | 摘要 | 展开策略 |
| --- | --- | --- |
| 开始 / 日常更新 | 对应款式图 + 待查询 / 有货 / 无货 / 未知 | 静默，不更新岛排序 |
| 首次到货或再次到货 | 对应款式图 + 有货 | 请求展开与优先展示，持续有货不重复提醒 |
| 手动测试 | 测试提醒，展开态注明非实际库存 | 用户主动操作才请求突出提示 |
| 停止监控 | 移除监控通知 ID 1 | 由系统清除其焦点通知与岛 |

岛摘要不伪装“无货”：网络失败为未知。岛设置有限有效期，避免系统长期展示过期库存；冷却期按下一轮查询时间延长有效期。正常通知保留机型、门店、结果、详情及下轮查询时间。通知点击进入应用，常驻普通通知有停止动作。

查询接口：notification_focus_protocol；canShowFocus provider 在工作线程查询。没有焦点权限时 filterWhenNoPermission=false，继续普通通知。应用开关关闭后更新已有通知并移除测试通知的岛参数；不会借助媒体播放或绕过平台授权。

## 验证边界

纯 JVM 检查通知 schema、静默更新、到货展开、图片引用、测试标识和 3072 字节上限；AOSP 模拟器检查普通通知回退和应用流程。两者都不代表真实小米超级岛验收。实体机应检查权限状态、摘要图与文字、展开态、到货提醒、停止下岛、锁屏和省电行为。

[官方接入流程](https://dev.mi.com/xiaomihyperos/documentation/detail?pId=2132)
[官方开发指南](https://dev.mi.com/xiaomihyperos/documentation/detail?pId=2131)
[版本支持范围](https://dev.mi.com/xiaomihyperos/documentation/detail?pId=2141)
