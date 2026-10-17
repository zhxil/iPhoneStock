package local.qingdao.iphonestock;

import android.app.*;
import android.content.*;
import android.net.Uri;
import android.media.AudioAttributes;

public final class Notifications {
    static final String MONITOR = "stock-monitor", ARRIVAL = "stock-arrival", SILENT = "stock-status";
    public static void channels(Context context) {
        NotificationManager nm = context.getSystemService(NotificationManager.class);
        NotificationChannel monitor = new NotificationChannel(MONITOR, "监控运行状态", NotificationManager.IMPORTANCE_LOW);
        monitor.setDescription("持续显示是否正在后台监控"); monitor.setSound(null, null);
        NotificationChannel arrival = new NotificationChannel(ARRIVAL, "有货与监控停止提醒", NotificationManager.IMPORTANCE_HIGH);
        arrival.enableVibration(true); arrival.setDescription("有货和后台监控被系统停止时提醒");
        NotificationChannel silent = new NotificationChannel(SILENT, "静默状态提醒", NotificationManager.IMPORTANCE_DEFAULT);
        silent.setSound(null, null);
        nm.createNotificationChannel(monitor); nm.createNotificationChannel(arrival); nm.createNotificationChannel(silent);
    }
    static PendingIntent open(Context c) { return PendingIntent.getActivity(c, 10, new Intent(c, MainActivity.class), PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE); }
    public static Notification monitor(Context c, String text) {
        PendingIntent stop = PendingIntent.getService(c, 11, new Intent(c, MonitorService.class).setAction(MonitorService.STOP), PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        State state=new State(c);android.graphics.Bitmap picture=ProductImages.get(c,state.target().part);
        Notification result = new Notification.Builder(c, MONITOR).setSmallIcon(R.drawable.ic_notification).setLargeIcon(picture).setContentTitle("iPhone 库存助手 · 监控中")
            .setContentText(text).setStyle(new Notification.BigTextStyle().bigText(text)).setContentIntent(open(c)).setOngoing(true).setOnlyAlertOnce(true)
            .setCategory(Notification.CATEGORY_SERVICE).addAction(new Notification.Action.Builder(null, "停止监控", stop).build()).build();
        HyperIsland.attach(c,result,Stock.label(state.last().kind),text,false,false);return result;
    }
    public static void alert(Context c, int id, String title, String body, boolean sound) {
        State state=new State(c);
        Notification notification = new Notification.Builder(c, sound ? ARRIVAL : SILENT).setSmallIcon(R.drawable.ic_notification).setLargeIcon(ProductImages.get(c,state.target().part))
            .setContentTitle(title).setContentText(body).setStyle(new Notification.BigTextStyle().bigText(body)).setContentIntent(open(c))
            .setAutoCancel(true).setCategory(Notification.CATEGORY_STATUS).build();
        boolean test=id==7; boolean arrival=id==2;
        // Only real stock arrival and explicit tests may request expansion.
        if(arrival||test)HyperIsland.attach(c,notification,arrival?"有货":"测试",body,true,test);
        c.getSystemService(NotificationManager.class).notify(id, notification);
    }
}
