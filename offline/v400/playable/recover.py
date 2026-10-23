#!/usr/bin/env python3
"""Recover this exact archived APK's protected managed assemblies in container /tmp.
Run with /opt/unity-tools/bin/python; requires unicorn==2.1.4.
Input is hash-locked. No APK is modified. Only the pure native decoder is emulated.
"""
from pathlib import Path
import argparse, hashlib, json, struct, tempfile, zipfile
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_ARM, UC_HOOK_CODE
from unicorn.arm_const import UC_ARM_REG_R0, UC_ARM_REG_R1, UC_ARM_REG_R2, UC_ARM_REG_R3, UC_ARM_REG_SP, UC_ARM_REG_LR, UC_ARM_REG_PC
APK_SHA='b3f2a366d553d0ab67f2c6fee76bde79dceb3a372f7ed64c3de4849fc1d9600e'
EXPECTED={
 'Assembly-CSharp.dll':'b0fd8872dc515aedf6d676c36c9e6099409c5f5a48a972f225741cb99fd4e99f',
 'Assembly-CSharp-firstpass.dll':'f4e5135e97bc17da040536d408f23591843a07d2a425b748da22b46241364cd4'}
def sha(data):return hashlib.sha256(data).hexdigest()
def outer(data):
 seed=struct.unpack_from('<I',data,32)[0];out=bytearray(len(data))
 for i,b in enumerate(data):
  seed=((((seed*seed)&0xffffffff)>>11)+0x1ba379ea)&0xffffffff
  out[i]=(b-seed)&255
 return bytes(out)
def header(data):return struct.unpack_from('<12I',data)
def sections(data):
 h=header(data)
 assert h[2]==48 and 0<h[3]<10000
 return [struct.unpack_from('<IIHH',data,h[2]+12*i) for i in range(h[3])]
def native_blob(native,offset):
 h=header(outer(native[offset:offset+48]))
 length=max(h[0]+h[1],h[4]+h[5],h[6]+h[7])
 assert 48<=length<=len(native)-offset
 return outer(native[offset:offset+length])
def plain_sections(data):
 h=header(data);ss=sections(data)
 assert h[5]==h[7]==0 and all(n==m for s,d,n,m in ss)
 out=bytearray(max(d+m for s,d,n,m in ss))
 for s,d,n,m in ss:out[d:d+m]=data[h[0]+s:h[0]+s+n]
 return bytes(out)
def decode(code,data):
 h=header(data);ss=sections(data);size=max(d+m for s,d,n,m in ss)
 assert size<0x1000000 and len(data)<0x1000000
 CODE,SRC,DST,STACK,AUX,STOP=[i*0x1000000 for i in range(1,7)]
 u=Uc(UC_ARCH_ARM,UC_MODE_ARM)
 for base,sz in [(CODE,0x10000),(SRC,(len(data)+4095)&~4095),(DST,(size+4095)&~4095),(STACK,0x200000),(AUX,4096),(STOP,4096)]:u.mem_map(base,sz)
 u.mem_write(CODE,code);u.mem_write(SRC,data)
 u.mem_write(AUX,struct.pack('<4I',SRC+h[6],h[7],SRC+h[4],h[5]))
 sp=STACK+0x100000
 u.mem_write(sp,struct.pack('<4I',h[3],h[8],AUX if h[7] else 0,AUX+8 if h[5] else 0))
 for reg,value in [(UC_ARM_REG_R0,DST),(UC_ARM_REG_R1,size),(UC_ARM_REG_R2,SRC+h[0]),(UC_ARM_REG_R3,SRC+h[2]),(UC_ARM_REG_SP,sp),(UC_ARM_REG_LR,STOP)]:u.reg_write(reg,value)
 def memset(uc,address,n,user):
  dest=uc.reg_read(UC_ARM_REG_R0);count=uc.reg_read(UC_ARM_REG_R2)
  assert count<0x1000000
  uc.mem_write(dest,bytes([uc.reg_read(UC_ARM_REG_R1)&255])*count)
  uc.reg_write(UC_ARM_REG_PC,uc.reg_read(UC_ARM_REG_LR))
 u.hook_add(UC_HOOK_CODE,memset,begin=CODE+0x2280,end=CODE+0x2280)
 u.emu_start(CODE+0xc49,STOP,count=300000000)
 assert u.reg_read(UC_ARM_REG_PC)==STOP and u.reg_read(UC_ARM_REG_R0)==1
 return bytes(u.mem_read(DST,size))
def dll_header(data):
 out=bytearray(data[:104]);seed=struct.unpack_from('<I',data,8)[0]
 for i in range(52):
  seed=(seed+(((seed*seed)&0xffffffff)>>(11+i%3))+i*0x483d)&0xffffffff
  value=(struct.unpack_from('<H',data,i*2)[0]+seed)&65535
  struct.pack_into('<H',out,i*2,value)
 h=struct.unpack('<26I',out)
 assert h[3]==0xbc026ec1
 return h

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--apk',type=Path,default=Path('/workspace/ディバゲ_4.0.0(紀念)-v20220207073837.apk'))
 args=parser.parse_args();apk=args.apk.read_bytes();assert sha(apk)==APK_SHA,'Unexpected APK: offsets are specific to the audited file'
 out=Path(tempfile.mkdtemp(prefix='dg400-recovered-',dir='/tmp'));managed=out/'managed';managed.mkdir()
 report={'apk':str(args.apk),'apk_sha256':sha(apk),'output_dir':str(out),'assemblies':[],'native_code_execution':'Unicorn ARM pure decoder only; no Android process, JNI or syscalls'}
 with zipfile.ZipFile(args.apk) as z:
  native=z.read('lib/armeabi-v7a/lib__6dba__.so')
  code=plain_sections(native_blob(native,0xa14f0))
  intermediate=decode(code,native_blob(native,0x10ac0))
  core=decode(code,outer(intermediate))
  schedule=core[0x10014:0x10014+244];assert len(schedule)==244
  report['native_sha256']=sha(native);report['decoder_sha256']=sha(code)
  for name in z.namelist():
   if name.startswith('assets/bin/Data/Managed/') and name.endswith('.dll'):
    base=Path(name).name;data=z.read(name)
    if base in EXPECTED:
     raw=data;h=dll_header(raw);start=h[24]
     length=struct.unpack_from('<I',raw,start+32)[0]^struct.unpack_from('<I',raw,start+36)[0]^0x37b2557a
     assert length==h[25] and start+length<=len(raw)
     inner=bytearray(outer(raw[start:start+length]));ih=header(inner)
     assert ih[7]==244
     inner[ih[6]:ih[6]+244]=schedule
     data=decode(code,bytes(inner))
     assert data[:2]==b'MZ' and sha(data)==EXPECTED[base]
     pe=struct.unpack_from('<I',data,0x3c)[0];assert data[pe:pe+4]==b'PE\0\0'
     report['assemblies'].append({'name':base,'encrypted_sha256':sha(raw),'restored_sha256':sha(data),'size':len(data),'pe_offset':pe})
    (managed/base).write_bytes(data)
 assert sha(args.apk.read_bytes())==APK_SHA
 (out/'recovery.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n')
 print(json.dumps(report,indent=2,ensure_ascii=False))
if __name__=='__main__':main()
