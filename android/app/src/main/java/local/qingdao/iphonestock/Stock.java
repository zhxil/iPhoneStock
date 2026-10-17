package local.qingdao.iphonestock;

import org.json.*;

public final class Stock {
    public static final int PENDING = 0, IN_STOCK = 1, OUT_OF_STOCK = 2, UNKNOWN = 3;
    public static final class Target {
        public final String store, storeName, part, product, location;
        public Target(String store, String storeName, String part, String product, String location) {
            this.store = store; this.storeName = storeName; this.part = part; this.product = product; this.location = location;
        }
        public JSONObject json() throws JSONException {
            return new JSONObject().put("store", store).put("storeName", storeName).put("part", part).put("product", product).put("location", location);
        }
        public static Target from(JSONObject obj) throws JSONException {
            Target t = new Target(obj.getString("store"), obj.getString("storeName"), obj.getString("part"), obj.getString("product"), obj.getString("location"));
            if (!t.store.matches("R[0-9]{3,5}") || !t.part.matches("[A-Za-z0-9]+/[A-Za-z0-9]+") || t.location.trim().isEmpty()) throw new JSONException("无效门店或商品编号");
            return t;
        }
    }
    public static Target defaults() { return new Target("R557", "青岛 · 青岛万象城", "MK2P4CH/A", "iPhone Duo 512GB 星光白色", "山东 青岛"); }
    public static final class Result {
        public final int kind;
        public final String detail;
        public final long checked;
        public Result(int kind, String detail) { this(kind, detail, System.currentTimeMillis()); }
        public Result(int kind, String detail, long checked) { this.kind = kind; this.detail = detail; this.checked = checked; }
    }
    public static String label(int kind) {
        switch (kind) { case IN_STOCK: return "有货"; case OUT_OF_STOCK: return "无货"; case UNKNOWN: return "未知"; default: return "待查询"; }
    }
    public static Result unknown(String detail) { return new Result(UNKNOWN, detail); }
    public static Result parse(String raw, Target target) {
        try {
            JSONObject root = new JSONObject(raw);
            JSONObject head = root.optJSONObject("head");
            if (head != null && !head.isNull("status") && !"200".equals(head.get("status").toString())) return unknown("Apple 返回非成功状态，不能判断库存");
            JSONObject body = root.getJSONObject("body");
            if (!body.optString("errorMessage", "").trim().isEmpty()) return unknown("Apple 返回错误，请核对官网商品状态");
            JSONArray stores = body.optJSONArray("stores");
            if (stores == null || stores.length() == 0) {
                JSONObject content = body.optJSONObject("content");
                JSONObject pickup = content == null ? null : content.optJSONObject("pickupMessage");
                stores = pickup == null ? null : pickup.optJSONArray("stores");
            }
            if (stores == null || stores.length() == 0) return unknown("Apple 未返回门店，商品可能尚未开售或暂不可购买");
            JSONObject matched = null;
            for (int i = 0; i < stores.length(); i++) {
                JSONObject s = stores.getJSONObject(i);
                if (target.store.equals(s.optString("storeNumber"))) {
                    if (matched != null) return unknown("门店结果重复，不能确认库存");
                    matched = s;
                }
            }
            if (matched == null) return unknown("响应缺少目标门店");
            JSONObject parts = matched.optJSONObject("partsAvailability");
            JSONObject part = parts == null ? null : parts.optJSONObject(target.part);
            if (part == null) return unknown("响应缺少目标商品");
            String inner = part.optString("partNumber", "");
            if (!inner.isEmpty() && !target.part.equals(inner)) return unknown("响应商品编号不一致");
            String display = part.optString("pickupDisplay", "").trim().toLowerCase(java.util.Locale.ROOT);
            if ("available".equals(display)) return new Result(IN_STOCK, "Apple 确认可取货，请立即前往官网确认并购买");
            if ("unavailable".equals(display)) return new Result(OUT_OF_STOCK, "当前不可到店取货");
            if ("ineligible".equals(display)) return new Result(OUT_OF_STOCK, "当前商品在此门店不支持到店取货");
            return unknown("无法识别库存字段，未作无货判断");
        } catch (Exception e) { return unknown("响应不是有效库存数据，未作无货判断"); }
    }
    public static boolean arrival(int previous, int next) { return next == IN_STOCK && previous != IN_STOCK; }
}
