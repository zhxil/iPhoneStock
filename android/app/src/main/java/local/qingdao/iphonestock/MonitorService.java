package local.qingdao.iphonestock;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.os.*;
import java.text.SimpleDateFormat;
import java.util.Locale;
import java.util.concurrent.*;

public final class MonitorService extends Service {
    public static final String START = "monitor.start", STOP = "monitor.stop", ONCE = "monitor.once", REFRESH="monitor.refresh";
    public static volatile boolean active, continuous;
    private ScheduledExecutorService worker;
    private State state;
    private AppleClient client;
    private PowerManager.WakeLock wake;
    private volatile boolean cancelled;
    @Override public void onCreate() {
        super.onCreate(); Notifications.channels(this); state = new State(this); client = new AppleClient(state.nextAllowed());
        worker = Executors.newSingleThreadScheduledExecutor();
    }
    @Override public int onStartCommand(Intent intent, int flags, int startId) {
        String action = intent == null ? null : intent.getAction();
        if(REFRESH.equals(action)){if(active)getSystemService(NotificationManager.class).notify(1,Notifications.monitor(this,state.target().storeName+" · "+Stock.label(state.last().kind)+"\n"+state.note()));else stopSelf();return START_NOT_STICKY;}
        if (STOP.equals(action)) { finish("已停止监控；列表保留最近一次查询结果"); return START_NOT_STICKY; }
        // Restarting automatically after a kill cannot safely guarantee the Android foreground budget.
        if (action == null) { finish("监控进程被系统中断，请打开应用后重新开始"); return START_NOT_STICKY; }
        if (active) return START_NOT_STICKY;
        try {
            Notification n = Notifications.monitor(this, "正在准备查询 · " + state.target().storeName);
            if (Build.VERSION.SDK_INT >= 29) startForeground(1, n, ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC); else startForeground(1, n);
            active = true; continuous = START.equals(action); cancelled = false;
            PowerManager pm = getSystemService(PowerManager.class);
            wake = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "iphonestock:monitor"); wake.acquire(6 * 60 * 60 * 1000L);
            state.schedule(Math.max(System.currentTimeMillis(), client.nextAllowed), "正在监控");
            RunLog.add(this,"系统",continuous?"监控已开始":"开始单次查询",state.target().product+"\n"+state.target().storeName);
            worker.schedule(this::round, Math.max(0, client.nextAllowed - System.currentTimeMillis()), TimeUnit.MILLISECONDS);
        } catch (RuntimeException e) { finish("系统未允许后台监控，请打开应用检查通知和省电设置"); Notifications.alert(this, 3, "监控未启动", state.note(), false); }
        return START_NOT_STICKY;
    }
    private void round() {
        try {
            if (cancelled) return;
            Stock.Target target = state.target();
            if (!state.configWarning.isEmpty()) { finish(state.configWarning); return; }
            Stock.Result previous = state.last(); Stock.Result result = client.check(target);
            if (cancelled || Thread.currentThread().isInterrupted()) return;
            state.result(result, client.nextAllowed);
            RunLog.add(this,result.kind==Stock.UNKNOWN?"异常":"查询",Stock.label(result.kind)+" · "+target.product,target.storeName+"\n"+result.detail);
            if (Stock.arrival(previous.kind, result.kind)) { Notifications.alert(this, 2, "有货了 · " + target.storeName, target.product + "\n请前往 Apple 官网确认并购买", state.sound()); RunLog.add(this,"提醒","发现有货，已提交到货通知",target.product+"\n"+target.storeName); }
            if (result.kind == Stock.UNKNOWN && previous.kind != Stock.UNKNOWN) Notifications.alert(this, 3, "监控暂不可信", result.detail, false);
            if (!continuous) { finish("单次查询完成；持续监控请点击开始"); return; }
            long next = Math.max(System.currentTimeMillis() + state.interval() * 1000L, client.nextAllowed);
            state.schedule(next, "正在监控");
            String text = target.storeName + " · " + Stock.label(result.kind) + "\n" + result.detail + "\n下次查询 " + new SimpleDateFormat("HH:mm:ss", Locale.CHINA).format(next);
            getSystemService(NotificationManager.class).notify(1, Notifications.monitor(this, text));
            worker.schedule(this::round, Math.max(0, next - System.currentTimeMillis()), TimeUnit.MILLISECONDS);
        } catch (RuntimeException e) {
            finish("监控发生异常，已停止；请打开应用重新开始");
            Notifications.alert(this, 3, "监控已停止", state.note(), false);
        }
    }
    private void finish(String reason) {
        cancelled = true; active = false; continuous = false;
        state.stopped(reason); if (client != null) client.cancel();
        RunLog.add(this,"系统","监控已停止",reason);
        if (wake != null && wake.isHeld()) wake.release();
        if (worker != null) worker.shutdownNow();
        stopForeground(STOP_FOREGROUND_REMOVE); stopSelf();
        getSystemService(NotificationManager.class).cancel(1);
    }
    @Override public void onTimeout(int startId, int type) {
        finish("Android 已达到后台运行时长限制。请打开应用并点击开始监控。");
        Notifications.alert(this, 4, "后台监控已到时停止", state.note(), true);
    }
    @Override public void onDestroy() {
        cancelled = true;
        if (active) { state.stopped("后台服务已停止，请打开应用重新开始"); active = false; continuous = false; }
        if (client != null) client.cancel();
        if (worker != null) worker.shutdownNow();
        if (wake != null && wake.isHeld()) wake.release();
        super.onDestroy();
    }
    @Override public IBinder onBind(Intent intent) { return null; }
}
