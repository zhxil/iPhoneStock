package local.qingdao.iphonestock;

import android.content.Context;
import org.json.*;
import java.util.*;
import java.io.*;
import java.nio.charset.StandardCharsets;

final class Catalog {
    static final class Store {
        String id, title, location, city;
        @Override public String toString() { return title; }
    }
    static final class Product {
        String part, title, family, capacity, color;
        @Override public String toString() { return title; }
    }
    final List<Store> stores = new ArrayList<>();
    final List<Product> products = new ArrayList<>();
    Catalog(Context c) throws Exception {
        JSONArray regions = new JSONArray(read(c, "stores.json"));
        for (int r = 0; r < regions.length(); r++) {
            JSONObject region = regions.getJSONObject(r);
            if (!"zh_CN".equals(region.getString("locale"))) continue;
            JSONArray states = region.getJSONArray("state");
            for (int i = 0; i < states.length(); i++) {
                JSONArray list = states.getJSONObject(i).getJSONArray("store");
                for (int j = 0; j < list.length(); j++) {
                    JSONObject obj = list.getJSONObject(j), address = obj.getJSONObject("address");
                    Store store = new Store(); store.id = obj.getString("id");
                    store.city = address.getString("city");
                    store.title = address.getString("city") + " · " + obj.getString("name");
                    store.location = address.getString("stateName") + " " + address.getString("city");
                    stores.add(store);
                }
            }
        }
        JSONArray families = new JSONArray(read(c, "products.json"));
        Set<String> unique = new HashSet<>();
        for (int i = 0; i < families.length(); i++) {
            JSONObject f = families.getJSONObject(i);
            if (!"iphone".equals(f.getString("category"))) continue;
            JSONObject data = f.getJSONObject("data"), colors = data.getJSONObject("displayValues").getJSONObject("dimensionColor");
            JSONArray list = data.getJSONArray("products");
            for (int j = 0; j < list.length(); j++) {
                JSONObject item = list.getJSONObject(j); Product p = new Product(); p.part = item.getString("partNumber");
                if (!unique.add(p.part)) continue;
                String type = item.getString("familyType"), name;
                switch (type) {
                    case "iphoneduo": name = "iPhone Duo"; break;
                    case "iphone18pro": name = "iPhone 18 Pro"; break;
                    case "iphone18promax": name = "iPhone 18 Pro Max"; break;
                    case "iphoneair": name = "iPhone Air"; break;
                    default: name = type.replaceFirst("^iphone", "iPhone ");
                }
                String color = colors.getJSONObject(item.getString("dimensionColor")).getString("value").replaceAll("<[^>]*>", "");
                p.title = name + " " + item.getString("dimensionCapacity").toUpperCase(Locale.ROOT) + " " + color;
                p.family = name; p.capacity = item.getString("dimensionCapacity").toUpperCase(Locale.ROOT); p.color = color;
                products.add(p);
            }
        }
    }
    private static String read(Context c, String file) throws IOException {
        try (InputStream input = c.getAssets().open(file); ByteArrayOutputStream buffer = new ByteArrayOutputStream()) {
            byte[] bytes = new byte[8192]; int n; while ((n = input.read(bytes)) != -1) buffer.write(bytes, 0, n);
            return new String(buffer.toByteArray(), StandardCharsets.UTF_8);
        }
    }
}
