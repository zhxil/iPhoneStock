"""Exercise the signed app on the isolated AOSP emulator; never targets a phone."""
import subprocess, time, re, json, xml.etree.ElementTree as ET
from pathlib import Path
from datetime import datetime
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/android-v11'; OUT.mkdir(parents=True,exist_ok=True)
ADB='D:/AndroidSDK/platform-tools/adb.exe'; SERIAL='emulator-5556'
checks=[]
def adb(*args):
    r=subprocess.run([ADB,'-s',SERIAL,*args],capture_output=True,check=True)
    return r.stdout

def dump(name='current'):
    adb('shell','uiautomator','dump','/sdcard/stock-ui.xml')
    data=adb('exec-out','cat','/sdcard/stock-ui.xml')
    (OUT/(name+'.xml')).write_bytes(data)
    return ET.fromstring(data)
def shot(name): (OUT/(name+'.png')).write_bytes(adb('exec-out','screencap','-p'))
def nodes():return list(dump().iter('node'))
def has(text):return any(text in (n.get('text','')+n.get('content-desc','')) for n in nodes())
def tap(text=None,desc=None):
    matches=[n for n in nodes() if (text is None or n.get('text')==text) and (desc is None or n.get('content-desc')==desc)]
    if not matches:raise AssertionError(f'control missing: {text or desc}')
    a=list(map(int,re.findall(r'\d+',matches[-1].get('bounds'))))
    adb('shell','input','tap',str((a[0]+a[2])//2),str((a[1]+a[3])//2));time.sleep(.6)
def nav(label):tap(desc='导航'+label)
def scroll_to(text):
    for _ in range(7):
        if has(text):return
        adb('shell','input','swipe','540','1750','540','700','300');time.sleep(.3)
    raise AssertionError('cannot scroll to '+text)
def check(ok,name):
    if not ok:raise AssertionError(name)
    checks.append(name); print('PASS',name,flush=True)
try:
    check(b'Android SDK' in adb('shell','getprop','ro.product.model'),'isolated-AOSP-emulator')
    nav('监控');check(has('iPhone Duo') and has('在 Apple 查看'),'monitor-and-purchase-action');shot('material3-monitor')
    check(has('商品图') and not has('暂无图片'),'product-thumbnail-present')
    nav('目标');check(has('城市') and has('Apple 直营店'),'target-page-separated');shot('material3-target')
    scroll_to('颜色');tap(text='星光白色');tap(text='夜空色');check(has('夜空色 · 512GB'),'product-color-preview-switches');shot('material3-target-night')
    tap(text='夜空色');tap(text='星光白色')
    nav('设置');scroll_to('测试到货提醒');tap(text='测试到货提醒');check(has('已发送测试提醒'),'notification-test-submitted');tap(text='知道了');shot('material3-settings')
    nav('记录');check(has('已提交测试到货提醒'),'structured-log-receives-real-events');shot('material3-logs')
    tap(text='全部类型');tap(text='提醒');check(has('已提交测试到货提醒'),'log-category-filter')
    tap(text='搜索机型、门店或内容');adb('shell','input','text','NoMatchXYZ');time.sleep(.4);check(has('没有符合筛选条件'),'log-search-empty-state');shot('material3-logs-empty')
    tap(desc='清除搜索');adb('shell','input','keyevent','4');check(has('已提交测试到货提醒'),'log-search-clears')
    nav('设置');scroll_to('测试超级岛提示');tap(text='测试超级岛提示');check(has('已提交测试通知'),'island-test-regular-notification-fallback');tap(text='知道了');shot('material3-island-settings')
    # AOSP gesture navigation: cancel first, then complete the edge gesture.
    nav('目标')
    adb('shell','input','motionevent','DOWN','1','1050');adb('shell','input','motionevent','MOVE','160','1050');time.sleep(.15);shot('predictive-back-mid-gesture')
    adb('shell','input','motionevent','MOVE','2','1050');adb('shell','input','motionevent','UP','2','1050');time.sleep(.6)
    check(has('取货门店'),'predictive-back-cancel-keeps-target-page');shot('predictive-back-cancelled')
    adb('shell','input','swipe','1','1050','600','1050','700');time.sleep(.7)
    check(has('开始后台监控'),'predictive-back-completes-to-monitor');shot('predictive-back-completed')
    adb('shell','input','swipe','1','1050','600','1050','700');time.sleep(.7)
    resumed=adb('shell','dumpsys','activity','activities').decode('utf-8','replace')
    check('local.qingdao.iphonestock/.MainActivity' not in '\n'.join(l for l in resumed.splitlines() if 'mResumedActivity:' in l),'root-back-returns-to-system-home');shot('predictive-back-home')
    adb('shell','am','start','-n','local.qingdao.iphonestock/.MainActivity');time.sleep(.7)
    nav('记录');check(has('已提交测试到货提醒'),'log-history-persists-across-navigation')
except Exception as e:
    shot('ui-failure');(OUT/'material3-ui-checks.json').write_text(json.dumps({'checks':checks,'error':str(e)},ensure_ascii=False,indent=2),encoding='utf-8');raise
(OUT/'material3-ui-checks.json').write_text(json.dumps({'timestamp':datetime.now().astimezone().isoformat(),'checks':checks,'device':'AOSP Android 36, emulator-5556','xiaomiIslandRendered':False},ensure_ascii=False,indent=2),encoding='utf-8')
