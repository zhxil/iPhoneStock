import local.qingdao.iphonestock.*;
import org.json.*;

public final class StockChecks {
    private static int passed;
    private static void check(boolean okay, String name) { if (!okay) throw new AssertionError(name); passed++; System.out.println("PASS " + name); }
    private static String fixture(String display, String part, String store) throws Exception {
        JSONObject status = new JSONObject().put("partNumber", part).put("pickupDisplay", display);
        return new JSONObject().put("head", new JSONObject().put("status", 200)).put("body", new JSONObject().put("stores", new JSONArray().put(new JSONObject().put("storeNumber", store).put("partsAvailability", new JSONObject().put("MK2P4CH/A", status))))).toString();
    }
    public static void main(String[] args) throws Exception {
        Stock.Target t = Stock.defaults();
        check(Stock.Target.from(t.json()).part.equals(t.part), "target persistence roundtrip");
        check(Stock.parse(fixture("available", t.part, t.store), t).kind == Stock.IN_STOCK, "available");
        check(Stock.parse(fixture("unavailable", t.part, t.store), t).kind == Stock.OUT_OF_STOCK, "unavailable");
        check(Stock.parse(fixture("ineligible", t.part, t.store), t).kind == Stock.OUT_OF_STOCK, "ineligible");
        check(Stock.parse(fixture("new-value", t.part, t.store), t).kind == Stock.UNKNOWN, "unknown status cannot mean no stock");
        check(Stock.parse(fixture("available", "OTHER", t.store), t).kind == Stock.UNKNOWN, "SKU mismatch");
        check(Stock.parse(fixture("available", t.part, "R001"), t).kind == Stock.UNKNOWN, "store mismatch");
        check(Stock.parse("<html>blocked</html>", t).kind == Stock.UNKNOWN, "HTML is unknown");
        check(Stock.parse("{\"head\":{\"status\":500},\"body\":{\"stores\":[]}}", t).kind == Stock.UNKNOWN, "failed envelope");
        check(Stock.parse("{\"body\":{\"errorMessage\":\"failed\",\"stores\":[]}}", t).kind == Stock.UNKNOWN, "Apple error");
        check(Stock.parse("{\"body\":{\"stores\":[]}}", t).kind == Stock.UNKNOWN, "empty stores");
        JSONObject nested = new JSONObject(fixture("available", t.part, t.store));
        JSONArray stores = nested.getJSONObject("body").getJSONArray("stores");
        nested.getJSONObject("body").remove("stores");
        nested.getJSONObject("body").put("content", new JSONObject().put("pickupMessage", new JSONObject().put("stores", stores)));
        check(Stock.parse(nested.toString(), t).kind == Stock.IN_STOCK, "legacy nested response");
        stores.put(stores.getJSONObject(0));
        check(Stock.parse(nested.toString(), t).kind == Stock.UNKNOWN, "duplicate stores fail closed");
        check(Stock.arrival(Stock.PENDING, Stock.IN_STOCK) && !Stock.arrival(Stock.IN_STOCK, Stock.IN_STOCK) && Stock.arrival(Stock.OUT_OF_STOCK, Stock.IN_STOCK), "arrival deduplication");
        check(AppleClient.retryTime("1", 1000) == 121000, "429 minimum cooldown");
        check(AppleClient.retryTime("180", 1000) == 181000, "429 Retry-After respected");
        check(AppleClient.retryTime("bad", 1000) == 121000, "invalid Retry-After safe default");
        check(AppleClient.retryTime("9223372036854775807", 1000) == Long.MAX_VALUE, "huge Retry-After cannot overflow");
        check(AppleClient.retryTime("172800", 1000) == 172801000, "long Retry-After is not shortened to one day");
        check(AppleClient.retryTime("-1", 1000) == 121000, "negative Retry-After minimum");
        check(AppleClient.retryTime(" Wed, 21 Oct 2037 07:28:00 GMT ", 1000) > 2000000000000L, "HTTP date Retry-After respected");
        for (int code : new int[] { 403, 541, 429 }) {
            int[] calls = { 0 };
            AppleClient blockedClient = new AppleClient(0, url -> {
                calls[0]++;
                return new java.net.HttpURLConnection(new java.net.URL(url)) {
                    @Override public int getResponseCode() { return code; }
                    @Override public String getHeaderField(String key) { return null; }
                    @Override public void connect() { }
                    @Override public void disconnect() { }
                    @Override public boolean usingProxy() { return false; }
                };
            });
            Stock.Result blocked = blockedClient.check(t);
            blockedClient.check(t);
            check(blocked.kind == Stock.UNKNOWN && calls[0] == 1 && blockedClient.nextAllowed > System.currentTimeMillis() + 110000,
                "warm-up HTTP " + code + " stops pickup and manual retry");
        }
        if (args.length > 0 && args[0].equals("--live")) {
            AppleClient client = new AppleClient(0); Stock.Result r = client.check(t);
            System.out.println("LIVE " + Stock.label(r.kind) + " " + r.detail + " checked=" + r.checked);
        }
        System.out.println(passed + " checks passed");
    }
}
