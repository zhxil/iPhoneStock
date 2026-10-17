package local.qingdao.iphonestock;

import android.app.Notification;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.drawable.Icon;
import android.net.Uri;
import android.os.Bundle;
import android.provider.Settings;

final class HyperIsland {
    static volatile int version;
    static volatile Boolean permission;
    static volatile boolean appIdConfigured;
    private static int protocol(Context c) { try { return Settings.System.getInt(c.getContentResolver(),"notification_focus_protocol",0); } catch(RuntimeException e) { return 0; } }
    static void refresh(Context context,Runnable completed) {
        Context c=context.getApplicationContext();
        new Thread(()->{
            version=protocol(c);
            appIdConfigured=!c.getString(R.string.xiaomi_app_id).trim().isEmpty();
            try {
                Bundle args=new Bundle();args.putString("package",c.getPackageName());
                Bundle result=c.getContentResolver().call(Uri.parse("content://miui.statusbar.notification.public"),"canShowFocus",null,args);
                permission=result==null?null:result.getBoolean("canShowFocus",false);
            } catch(RuntimeException e) { permission=null; }
            if(completed!=null)new android.os.Handler(android.os.Looper.getMainLooper()).post(completed);
        },"island-permission").start();
    }
    static String status() {
        if(version<2)return "当前系统未报告支持 · 使用普通通知";
        String platform=version>=3?"超级岛":"焦点通知";
        if(!appIdConfigured)return platform+"未配置小米应用授权 · 使用普通通知";
        if(Boolean.TRUE.equals(permission))return platform+"权限已开启 · 展示以系统为准";
        if(Boolean.FALSE.equals(permission))return platform+"未授权 · 仍显示普通通知";
        return platform+"权限未能确认 · 仍保留普通通知";
    }
    static void attach(Context c,Notification notification,String label,String detail,boolean important,boolean test) {
        State state=new State(c);if(!state.island())return;
        int protocol=protocol(c);
        if(protocol<2)return;
        try {
            Stock.Target target=state.target();Bitmap bitmap=ProductImages.get(c,target.part);
            if(bitmap==null)return;
            int validity=(int)Math.min(3600,Math.max(state.interval(),(state.nextCheck()-System.currentTimeMillis())/1000));
            String payload=FocusPayload.build(label,target.product,target.storeName,detail,important,test,validity);
            if(payload.getBytes(java.nio.charset.StandardCharsets.UTF_8).length>3072)return;
            Bundle pics=new Bundle();pics.putParcelable(FocusPayload.IMAGE,Icon.createWithBitmap(bitmap));
            notification.extras.putBundle("miui.focus.pics",pics);notification.extras.putString("miui.focus.param",payload);
        } catch(Exception e) { android.util.Log.w("StockIsland","Using regular notification",e); }
    }
}
