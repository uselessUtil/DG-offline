// Build-time only. Locate the official icon's face detail in the full portrait.
// No game code, save data or network access is involved.
using System;using System.IO;
class MatchPortrait {
 static int W=256,H=256,TW=20,TH=24;
 static byte[] src,tpl;
 static long Score(int x,int y,int w,int h,bool dense,long limit){
  long sum=0;int step=dense?1:4;
  for(int ty=2;ty<TH-2;ty+=step)for(int tx=2;tx<TW-2;tx+=step){
   int sx=x+tx*w/TW,sy=y+ty*h/TH,p=(sy*W+sx)*4,t=(ty*TW+tx)*3;
   if(src[p+3]<100)return long.MaxValue/4;
   for(int c=0;c<3;c++){int d=src[p+c]-tpl[t+c];sum+=d*d;}
   if(sum>limit)return sum;
  }return sum;
 }
 static void Main(string[] a){
  using(var input=new BinaryReader(File.OpenRead(a[0])))
  using(var output=new StreamWriter(a[1])){
   int count=input.ReadInt32();
   for(int i=0;i<count;i++){
    int id=input.ReadInt32();src=input.ReadBytes(W*H*4);tpl=input.ReadBytes(TW*TH*3);
    long best=long.MaxValue;int bx=0,by=0,bw=32,bh=38;
    for(int w=12;w<=108;w+=4){
     int h=w*TH/TW;
     for(int y=0;y<=H-h;y+=3)for(int x=0;x<=W-w;x+=3){
      long score=Score(x,y,w,h,false,best);if(score<best){best=score;bx=x;by=y;bw=w;bh=h;}
     }
    }
    int cx=bx,cy=by,cw=bw;best=long.MaxValue;
    for(int w=Math.Max(8,cw-5);w<=Math.Min(116,cw+5);w++){
     int h=w*TH/TW;
     for(int y=Math.Max(0,cy-4);y<=Math.Min(H-h,cy+4);y++)
      for(int x=Math.Max(0,cx-4);x<=Math.Min(W-w,cx+4);x++){
       long score=Score(x,y,w,h,true,best);if(score<best){best=score;bx=x;by=y;bw=w;bh=h;}
      }
    }
    double error=best/(double)(16*20*3*255*255);
    output.WriteLine(id+"\t"+bx+"\t"+by+"\t"+bw+"\t"+bh+"\t"+error.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
   }
  }
 }
}
