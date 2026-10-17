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
nav('目标')
p=subprocess.Popen([ADB,'-s',SERIAL,'shell','input','swipe','1','1050','650','1050','2000'],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
time.sleep(.95);shot('predictive-back-progress-live');p.communicate(timeout=5);time.sleep(.6)
check(has('开始后台监控'),'continuous-gesture-completes-to-monitor')
# Real query and foreground-service lifecycle on the isolated emulator.
adb('shell','settings','put','global','http_proxy','10.0.2.2:7897')
tap(text='查询一次')
for _ in range(15):
    if has('最近查询'):break
    time.sleep(1)
check(has('最近查询'),'real-query-completes-on-device')
shot('material3-monitor-after-query')
nav('记录');check(has('单次查询完成') or has('监控已停止'),'service-query-records-structured-events');shot('material3-logs-after-query')
nav('监控');tap(text='开始后台监控');check(has('停止监控'),'foreground-monitor-starts-from-Material-button')
adb('shell','input','swipe','1','1050','650','1050','700');time.sleep(.5)
services=adb('shell','dumpsys','activity','services','local.qingdao.iphonestock').decode('utf-8','replace')
(OUT/'foreground-service.txt').write_text(services,encoding='utf-8')
check('isForeground=true' in services,'monitor-remains-foreground-after-return-home')
adb('shell','am','start','-n','local.qingdao.iphonestock/.MainActivity');time.sleep(.7)
tap(text='停止监控');time.sleep(.5)
services=adb('shell','dumpsys','activity','services','local.qingdao.iphonestock').decode('utf-8','replace')
check('isForeground=true' not in services,'stop-button-ends-foreground-service')
adb('shell','cmd','uimode','night','yes');time.sleep(1);shot('material3-dark-monitor')
adb('shell','cmd','uimode','night','no');time.sleep(.8)
(OUT/'device-lifecycle-checks.json').write_text(json.dumps({'timestamp':datetime.now().astimezone().isoformat(),'checks':checks,'device':'AOSP Android 36','xiaomiDevice':False},ensure_ascii=False,indent=2),encoding='utf-8')