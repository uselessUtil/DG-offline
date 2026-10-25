using System;using System.IO;
class ApkImageReaderTests {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Reject(string path,int offset,int count,int total){
  bool bad=false;try{DGApkImages.Read(path,offset,count,total);}catch(IOException){bad=true;}catch(InvalidOperationException){bad=true;}
  Check(bad,"Accepted invalid image store "+path);
 }
 static void Main(string[] args){
  string dir=args[0];int checks=0;
  foreach(string name in new[]{"good.apk","max-comment.apk"}){
   string path=Path.Combine(dir,name);
   for(int i=0;i<1000;i++){
    int start=(i*37)%500,count=Math.Min(501-start,1+i%79);
    byte[] got=DGApkImages.Read(path,start,count,501);
    Check(got.Length==count,"Slice count");
    for(int j=0;j<count;j++)Check(got[j]==(byte)((start+j)%256),"Wrong image byte");
    checks++;
   }
  }
  foreach(string name in new[]{"missing.apk","compressed.apk","truncated.apk","wrong-local.apk","zip64.apk"})
   Reject(Path.Combine(dir,name),0,10,501);
  string good=Path.Combine(dir,"good.apk");
  Reject(good,-1,10,501);Reject(good,490,12,501);Reject(good,0,0,501);
  Reject(good,0,10,502);Reject("jar:file://"+good,0,10,501);
  Console.WriteLine("PASS APK image reader: "+checks+" exact random slices; max ZIP comment; truncated/missing/compressed/mismatched/local-header/ZIP64/path/bounds rejected; no extraction.");
 }
}
