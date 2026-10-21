using System;
using System.Collections.Generic;
using System.IO;
using LitJson;

// Exercise the original GetSprite -> packaged EnsureSprite route for every
// original area's res_map_icon. Native component ownership is a test stand-in.
public static class VerifyAreaIcons
{
    public static void Main(string[] args)
    {
        var expected = new Dictionary<string, int[]>();
        foreach (string line in File.ReadAllLines(args[0]))
        {
            string[] row = line.Split('\t');
            expected.Add(row[0], Array.ConvertAll(row[1].Split(','), int.Parse));
        }
        var atlas = new UIAtlas();
        EngineSupport.BindComponentOwner(atlas, "DG offline atlas AtlasAreaIcon_000_020");
        var areas = JsonMapper.ToObject<MasterDataArea[]>(File.ReadAllText(Path.Combine(
            Environment.GetEnvironmentVariable("DG_TEST_TABLES"), "MasterDataArea.json")));
        int count = 0;
        foreach (var area in areas)
        {
            if (area.fix_id == 0) continue;
            int[] rect;
            if (!expected.TryGetValue(area.res_map_icon, out rect)) throw new Exception("Missing area " + area.fix_id);
            var sprite = atlas.GetSprite(area.res_map_icon);
            if (sprite == null || sprite.outer.x != rect[0] || sprite.outer.y != rect[1] ||
                sprite.outer.width != 207 || sprite.outer.height != 131)
                throw new Exception("Wrong actual sprite routing for " + area.fix_id + ": " + area.res_map_icon);
            if (!object.ReferenceEquals(sprite, atlas.GetSprite(area.res_map_icon)))
                throw new Exception("Repeated lookup replaced sprite " + area.res_map_icon);
            count++;
        }
        if (atlas.spriteList.Count != expected.Count) throw new Exception("Missing/duplicate sprite registration");
        Console.WriteLine("PASS original area selection sprite lookup: " + count + " area rows resolve " + expected.Count +
            " cropped sprites in the actual APK adapter, all 207x131 with stable cached coordinates; no master-table changes required. Android rendering pending.");
    }
}
