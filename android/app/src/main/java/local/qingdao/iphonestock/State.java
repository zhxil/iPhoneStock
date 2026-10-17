package local.qingdao.iphonestock;

import android.content.*;
import org.json.JSONObject;

public final class State {
    final SharedPreferences prefs;
    public String configWarning = "";
    public State(Context context) { prefs = context.getSharedPreferences("monitor-v1", Context.MODE_PRIVATE); }
    public Stock.Target target() {
        try {
            String saved = prefs.getString("target", "");
            return saved.isEmpty() ? Stock.defaults() : Stock.Target.from(new JSONObject(saved));
        } catch (Exception e) { configWarning = "保存的目标无法读取，已显示默认目标；请重新选择并保存。"; return Stock.defaults(); }
    }
    public void target(Stock.Target value) throws Exception {
        if (!prefs.edit().putString("target", value.json().toString()).remove("kind").remove("detail").remove("checked").putString("note", "目标已更新，等待首次查询").commit()) throw new Exception("配置写入失败，请检查存储空间");
    }
    public int interval() { return Math.max(60, Math.min(3600, prefs.getInt("interval", 60))); }
    public boolean sound() { return prefs.getBoolean("sound", true); }
    public boolean island() { return prefs.getBoolean("island", true); }
    public boolean island(boolean enabled) { return prefs.edit().putBoolean("island", enabled).commit(); }
    public boolean settings(int interval, boolean sound) { return prefs.edit().putInt("interval", Math.max(60, interval)).putBoolean("sound", sound).commit(); }
    public Stock.Result last() { return new Stock.Result(prefs.getInt("kind", Stock.PENDING), prefs.getString("detail", "尚未发出库存查询"), prefs.getLong("checked", 0)); }
    public void result(Stock.Result value, long nextAllowed) {
        prefs.edit().putInt("kind", value.kind).putString("detail", value.detail).putLong("checked", value.checked).putLong("nextAllowed", nextAllowed).commit();
    }
    public long nextAllowed() { return prefs.getLong("nextAllowed", 0); }
    public long nextCheck() { return prefs.getLong("nextCheck", 0); }
    public void schedule(long next, String note) { prefs.edit().putLong("nextCheck", next).putString("note", note).apply(); }
    public String note() { return prefs.getString("note", "点击开始，手机将独立查询库存"); }
    public void stopped(String reason) { prefs.edit().putLong("nextCheck", 0).putString("note", reason).commit(); }
}
