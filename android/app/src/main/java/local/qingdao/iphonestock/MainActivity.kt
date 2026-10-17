package local.qingdao.iphonestock

import android.Manifest
import android.app.NotificationManager
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.PowerManager
import android.provider.Settings
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.automirrored.filled.List
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.*
import kotlinx.coroutines.delay
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Google Material 3 UI; service, stock parsing and vendor notifications stay in Java. */
class MainActivity : ComponentActivity() {
    private lateinit var state: State
    private var catalog: Catalog? = null
    private var pending: String? = null
    private var prompt by mutableStateOf<Prompt?>(null)
    private data class Prompt(val title: String, val message: String, val confirm: String = "知道了", val action: (() -> Unit)? = null)
    private val notificationPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { allowed ->
        val action = pending; pending = null
        if (allowed && action != null) requestAction(action) else info("没有通知权限，未开始监控。可在通知设置中重新开启。")
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState); enableEdgeToEdge()
        state = State(this); Notifications.channels(this)
        try { catalog = Catalog(this) } catch (e: Exception) { RunLog.add(this, "异常", "目录读取失败", e.toString()) }
        setContent {
            val dark = isSystemInDarkTheme()
            val colors = if (Build.VERSION.SDK_INT >= 31) {
                if (dark) dynamicDarkColorScheme(this) else dynamicLightColorScheme(this)
            } else if (dark) darkColorScheme() else lightColorScheme(primary = Color(0xff1765d1))
            MaterialTheme(colorScheme = colors) { App() }
        }
    }
    override fun onResume() { super.onResume(); HyperIsland.refresh(this, null) }

    @OptIn(ExperimentalMaterial3Api::class)
    @Composable private fun App() {
        var tick by remember { mutableLongStateOf(System.currentTimeMillis()) }
        LaunchedEffect(Unit) { while (true) { tick = System.currentTimeMillis(); delay(1000) } }
        val nav = rememberNavController()
        val backStack by nav.currentBackStackEntryAsState()
        val route = backStack?.destination?.route ?: "monitor"
        val routes = listOf("monitor", "target", "logs", "settings")
        val labels = listOf("监控", "目标", "记录", "设置")
        val titles = listOf("监控", "监控目标", "运行记录", "设置")
        val icons = listOf(Icons.Default.Home, Icons.Default.Add, Icons.AutoMirrored.Filled.List, Icons.Default.Settings)
        fun navigate(destination: String) {
            nav.navigate(destination) { popUpTo(nav.graph.findStartDestination().id) { saveState = true }; launchSingleTop = true; restoreState = true }
        }
        Scaffold(
            topBar = { TopAppBar(title = { Column { Text(titles[routes.indexOf(route).coerceAtLeast(0)]); Text("iPhone 库存助手", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) } }) },
            bottomBar = { NavigationBar { routes.forEachIndexed { index, key -> NavigationBarItem(selected = key == route, onClick = { navigate(key) }, icon = { Icon(icons[index], contentDescription = null) }, label = { Text(labels[index]) }, modifier = Modifier.semantics { contentDescription = "导航${labels[index]}" }) } } }
        ) { padding ->
            // NavHost owns the predictive gesture: no custom BackHandler intercepts it.
            NavHost(navController = nav, startDestination = "monitor", modifier = Modifier.padding(padding).fillMaxSize()) {
                composable("monitor") { Monitor(tick, onTarget = { navigate("target") }) }
                composable("target") { Target(tick, onSaved = { navigate("monitor") }) }
                composable("logs") { Logs(tick) }
                composable("settings") { Preferences(tick) }
            }
        }
        prompt?.let { current -> AlertDialog(onDismissRequest = { prompt = null }, title = { Text(current.title) }, text = { Text(current.message) }, confirmButton = { TextButton(onClick = { prompt = null; current.action?.invoke() }) { Text(current.confirm) } }, dismissButton = { if (current.action != null) TextButton(onClick = { prompt = null }) { Text("取消") } }) }
    }

    @Composable private fun Monitor(now: Long, onTarget: () -> Unit) {
        val target = state.target(); val result = state.last(); val product = product(target.part)
        val active = MonitorService.active
        val stale = active && result.checked > 0 && now - result.checked > maxOf(180000L, state.interval() * 3000L) && now > state.nextCheck() + 60000
        var menu by remember { mutableStateOf(false) }
        val kindColor = when { stale || result.kind == Stock.UNKNOWN -> MaterialTheme.colorScheme.error; result.kind == Stock.IN_STOCK -> if (isSystemInDarkTheme()) Color(0xff80dbaa) else Color(0xff176338); else -> MaterialTheme.colorScheme.onSurfaceVariant }
        LazyColumn(contentPadding = PaddingValues(20.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            item { Column(verticalArrangement = Arrangement.spacedBy(4.dp)) { Text(if (active) if (MonitorService.continuous) "正在后台监控" else "正在单次查询" else "已停止 · 展示最近一次结果", style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.primary); Text(if (active) if (state.nextCheck() > now) "下次查询 ${time(state.nextCheck())} · ${((state.nextCheck() - now) / 1000).coerceAtLeast(0)} 秒" else "正在查询…" else "开始监控后显示下一轮时间", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
            item {
                OutlinedCard(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        ProductPicture(target.part, target.product, Modifier.size(88.dp))
                        Column(Modifier.weight(1f).padding(start = 12.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) { Text(product?.family ?: target.product, style = MaterialTheme.typography.titleLarge); if (product != null) Text("${product.color} · ${product.capacity}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(target.storeName, style = MaterialTheme.typography.bodySmall) }
                        Box { IconButton(onClick = { menu = true }) { Icon(Icons.Default.MoreVert, "更多目标操作") }; DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) { DropdownMenuItem(text = { Text("更换监控目标") }, onClick = { menu = false; onTarget() }, leadingIcon = { Icon(Icons.Default.Edit, null) }) } }
                    }
                    Column(verticalArrangement = Arrangement.spacedBy(5.dp)) { Text(if (stale) "查询可能延迟" else Stock.label(result.kind), style = MaterialTheme.typography.headlineMedium, color = kindColor); Text(if (stale) "结果可能已过期，请检查网络和后台设置。" else result.detail, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant); Text(if (result.checked == 0L) "尚未完成首次查询" else "最近查询 ${time(result.checked)}", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    HorizontalDivider()
                    OutlinedButton(onClick = { openProduct(target.part) }, modifier = Modifier.align(Alignment.End)) { Icon(Icons.AutoMirrored.Filled.ArrowForward, null, Modifier.size(18.dp)); Spacer(Modifier.width(8.dp)); Text("在 Apple 查看") }
                } }
            }
            item { Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) { Button(onClick = { if (active) stop() else requestAction(MonitorService.START) }, modifier = Modifier.weight(1f)) { Text(if (active) "停止监控" else "开始后台监控") }; OutlinedButton(onClick = { requestAction(MonitorService.ONCE) }, enabled = !active && state.nextAllowed() <= now, modifier = Modifier.weight(1f)) { Text(if (!active && state.nextAllowed() > now) "查询冷却中" else "查询一次") } } }
            item { Text(state.configWarning.ifEmpty { state.note() }, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            item { Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { Icon(Icons.Default.Info, null, Modifier.size(16.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant); Text("返回桌面后继续监控 · 库存以 Apple 官网为准", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
        }
    }

    @Composable private fun Target(now: Long, onSaved: () -> Unit) {
        val catalog = catalog
        if (catalog == null) { Box(Modifier.fillMaxSize(), Alignment.Center) { Text("目录读取失败，请重新打开应用。") }; return }
        val existing = state.target(); val initial = product(existing.part)
        var city by rememberSaveable { mutableStateOf(catalog.stores.firstOrNull { it.id == existing.store }?.city ?: "青岛") }
        var storeId by rememberSaveable { mutableStateOf(existing.store) }
        var family by rememberSaveable { mutableStateOf(initial?.family ?: "iPhone Duo") }
        var capacity by rememberSaveable { mutableStateOf(initial?.capacity ?: "512GB") }
        var color by rememberSaveable { mutableStateOf(initial?.color ?: "星光白色") }
        val stores = catalog.stores.filter { it.city == city }
        val capacities = catalog.products.filter { it.family == family }.map { it.capacity }.distinct()
        val colors = catalog.products.filter { it.family == family && it.capacity == capacity }.map { it.color }.distinct()
        val selected = catalog.products.firstOrNull { it.family == family && it.capacity == capacity && it.color == color }
        val selectedStore = stores.firstOrNull { it.id == storeId } ?: stores.firstOrNull()
        val editable = !MonitorService.active && now > 0
        Column(Modifier.fillMaxSize()) {
            LazyColumn(Modifier.weight(1f), contentPadding = PaddingValues(20.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
                item { SectionTitle("取货门店") }
                item { OutlinedCard { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) { Choice("城市", city, catalog.stores.map { it.city }.distinct(), editable) { city = it; storeId = catalog.stores.firstOrNull { s -> s.city == it }?.id ?: "" }; Choice("Apple 直营店", selectedStore?.title ?: "", stores.map { it.title }, editable) { title -> storeId = stores.first { it.title == title }.id } } } }
                item { SectionTitle("选择商品") }
                item { OutlinedCard { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(16.dp)) { ProductPicture(selected?.part ?: "", selected?.title ?: "商品预览", Modifier.size(96.dp)); Column { Text(family, style = MaterialTheme.typography.titleMedium); Text("$color · $capacity", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
                    Choice("机型", family, catalog.products.map { it.family }.distinct(), editable) { family = it; capacity = catalog.products.first { p -> p.family == it }.capacity; color = catalog.products.first { p -> p.family == it }.color }
                    Choice("容量", capacity, capacities, editable) { capacity = it; color = catalog.products.first { p -> p.family == family && p.capacity == it }.color }
                    Choice("颜色", color, colors, editable) { color = it }
                } } }
                item { Text(if (editable) "单目标监控，保存将替换当前目标。" else "正在监控，请先停止后再修改目标。", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            }
            Surface(tonalElevation = 2.dp) { Button(onClick = { if (!MonitorService.active && selected != null && selectedStore != null) try { state.target(Stock.Target(selectedStore.id, selectedStore.title, selected.part, selected.title, selectedStore.location)); state.configWarning = ""; RunLog.add(this@MainActivity, "系统", "监控目标已更新", selected.title + "\n" + selectedStore.title); onSaved() } catch (e: Exception) { info("保存失败：${e.message}") } }, enabled = editable && selected != null, modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 12.dp)) { Text("保存监控目标") } }
        }
    }

    @OptIn(ExperimentalMaterial3Api::class)
    @Composable private fun Choice(label: String, value: String, options: List<String>, enabled: Boolean = true, onValue: (String) -> Unit) {
        var expanded by remember { mutableStateOf(false) }
        ExposedDropdownMenuBox(expanded = expanded && enabled, onExpandedChange = { if (enabled) expanded = !expanded }) {
            OutlinedTextField(value = value, onValueChange = {}, readOnly = true, enabled = enabled, label = { Text(label) }, trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) }, modifier = Modifier.fillMaxWidth().menuAnchor())
            ExposedDropdownMenu(expanded = expanded && enabled, onDismissRequest = { expanded = false }) { options.forEach { option -> DropdownMenuItem(text = { Text(option) }, onClick = { expanded = false; onValue(option) }) } }
        }
    }
    @Composable private fun SectionTitle(title: String) { Text(title, style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.primary) }
    @Composable private fun ProductPicture(part: String, name: String, modifier: Modifier = Modifier) {
        val image = remember(part) { ProductImages.get(this, part)?.asImageBitmap() }
        if (image != null) Image(image, "$name 商品图", modifier) else Box(modifier, contentAlignment = Alignment.Center) { Text("暂无图片", style = MaterialTheme.typography.labelSmall) }
    }

    @Composable private fun Logs(now: Long) {
        var category by rememberSaveable { mutableStateOf("全部类型") }; var query by rememberSaveable { mutableStateOf("") }
        var revision by remember { mutableIntStateOf(0) }
        val all = remember(now, revision) { RunLog.read(this) }
        val filtered = all.filter { (category == "全部类型" || it.category == category) && (it.title + it.detail + it.category).contains(query.trim(), ignoreCase = true) }
        Column(Modifier.fillMaxSize().padding(horizontal = 20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Choice("记录类型", category, listOf("全部类型", "查询", "异常", "提醒", "系统")) { category = it }
            OutlinedTextField(value = query, onValueChange = { query = it }, label = { Text("搜索机型、门店或内容") }, leadingIcon = { Icon(Icons.Default.Search, null) }, trailingIcon = { if (query.isNotEmpty()) IconButton(onClick = { query = "" }) { Icon(Icons.Default.Close, "清除搜索") } }, singleLine = true, modifier = Modifier.fillMaxWidth())
            Text("显示 ${filtered.size} 条 · 本机共 ${all.size} 条（最多 150 条）", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (filtered.isEmpty()) Box(Modifier.weight(1f).fillMaxWidth(), Alignment.Center) { Text(if (all.isEmpty()) "暂无运行记录" else "没有符合筛选条件的记录", color = MaterialTheme.colorScheme.onSurfaceVariant) }
            else LazyColumn(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(bottom = 8.dp)) { itemsIndexed(filtered, key = { index, item -> "${item.at}-$index" }) { _, entry -> OutlinedCard { Column(Modifier.fillMaxWidth().padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) { Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) { Text(entry.category, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary); Text(time(entry.at), style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Text(entry.title, style = MaterialTheme.typography.titleSmall); if (entry.detail.isNotEmpty()) Text(entry.detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } } } }
            Row(Modifier.padding(bottom = 12.dp), horizontalArrangement = Arrangement.spacedBy(12.dp)) { OutlinedButton(onClick = { getSystemService(ClipboardManager::class.java).setPrimaryClip(ClipData.newPlainText("运行记录", filtered.joinToString("\n\n") { it.plain() })) }, modifier = Modifier.weight(1f)) { Text("复制当前列表") }; TextButton(onClick = { prompt = Prompt("清空运行记录？", "仅清空本机记录，监控目标和设置会保留。", "清空") { RunLog.clear(this@MainActivity); revision++ } }, modifier = Modifier.weight(1f)) { Text("清空记录") } }
        }
    }

    @Composable private fun Preferences(now: Long) {
        var interval by remember { mutableIntStateOf(state.interval()) }; var sound by remember { mutableStateOf(state.sound()) }; var island by remember { mutableStateOf(state.island()) }
        val periods = linkedMapOf("每 1 分钟" to 60, "每 2 分钟" to 120, "每 5 分钟" to 300, "每 15 分钟" to 900)
        val notifications = getSystemService(NotificationManager::class.java).areNotificationsEnabled()
        val unrestricted = getSystemService(PowerManager::class.java).isIgnoringBatteryOptimizations(packageName)
        LazyColumn(contentPadding = PaddingValues(20.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            item { SectionTitle("查询与提醒") }
            item { OutlinedCard { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Choice("查询间隔", periods.entries.firstOrNull { it.value == interval }?.key ?: "$interval 秒", periods.keys.toList()) { interval = periods.getValue(it); if (!state.settings(interval, sound)) info("设置保存失败") }
                SettingSwitch("到货提示音", "确认有货时播放通知提示音", sound) { sound = it; if (!state.settings(interval, sound)) info("设置保存失败") }
                OutlinedButton(onClick = { requestAction("test") }) { Text("测试到货提醒") }
            } } }
            item { SectionTitle("小米超级岛") }
            item { OutlinedCard { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                SettingSwitch("展示监控信息", "平时静默更新，有货时突出提示", island) { island = it; state.island(it); if (!it) getSystemService(NotificationManager::class.java).cancel(7); if (MonitorService.active) startService(Intent(this@MainActivity, MonitorService::class.java).setAction(MonitorService.REFRESH)) }
                Text(now.let { HyperIsland.status() }, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Text("需要 HyperOS 支持及小米授予应用权限；未授权时仍使用普通通知。", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                OutlinedButton(onClick = { requestAction("island-test") }, enabled = island) { Text("测试超级岛提示") }
                TextButton(onClick = { HyperIsland.refresh(this@MainActivity) { info(HyperIsland.status() + "\n\n普通通知开关不能代替小米应用授权；正式接入需要真实 App ID、签名及场景审核。") } }) { Text("检查超级岛权限") }
            } } }
            item { SectionTitle("后台与通知") }
            item { OutlinedCard { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text((if (notifications) "通知已开启" else "通知未开启") + " · " + if (unrestricted) "省电限制已放行" else "省电可能延后查询", style = MaterialTheme.typography.bodySmall)
                OutlinedButton(onClick = { open(Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, packageName)) }) { Text("通知设置") }
                OutlinedButton(onClick = { battery() }) { Text("允许锁屏后台运行") }
                TextButton(onClick = { prompt = Prompt("小米后台运行", "保留常驻通知，按系统提示放行省电限制；在应用详情检查后台运行和耗电设置，系统提供时可在最近任务中锁定应用。\n\n重启或强制停止后需手动重新开始。超级岛不会延长后台服务时长。", "应用详情") { open(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:$packageName"))) } }) { Text("小米后台设置说明") }
            } } }
            item { SectionTitle("运行说明") }
            item { Text("Android 15+ 对 dataSync 前台服务通常设有后台 24 小时合计 6 小时预算；回到应用前台会重置系统计时。到时停止并提醒，不能保证全天每分钟查询。省电也可能延后联网。", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            item { Text("1.1.0 · Jetpack Compose / Material 3\n原项目 ENCHIGO/apple-pickup-watcher · GPL-3.0-or-later\n商品图片来自 Apple，权利归 Apple 所有。", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
        }
    }
    @Composable private fun SettingSwitch(title: String, detail: String, checked: Boolean, onChecked: (Boolean) -> Unit) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(16.dp)) { Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) { Text(title, style = MaterialTheme.typography.bodyLarge); Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }; Switch(checked = checked, onCheckedChange = onChecked, modifier = Modifier.semantics { contentDescription = title }) }
    }
    private fun product(part: String) = catalog?.products?.firstOrNull { it.part == part }
    private fun time(value: Long) = SimpleDateFormat("HH:mm:ss", Locale.CHINA).format(Date(value))
    private fun info(message: String) { prompt = Prompt("提示", message) }
    private fun open(intent: Intent) { try { startActivity(intent) } catch (_: RuntimeException) { info("系统未提供此入口，请在应用详情中手动设置。") } }
    private fun openProduct(part: String) { val slug = product(part)?.family?.lowercase(Locale.ROOT)?.replace(" ", "-"); open(Intent(Intent.ACTION_VIEW, Uri.parse("https://www.apple.com.cn/shop/buy-iphone" + if (slug == null) "" else "/$slug/${part.lowercase(Locale.ROOT)}"))) }
    private fun stop() { startService(Intent(this, MonitorService::class.java).setAction(MonitorService.STOP)) }
    private fun requestAction(action: String) {
        if (Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) { pending = action; notificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS); return }
        val manager = getSystemService(NotificationManager::class.java); val channel = manager.getNotificationChannel(Notifications.ARRIVAL)
        if (!manager.areNotificationsEnabled() || channel?.importance == NotificationManager.IMPORTANCE_NONE) { info("请先开启通知与到货提醒渠道，避免漏掉有货提醒。"); return }
        if (action == "test" || action == "island-test") {
            val testIsland = action == "island-test"
            if (testIsland && !state.island()) { info("请先开启超级岛展示开关。"); return }
            Notifications.alert(this, 7, if (testIsland) "超级岛测试 · 非实际库存" else "有货了（测试）", "测试不代表真实有货，请检查通知栏及超级岛。", state.sound())
            RunLog.add(this, "提醒", if (testIsland) "已提交超级岛测试通知" else "已提交测试到货提醒", "此为测试，非真实库存。" + HyperIsland.status())
            info(if (testIsland) "已提交测试通知。${HyperIsland.status()}" else "已发送测试提醒，请查看通知栏"); return
        }
        if (state.configWarning.isNotEmpty()) { info(state.configWarning); return }
        try { startForegroundService(Intent(this, MonitorService::class.java).setAction(action)) } catch (_: RuntimeException) { state.stopped("系统拒绝启动后台服务，请检查权限后重试"); info(state.note()) }
    }
    private fun battery() {
        if (getSystemService(PowerManager::class.java).isIgnoringBatteryOptimizations(packageName)) { info("系统省电限制已放行；仍请保留常驻通知并检查小米后台设置。"); return }
        prompt = Prompt("允许锁屏查询", "锁屏联网和处理器唤醒会增加耗电，系统后台时长限制仍然有效。", "打开系统设置") { open(Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS, Uri.parse("package:$packageName"))) }
    }
}
