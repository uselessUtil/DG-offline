using Godot;
using System;
using System.Text.Json.Nodes;

namespace DivineGateGodot;

public static class MapArt
{
    static JsonNode? data;
    public static void Draw(Node2D canvas,MasterDataArea area)
    {
        data??=JsonNode.Parse(FileAccess.GetFileAsString("res://Reference/battle/maps.json"));
        string? tex=data!["textures"]?[area.res_map]?.GetValue<string>();
        if(tex==null)return;
        var texture=LegacyView.Texture(tex);
        // Original maps lie on the XZ plane. Each unit is one floor square.
        foreach(string root in new[]{area.mesh_map,area.mesh_door}){
            if(data["maps"]?[root] is not JsonArray parts)continue;
            foreach(var part in parts){
                var vertices=part!["vertices"]!.AsArray();var uv=part["uv"]!.AsArray();var ids=part["indices"]!.AsArray();
                for(int i=0;i+2<ids.Count;i+=3){
                    Vector2[] points=new Vector2[3],coords=new Vector2[3];
                    for(int n=0;n<3;n++){int idx=ids[i+n]!.GetValue<int>();var v=vertices[idx]!;
                        points[n]=new(320+LegacyView.F(v[0])*120,650-LegacyView.F(v[2])*120-LegacyView.F(v[1])*20);
                        coords[n]=new(LegacyView.F(uv[idx]![0]),1-LegacyView.F(uv[idx]![1]));}
                    // The top-down projection naturally collapses edge faces.
                    if(Math.Abs((points[1]-points[0]).Cross(points[2]-points[0]))>.01f)canvas.DrawPolygon(points,new[]{Colors.White},coords,texture);
                }
            }
        }
    }
}
