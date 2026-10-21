using System;
using System.IO;

// Original UnityUtil.FindSpriteInAtlas -> patched UIAtlas.GetSprite -> actual
// adapter EnsureSprite, with native component ownership supplied by TestEngine.
public static class VerifyEnemyAtlasSearch
{
    public static void Main(string[] args)
    {
        int count = 0;
        foreach (string suffix in new string[] { "", "Alpha" })
        {
            UIAtlas[] atlases = new UIAtlas[5];
            for (int i = 0; i < atlases.Length; i++)
            {
                atlases[i] = new UIAtlas();
                EngineSupport.BindComponentOwner(atlases[i], "DG offline atlas AtlasCharaIcon_" +
                    (i*200).ToString("000") + "_" + (i*200+199).ToString("000") + suffix);
            }
            foreach (string line in File.ReadAllLines(args[0]))
            {
                string[] row = line.Split('\t');
                string sprite = "chara_icon_" + row[1];
                int expected = int.Parse(row[0]) / 200;
                int found = -1;
                for (int i = 0; i < atlases.Length; i++)
                    if (UnityUtil.FindSpriteInAtlas(atlases[i], sprite)) { found = i; break; }
                if (found != expected) throw new Exception(sprite + " selected wrong atlas " + found + " expected " + expected);
                for (int i = 0; i < atlases.Length; i++)
                    if (i != expected && atlases[i].GetSprite(sprite) != null)
                        throw new Exception("Foreign atlas synthesized an enemy placeholder: " + sprite);
                count++;
            }
            foreach (UIAtlas atlas in atlases)
            {
                if (UnityUtil.FindSpriteInAtlas(atlas, "chara_icon_99999"))
                    throw new Exception("Unknown enemy must not report a successful atlas match");
                if (!UnityUtil.FindSpriteInAtlas(atlas, "chara_icon_empty"))
                    throw new Exception("Empty friend/party slot lost");
            }
        }
        Console.WriteLine("PASS original enemy atlas search: " + count +
            " character/Alpha lookups choose the real owning atlas; foreign and unknown sprites return null; Empty remains available");
    }
}
