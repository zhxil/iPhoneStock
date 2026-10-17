package local.qingdao.iphonestock;

import android.content.Context;
import org.json.*;
import java.util.*;

/** Bounded event history shared by the activity and foreground service. */
final class RunLog {
    static final class Entry {
        final long at; final String category, title, detail;
        Entry(JSONObject o) { at=o.optLong("at"); category=o.optString("category"); title=o.optString("title"); detail=o.optString("detail"); }
        String plain() { return new java.text.SimpleDateFormat("MM-dd HH:mm:ss", Locale.CHINA).format(new Date(at))+" ["+category+"] "+title+"\n"+detail; }
    }
    static synchronized void add(Context c, String category, String title, String detail) {
        try {
            JSONArray old = new JSONArray(c.getSharedPreferences("events-v1",0).getString("entries","[]"));
            JSONArray next = new JSONArray();
            for(int i=Math.max(0,old.length()-149);i<old.length();i++) next.put(old.get(i));
            next.put(new JSONObject().put("at",System.currentTimeMillis()).put("category",category).put("title",title).put("detail",detail));
            c.getSharedPreferences("events-v1",0).edit().putString("entries",next.toString()).apply();
        } catch(JSONException e) { android.util.Log.e("StockLog","Cannot record event",e); }
    }
    static synchronized List<Entry> read(Context c) {
        List<Entry> result = new ArrayList<>();
        try { JSONArray a=new JSONArray(c.getSharedPreferences("events-v1",0).getString("entries","[]")); for(int i=a.length()-1;i>=0;i--)result.add(new Entry(a.getJSONObject(i))); }
        catch(JSONException e) { android.util.Log.e("StockLog","Cannot read event history",e); }
        return result;
    }
    static synchronized void clear(Context c) { c.getSharedPreferences("events-v1",0).edit().remove("entries").apply(); }
}
