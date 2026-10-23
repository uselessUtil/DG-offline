// PNG fallback for the archive's stripped UnityEngine (no LoadImage binding).
using System;using System.IO;using System.Runtime.InteropServices;using UnityEngine;
public static partial class DivineGateOffline {
 static int uncompress(byte[] output,ref uint outputLength,byte[] source,uint sourceLength){throw new NotImplementedException("bound by build");}
 static int Be400(byte[] b,int i){return (b[i]<<24)|(b[i+1]<<16)|(b[i+2]<<8)|b[i+3];}
 static int Paeth400(int a,int b,int c){int p=a+b-c,x=Math.Abs(p-a),y=Math.Abs(p-b),z=Math.Abs(p-c);return x<=y&&x<=z?a:y<=z?b:c;}
 static Texture2D DecodePng400(byte[] data){
  if(data.Length<33 || data[0]!=137 || data[1]!=80)throw new InvalidOperationException("PNG signature");
  int width=Be400(data,16),height=Be400(data,20),channels=data[25]==6?4:data[25]==2?3:0;
  if(width<1||height<1||width>4096||height>4096||data[24]!=8||channels==0||data[28]!=0)throw new InvalidOperationException("PNG format");
  var packed=new MemoryStream();int pos=8;
  while(pos+12<=data.Length){int n=Be400(data,pos);if(n<0||pos+12L+n>data.Length)throw new InvalidOperationException("PNG chunk");if(data[pos+4]==73&&data[pos+5]==68&&data[pos+6]==65&&data[pos+7]==84)packed.Write(data,pos+8,n);pos+=12+n;}
  byte[] z=packed.ToArray();byte[] decoded=new byte[height*(width*channels+1)];uint length=(uint)decoded.Length;if(uncompress(decoded,ref length,z,(uint)z.Length)!=0 || length!=decoded.Length)throw new InvalidOperationException("PNG deflate");var stream=new MemoryStream(decoded);
  int stride=width*channels;byte[] previous=new byte[stride],row=new byte[stride];var texture=new Texture2D(width,height);
  for(int y=0;y<height;y++){
   int filter=stream.ReadByte();int read=0;while(read<stride){int got=stream.Read(row,read,stride-read);if(got<=0)throw new InvalidOperationException("PNG truncation");read+=got;}
   for(int x=0;x<stride;x++){int left=x>=channels?row[x-channels]:0,up=previous[x],corner=x>=channels?previous[x-channels]:0;int add=filter==0?0:filter==1?left:filter==2?up:filter==3?(left+up)/2:filter==4?Paeth400(left,up,corner):-1000;if(add<0)throw new InvalidOperationException("PNG filter");row[x]=(byte)(row[x]+add);}
   for(int x=0;x<width;x++){int i=x*channels;texture.SetPixel(x,height-1-y,new Color(row[i]/255f,row[i+1]/255f,row[i+2]/255f,channels==4?row[i+3]/255f:1f));}
   var swap=previous;previous=row;row=swap;
  }
  stream.Close();texture.Apply();return texture;
 }
}
