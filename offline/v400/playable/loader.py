'''Replace the archived integer-constant decoder with its pure Java equivalent.
The two game DLLs are already restored. The original native protector is not
loaded; its JNI method is an arithmetic transform at native core 0x5f1c.
'''
import re,subprocess,zipfile

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
 subprocess.run(['smali','assemble',str(smali),'-o',str(out/'classes.dex')],check=True)
 return {'classes.dex':(out/'classes.dex').read_bytes()}
