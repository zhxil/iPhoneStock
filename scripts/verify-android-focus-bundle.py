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
# Schema routing only: this does not grant Xiaomi authorization or render an island.
old=adb('shell','settings','get','system','notification_focus_protocol').decode().strip()
try:
    adb('shell','settings','put','system','notification_focus_protocol','3')
    adb('shell','am','force-stop','local.qingdao.iphonestock');adb('shell','am','start','-n','local.qingdao.iphonestock/.MainActivity');time.sleep(.7)
    nav('设置');scroll_to('测试超级岛提示');tap(text='测试超级岛提示');tap(text='知道了')
    dump=adb('shell','dumpsys','notification','--noredact').decode('utf-8','replace')
    (OUT/'focus-notification-bundle.txt').write_text(dump,encoding='utf-8')
    check('miui.focus.param' in dump and 'stock_monitor' in dump,'Android-notification-carries-focus-JSON')
    check('miui.focus.pics' in dump and 'miui.focus.pic_product' in dump,'Android-notification-carries-product-image-bundle')
    check('filterWhenNoPermission' in dump and '非实际库存' in dump,'fallback-and-test-label-reach-system-notification')
finally:
    if old=='null':adb('shell','settings','delete','system','notification_focus_protocol')
    else:adb('shell','settings','put','system','notification_focus_protocol',old)
    adb('shell','am','force-stop','local.qingdao.iphonestock');adb('shell','am','start','-n','local.qingdao.iphonestock/.MainActivity')
(OUT/'focus-bundle-checks.json').write_text(json.dumps({'timestamp':datetime.now().astimezone().isoformat(),'checks':checks,'scope':'AOSP protocol routing test only; no Xiaomi authorization or island rendering'},ensure_ascii=False,indent=2),encoding='utf-8')