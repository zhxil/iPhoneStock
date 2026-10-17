package local.qingdao.iphonestock;

import android.content.Context;
import android.graphics.*;
import android.util.LruCache;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;

final class ProductImages {
    private static JSONArray index;
    private static final LruCache<String,Bitmap> cache=new LruCache<>(8);
    static synchronized Bitmap get(Context c,String part) {
        Bitmap cached=cache.get(part); if(cached!=null)return cached;
        try {
            if(index==null) try(InputStream in=c.getAssets().open("product-images/index.json");ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] buf=new byte[8192];int n;while((n=in.read(buf))!=-1)out.write(buf,0,n);index=new JSONArray(new String(out.toByteArray(),StandardCharsets.UTF_8));}
            for(int i=0;i<index.length();i++) {
                JSONObject e=index.getJSONObject(i);if(!part.equals(e.getString("partNumber")))continue;
                BitmapFactory.Options options=new BitmapFactory.Options(); options.inSampleSize=4;
                Bitmap original;try(InputStream in=c.getAssets().open("product-images/"+e.getString("file"))){original=BitmapFactory.decodeStream(in,null,options);}
                if(original==null)return null;
                JSONArray b=e.getJSONArray("bounds");float sx=original.getWidth()/(float)e.getInt("width"),sy=original.getHeight()/(float)e.getInt("height");
                Rect src=new Rect(Math.round((float)b.getDouble(0)*sx),Math.round((float)b.getDouble(1)*sy),Math.min(original.getWidth(),Math.round((float)b.getDouble(2)*sx)),Math.min(original.getHeight(),Math.round((float)b.getDouble(3)*sy)));
                if(src.width()<=0||src.height()<=0){original.recycle();return null;}
                Bitmap result=Bitmap.createBitmap(256,256,Bitmap.Config.ARGB_8888);float scale=Math.min(240f/src.width(),240f/src.height());
                float w=src.width()*scale,h=src.height()*scale;
                new Canvas(result).drawBitmap(original,src,new RectF((256-w)/2,(256-h)/2,(256+w)/2,(256+h)/2),new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG));
                original.recycle();cache.put(part,result);return result;
            }
        } catch(Exception e) { android.util.Log.w("StockImages","Product image unavailable",e); }
        return null;
    }
}
