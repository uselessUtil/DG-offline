using System;using System.Collections.Generic;using UnityEngine;
public static class DoorCurveProbe {
 public static readonly List<float[]> Values=new List<float[]>();
 public static readonly List<string> Paths=new List<string>();
 public static AnimationCurve Curve(float t0,float v0,float t1,float v1){Values.Add(new[]{t0,v0,t1,v1});return null;}
 public static void Binding(string path,Type type,string property,AnimationCurve curve){
  if(type!=typeof(Transform) || property!="localScale.x")throw new Exception("Wrong door animation binding");
  Paths.Add(path);
 }
}
