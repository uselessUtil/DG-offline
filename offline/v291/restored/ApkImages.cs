// Stored PNG payloads are read directly from the signed APK, one image at a time.
// No compression library, extraction directory, external permission or giant RVA class.
using System;using System.IO;using System.Text;
internal static class DGApkImages {
 const string Entry="assets/dg-images.bin";
 static readonly object gate=new object();
 static string cachedPath;static long cachedStart,cachedLength;
 static ushort U16(byte[] b,int p){return (ushort)(b[p]|b[p+1]<<8);}
 static uint U32(byte[] b,int p){return (uint)(b[p]|b[p+1]<<8|b[p+2]<<16|b[p+3]<<24);}
 static byte[] Exact(Stream s,int size){
  byte[] data=new byte[size];int done=0;
  while(done<size){int n=s.Read(data,done,size-done);if(n<=0)throw new InvalidOperationException("Truncated APK image entry");done+=n;}
  return data;
 }
 static long Locate(FileStream file,long expected){
  if(file.Length<22)throw new InvalidOperationException("Missing APK directory");
  int tailSize=file.Length<65557L?(int)file.Length:65557;file.Position=file.Length-tailSize;
  byte[] tail=Exact(file,tailSize);int end=-1;
  for(int i=tail.Length-22;i>=0;i--)if(U32(tail,i)==0x06054b50 && i+22+U16(tail,i+20)==tail.Length){end=i;break;}
  if(end<0 || U16(tail,end+4)!=0 || U16(tail,end+6)!=0 || U16(tail,end+8)!=U16(tail,end+10))
   throw new InvalidOperationException("Unsupported APK directory");
  int count=U16(tail,end+10);long directory=U32(tail,end+16),directorySize=U32(tail,end+12);
  if(count==65535 || directory==uint.MaxValue || directory+directorySize>file.Length-tailSize+end)
   throw new InvalidOperationException("Unsupported APK size");
  file.Position=directory;
  for(int i=0;i<count;i++){
   byte[] h=Exact(file,46);if(U32(h,0)!=0x02014b50)throw new InvalidOperationException("Invalid APK entry");
   int names=U16(h,28),extra=U16(h,30),comment=U16(h,32);
   string name=Encoding.UTF8.GetString(Exact(file,names));long next=file.Position+extra+comment;
   if(next>directory+directorySize)throw new InvalidOperationException("Invalid APK entry bounds");
   if(name==Entry){
    if((U16(h,8)&1)!=0 || U16(h,10)!=0 || U32(h,20)!=expected || U32(h,24)!=expected)
     throw new InvalidOperationException("PNG store must be uncompressed and complete");
    file.Position=U32(h,42);byte[] local=Exact(file,30);
    if(U32(local,0)!=0x04034b50 || U16(local,8)!=0)throw new InvalidOperationException("Invalid PNG store header");
    string localName=Encoding.UTF8.GetString(Exact(file,U16(local,26)));
    long start=file.Position+U16(local,28);
    if(localName!=Entry || start+expected>directory)throw new InvalidOperationException("Invalid PNG store range");
    return start;
   }
   file.Position=next;
  }
  throw new FileNotFoundException("Missing local PNG store",Entry);
 }
 public static byte[] Read(string path,int offset,int size,int total){
  if(path==null || !path.EndsWith(".apk") || path.IndexOf("://")>=0)
   throw new InvalidOperationException("Expected local APK path");
  if(offset<0 || size<=0 || size>64*1024*1024 || (long)offset+size>total)
   throw new InvalidOperationException("Invalid PNG slice");
  using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
   long start;
   lock(gate){
    if(cachedPath!=path || cachedLength!=total){long found=Locate(file,total);cachedStart=found;cachedLength=total;cachedPath=path;}
    start=cachedStart;
   }
   if(start+total>file.Length)throw new InvalidOperationException("Truncated PNG store");
   file.Position=start+offset;return Exact(file,size);
  }
 }
}
