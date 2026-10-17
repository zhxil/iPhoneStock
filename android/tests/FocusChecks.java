import local.qingdao.iphonestock.FocusPayload;
import org.json.*;
import java.nio.charset.StandardCharsets;

public final class FocusChecks {
    private static int passed;
    private static void check(boolean okay,String name){if(!okay)throw new AssertionError(name);System.out.println("PASS "+name);passed++;}
    public static void main(String[] args)throws Exception{
        String raw=FocusPayload.build("无货","iPhone Duo 512GB 星光白色","青岛万象城","下次查询 17:30:00",false,false,60);
        JSONObject p=new JSONObject(raw).getJSONObject("param_v2");
        check(!p.getBoolean("enableFloat")&&!p.getBoolean("islandFirstFloat"),"routine update stays quiet");
        check(!p.getBoolean("filterWhenNoPermission"),"no island permission preserves regular notification");
        check(p.getString("business").equals("stock_monitor"),"stock monitoring identifies its own scenario");
        check(p.getJSONObject("baseInfo").getInt("type")==2&&p.getJSONObject("picInfo").has("pic"),"official text and picture template fields");
        JSONObject island=p.getJSONObject("param_island");
        check(island.getJSONObject("bigIslandArea").getJSONObject("imageTextInfoLeft").getJSONObject("textInfo").getString("title").equals("无货"),"island summary reflects current stock");
        check(island.getJSONObject("smallIslandArea").getJSONObject("picInfo").getString("pic").equals(FocusPayload.IMAGE),"small island references actual product image");
        check(!island.getBoolean("islandOrder")&&island.getInt("islandTimeout")>=180,"routine update does not reorder and expires");
        check(raw.getBytes(StandardCharsets.UTF_8).length<=3072,"notification payload fits Xiaomi byte limit");
        JSONObject alert=new JSONObject(FocusPayload.build("有货","iPhone Duo","青岛万象城","前往官网确认",true,false,60)).getJSONObject("param_v2");
        check(alert.getBoolean("enableFloat")&&alert.getJSONObject("param_island").getBoolean("islandOrder"),"real arrival requests attention");
        JSONObject test=new JSONObject(FocusPayload.build("有货","iPhone Duo","青岛万象城","test",true,true,60)).getJSONObject("param_v2");
        check(test.getJSONObject("baseInfo").getString("title").contains("非实际库存")&&test.getJSONObject("param_island").getJSONObject("bigIslandArea").getJSONObject("imageTextInfoLeft").getJSONObject("textInfo").getString("title").equals("测试提醒"),"test cannot look like real stock");
        System.out.println(passed+" focus payload checks passed");
    }
}
