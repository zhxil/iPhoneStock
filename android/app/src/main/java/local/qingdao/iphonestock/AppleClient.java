package local.qingdao.iphonestock;

import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.time.*;
import java.time.format.DateTimeFormatter;

public final class AppleClient {
    private static final String BASE = "https://www.apple.com.cn";
    public interface ConnectionFactory { HttpURLConnection open(String url) throws IOException; }
    private final ConnectionFactory connections;
    private volatile HttpURLConnection current;
    private boolean warmed;
    public long nextAllowed;
    public AppleClient(long nextAllowed) { this(nextAllowed, url -> (HttpURLConnection)new URL(url).openConnection()); }
    public AppleClient(long nextAllowed, ConnectionFactory connections) {
        this.connections = connections;
        this.nextAllowed = nextAllowed;
        CookieHandler.setDefault(new CookieManager(null, CookiePolicy.ACCEPT_ORIGINAL_SERVER));
    }
    public void cancel() { HttpURLConnection conn = current; if (conn != null) conn.disconnect(); }
    private HttpURLConnection open(String url) throws IOException {
        HttpURLConnection conn = connections.open(url); current = conn;
        conn.setConnectTimeout(15000); conn.setReadTimeout(20000); conn.setInstanceFollowRedirects(true);
        conn.setRequestProperty("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");
        conn.setRequestProperty("Accept-Language", "zh-CN,zh;q=0.9");
        conn.setRequestProperty("Referer", BASE + "/shop/buy-iphone/iphone-duo");
        return conn;
    }
    public Stock.Result check(Stock.Target target) {
        if (System.currentTimeMillis() < nextAllowed) return Stock.unknown("请求冷却中，稍后自动重试");
        nextAllowed = System.currentTimeMillis() + 60000;
        try {
            if (!warmed) {
                warmed = true;
                try {
                    HttpURLConnection warm = open(BASE + "/shop/bag");
                    try { String blocked = throttle(warm); if (blocked != null) return Stock.unknown(blocked); }
                    finally { warm.disconnect(); }
                }
                catch (IOException e) { /* Auxiliary session warm-up does not determine stock. */ }
                if (Thread.currentThread().isInterrupted()) return Stock.unknown("查询已取消");
            }
            String query = "?pl=true&mts.0=regular&location=" + URLEncoder.encode(target.location, "UTF-8") + "&parts.0=" + URLEncoder.encode(target.part, "UTF-8");
            HttpURLConnection conn = open(BASE + "/shop/retail/pickup-message" + query);
            conn.setRequestProperty("Accept", "application/json");
            int code = conn.getResponseCode();
            String blocked = throttle(conn);
            if (blocked != null) return Stock.unknown(blocked);
            if (code < 200 || code >= 300) return Stock.unknown("Apple 查询失败（HTTP " + code + "）");
            long deadline = System.nanoTime() + 25000000000L;
            try (InputStream input = conn.getInputStream(); ByteArrayOutputStream buffer = new ByteArrayOutputStream()) {
                byte[] chunk = new byte[8192]; int n;
                while ((n = input.read(chunk)) != -1) {
                    if (Thread.currentThread().isInterrupted()) return Stock.unknown("查询已取消");
                    if (System.nanoTime() > deadline) return Stock.unknown("读取响应超时");
                    if (buffer.size() + n > 4 * 1024 * 1024) return Stock.unknown("响应过大，未作库存判断");
                    buffer.write(chunk, 0, n);
                }
                return Stock.parse(new String(buffer.toByteArray(), StandardCharsets.UTF_8), target);
            }
        } catch (SocketTimeoutException e) { return Stock.unknown("网络查询超时，请检查移动网络或 Wi-Fi"); }
        catch (IOException | RuntimeException e) { return Stock.unknown("网络请求失败（" + e.getClass().getSimpleName() + "），未作无货判断"); }
        finally { HttpURLConnection conn = current; if (conn != null) conn.disconnect(); current = null; }
    }
    private String throttle(HttpURLConnection conn) throws IOException {
        int code = conn.getResponseCode();
        if (code == 541 || code == 403) {
            nextAllowed = System.currentTimeMillis() + 15 * 60000;
            return "Apple 拦截请求（HTTP " + code + "），冷却 15 分钟；未知不等于无货";
        }
        if (code == 429) {
            nextAllowed = retryTime(conn.getHeaderField("Retry-After"), System.currentTimeMillis());
            return "Apple 限流（HTTP 429），已延后查询";
        }
        return null;
    }
    public static long retryTime(String value, long now) {
        long minimum = now > Long.MAX_VALUE - 120000 ? Long.MAX_VALUE : now + 120000;
        if (value == null) return minimum;
        try {
            long seconds = Long.parseLong(value.trim());
            if (seconds < 0) return minimum;
            // Saturate before multiplying: huge server delays must not wrap to a short retry.
            if (seconds > (Long.MAX_VALUE - now) / 1000L) return Long.MAX_VALUE;
            return Math.max(minimum, now + seconds * 1000L);
        } catch (NumberFormatException e) {
            try { return Math.max(minimum, ZonedDateTime.parse(value.trim(), DateTimeFormatter.RFC_1123_DATE_TIME).toInstant().toEpochMilli()); }
            catch (Exception ignored) { return minimum; }
        }
    }
}
