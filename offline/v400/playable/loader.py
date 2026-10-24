'''Replace the archived integer-constant decoder with its pure Java equivalent.
The two game DLLs are already restored. The original native protector is not
loaded; its JNI method is an arithmetic transform at native core 0x5f1c.
'''
import json,re,subprocess,zipfile

AAID_CREATE = '''.method public CreateAAID(Landroid/content/Context;)V
    .locals 1

    const-string v0, ""
    sput-object v0, Ljp/gungho/aaid/AAIDManager;->aaid:Ljava/lang/String;
    const/4 v0, 0x1
    sput v0, Ljp/gungho/aaid/AAIDManager;->enableLimit:I
    sput-boolean v0, Ljp/gungho/aaid/AAIDManager;->ready:Z
    return-void
.end method'''


def method(text, signature):
 found=re.findall(r'^'+re.escape(signature)+r'\n.*?^\.end method',text,re.M|re.S)
 if len(found)!=1:raise RuntimeError('Unexpected Java method contract: '+signature)
 return found[0]


def instructions(body):
 return [line.strip() for line in body.splitlines()
         if line.strip() and not line.strip().startswith(('#','.registers ','.locals '))]


def disable_advertising_id(smali):
 path=smali/'jp/gungho/aaid/AAIDManager.smali'
 text=path.read_text();signature='.method public CreateAAID(Landroid/content/Context;)V'
 old=method(text,signature)
 if old!=AAID_CREATE:
  assert old.count('Ljava/lang/Thread;->start()V')==1
  assert old.count('Ljp/gungho/aaid/AAIDManager$1;-><init>')==1
  assert '->ready:Z' in old and '->GetAAID' not in old
 path.write_text(text.replace(old,AAID_CREATE))
 # JNI readers must still see a completed, empty ID with tracking disabled.
 for name,field,ret in [('Ready()Z','ready:Z','return v0'),('GetAAID()Ljava/lang/String;','aaid:Ljava/lang/String;','return-object v0'),('EnableLimit()I','enableLimit:I','return v0')]:
  reader=method(text,'.method public '+name)
  assert '->'+field in reader and ret in reader


def verify_advertising_id(out,smali):
 # Inspect the assembled DEX, not just the pre-assembly patch text.
 checked=out/'verified-smali'
 subprocess.run(['baksmali','disassemble',str(out/'classes.dex'),'-o',str(checked)],check=True)
 relative='jp/gungho/aaid/AAIDManager.smali';text=(checked/relative).read_text()
 body=method(text,'.method public CreateAAID(Landroid/content/Context;)V')
 assert instructions(body)==instructions(AAID_CREATE)
 assert 'invoke-' not in body and 'new-instance' not in body
 before=(smali/relative).read_text()
 for name in ['Ready()Z','GetAAID()Ljava/lang/String;','EnableLimit()I']:
  signature='.method public '+name
  assert method(before,signature)==method(text,signature)
 # This dead worker is retained for class compatibility but no entry creates it.
 references=[]
 for path in checked.rglob('*.smali'):
  for line in path.read_text().splitlines():
   if 'invoke-' in line and 'Ljp/gungho/aaid/AAIDManager$1;-><init>' in line:
    references.append(str(path.relative_to(checked)))
 assert not references,references
 report={'status':'PASS','assembled_dex_checked':True,
         'create_aaid_starts_thread':False,'create_aaid_calls_google_services':False,
         'completed_state':{'aaid':'','enableLimit':1,'ready':True},
         'jni_getters_unchanged':True,'worker_constructor_call_sites':references,
         'limit':'Structural DEX checks; no Android startup or JNI execution.'}
 (out.parent/'advertising-id-checks.json').write_text(json.dumps(report,indent=2)+'\n')

def create(apk,build,code):
 out=build/'loader';out.mkdir(exist_ok=True);dex=out/'original.dex'
 with zipfile.ZipFile(apk) as z:dex.write_bytes(z.read('classes.dex'))
 smali=out/'smali'
 if not smali.exists():subprocess.run(['baksmali','disassemble',str(dex),'-o',str(smali)],check=True)
 a=smali/'com/google/android/gms/common/api/GoogleApiActivitya.smali'
 s=a.read_text();old='    const-string v0, "__6dba__"\n\n    invoke-static {v0}, Ljava/lang/System;->loadLibrary(Ljava/lang/String;)V'
 if old in s:s=s.replace(old,'    # Offline assemblies are already decoded; no native protector startup.')
 assert '__6dba__' not in s
 a.write_text(s)
 b=smali/'com/google/android/gms/common/api/GoogleApiActivityb.smali'
 original='.method public static native a(I)I\n.end method'
 replacement='''.method public static a(I)I
    .locals 2
    ushr-int/lit8 v0, p0, 0x5
    shl-int/lit8 v1, p0, 0x1b
    or-int/2addr v0, v1
    ushr-int/lit8 v1, v0, 0x10
    mul-int/2addr v1, v1
    ushr-int/lit8 v1, v1, 0xb
    xor-int/2addr v0, v1
    int-to-short v0, v0
    return v0
.end method'''
 s=b.read_text()
 if original in s:s=s.replace(original,replacement)
 assert replacement in s;b.write_text(s)
 # Offline APKs omit Google Play metadata and never need an advertising ID.
 # Leaving the original worker active crashes phones with Play services present.
 disable_advertising_id(smali)
 subprocess.run(['smali','assemble',str(smali),'-o',str(out/'classes.dex')],check=True)
 verify_advertising_id(out,smali)
 return {'classes.dex':(out/'classes.dex').read_bytes()}
