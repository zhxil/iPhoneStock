package local.qingdao.iphonestock;

import org.json.*;

/** Official OS2/OS3 native-notification schema; no platform dependencies. */
public final class FocusPayload {
    public static final String IMAGE="miui.focus.pic_product";
    public static String build(String label,String product,String store,String detail,boolean important,boolean test,int interval) throws JSONException {
        String islandLabel=test?"测试提醒":label;
        JSONObject picture=new JSONObject().put("type",1).put("pic",IMAGE);
        JSONObject text=new JSONObject().put("title",islandLabel).put("showHighlightColor",important);
        JSONObject left=new JSONObject().put("type",1).put("picInfo",picture).put("textInfo",text);
        JSONObject island=new JSONObject().put("islandProperty",1).put("islandOrder",important).put("islandTimeout",Math.max(180,interval+120))
            .put("bigIslandArea",new JSONObject().put("imageTextInfoLeft",left))
            .put("smallIslandArea",new JSONObject().put("picInfo",picture));
        JSONObject payload=new JSONObject().put("protocol",1).put("business","stock_monitor").put("updatable",!test)
            .put("reopen","reopen").put("filterWhenNoPermission",false).put("enableFloat",important).put("islandFirstFloat",important)
            .put("timeout",test?1:720).put("ticker",islandLabel).put("tickerPic",IMAGE).put("aodTitle",islandLabel).put("aodPic",IMAGE)
            .put("param_island",island).put("baseInfo",new JSONObject().put("type",2).put("title",test?"上岛测试 · 非实际库存":label).put("content",product).put("subContent",store).put("extraTitle",detail))
            .put("picInfo",new JSONObject().put("type",1).put("pic",IMAGE));
        return new JSONObject().put("param_v2",payload).toString();
    }
}
