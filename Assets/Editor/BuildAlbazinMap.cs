using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools -> Build Albazin Map : расставляет острог по координатам из макета (метры, 1 м = 1 юнит).
// Координаты: X вправо, Y вниз в макете -> в Unity Y инвертируется. Точка = низ-центр спрайта.
public static class BuildAlbazinMap
{
    const string Folder = "Assets/Sprites/AlbazinLandmarks";

    // ---- НАСТРОЙКИ СТЕН (метры вдоль стены) — подбирай на глаз ----
    const float StepNorth = 2.4f;   // (4) — стена смотрит на север
    const float StepSouth = 2.9f;   // (2) — стена смотрит на юг
    const float StepWest  = 4.6f;   // (3) отзеркалена
    const float StepEast  = 4.6f;   // (3)
    const string NorthSprite = "House_W4_A10_1F_6x4_1b (4)";
    const string SouthSprite = "House_W4_A10_1F_6x4_1b (2)";
    const string SideSprite  = "House_W4_A10_1F_6x4_1b (3)";

    struct Obj
    {
        public int id; public string sprite; public float x, bottomY, scale;
        public Obj(int id, string sprite, float x, float bottomY, float scale)
        { this.id = id; this.sprite = sprite; this.x = x; this.bottomY = bottomY; this.scale = scale; }
    }

    static readonly Obj[] Objects =
    {
        new Obj(1, "Town", -37.7f, -35.8f, 0.8f),
        new Obj(3, "Town", 33.6f, -35.6f, 0.8f),
        new Obj(25, "Town", -34.8f, 43.0f, 0.8f),
        new Obj(29, "Town", 39.0f, 42.0f, 0.8f),
        new Obj(9, "Town", -38.4f, -11.1f, 0.6f),
        new Obj(16, "Town", -37.4f, 17.6f, 0.6f),
        new Obj(2, "LongHouse", -13.0f, -29.7f, 0.9f),
        new Obj(21, "LongHouse", -25.6f, 25.6f, 0.8f),
        new Obj(24, "LongHouse", -17.9f, 35.8f, 0.93f),
        new Obj(12, "Town", 17.2f, -7.8f, 1.0f),
        new Obj(5, "House_W4_A10_1F_6x4_1b", 19.9f, -28.9f, 0.8f),
        new Obj(10, "House_W4_A10_1F_6x4_1b", 19.8f, -20.6f, 0.7f),
        new Obj(8, "House_W4_A10_1F_6x4_1b", 11.3f, -23.9f, 0.6f),
        new Obj(6, "House_W4_A10_1F_6x4_1b_1", -22.4f, -17.6f, 0.65f),
        new Obj(7, "House_W4_A10_1F_6x4_1b_1", -28.7f, -17.6f, 0.55f),
        new Obj(11, "House_W4_A10_1F_6x4_1b", -28.6f, -6.5f, 0.7f),
        new Obj(14, "House_W4_A10_1F_6x4_1b", -26.1f, 11.5f, 0.8f),
        new Obj(22, "House_W4_A10_1F_6x4_1b_1", 30.5f, 18.5f, 0.85f),
        new Obj(26, "House_W4_A10_1F_6x4_1b_1", 30.9f, 32.2f, 0.85f),
        new Obj(23, "House_W4_A10_1F_6x4_1b_1", 17.3f, 20.2f, 0.9f),
        new Obj(4, "House_W4_A10_1F_6x4_1b_1", -35.2f, -19.2f, 0.45f),
        new Obj(15, "House_W4_A10_1F_6x4_1b_1", 29.8f, -1.5f, 0.5f),
        new Obj(20, "House_W4_A10_1F_6x4_1b_1", 10.1f, 12.5f, 0.5f),
        new Obj(27, "House_W4_A10_1F_6x4_1b_1", -3.7f, 37.2f, 0.5f),
        new Obj(30, "House_W4_A10_1F_6x4_1b_3", 24.2f, 40.6f, 0.35f),
        new Obj(19, "House_W4_A10_1F_6x4_1b_3", 4.5f, 17.6f, 1.0f),
        new Obj(13, "Gate_Front", -19.5f, 4.0f, 1.0f),
        new Obj(17, "Gate_Front", 36.3f, 6.5f, 1.0f),
        new Obj(28, "Gate_Front", 5.9f, 45.6f, 1.3f),
    };

    // углы стены: NW, NE, SE, SW
    static readonly Vector2[] Corner =
    {
        new Vector2(-37.7f, -39.0f), new Vector2(33.6f, -39.0f),
        new Vector2(39.0f, 38.4f),  new Vector2(-34.8f, 39.5f)
    };
    // проёмы: южные ворота по X, восточные по Y
    const float SouthGateX = 5.9f, SouthGateHalf = 3.5f;
    const float EastGateY = 2.5f,  EastGateHalf = 3.0f;

    [MenuItem("Tools/Build Albazin Map")]
    static void Build()
    {
        var old = GameObject.Find("AlbazinMap");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("AlbazinMap").transform;

        foreach (var o in Objects)
            Place(root, "B" + o.id + "_" + o.sprite, o.sprite, o.x, o.bottomY, o.scale, false);

        Wall(root, Corner[0], Corner[1], NorthSprite, StepNorth, false, 0, 0, true);
        Wall(root, Corner[3], Corner[2], SouthSprite, StepSouth, false, SouthGateX - SouthGateHalf, SouthGateX + SouthGateHalf, true);
        Wall(root, Corner[0], Corner[3], SideSprite, StepWest, true, 0, 0, false);
        Wall(root, Corner[1], Corner[2], SideSprite, StepEast, false, EastGateY - EastGateHalf, EastGateY + EastGateHalf, false);

        Debug.Log("AlbazinMap построена. Подкрути Step* вверху скрипта и запусти снова.");
    }

    static void Wall(Transform root, Vector2 p, Vector2 q, string sprite, float step, bool flip,
                     float gapA, float gapB, bool horizontal)
    {
        float len = Vector2.Distance(p, q);
        int n = Mathf.Max(1, Mathf.FloorToInt(len / step));
        for (int k = 0; k <= n; k++)
        {
            float t = (float)k / n;
            Vector2 pt = Vector2.Lerp(p, q, t);
            float coord = horizontal ? pt.x : pt.y;
            if (gapB > gapA && coord >= gapA && coord <= gapB) continue;
            Place(root, "Wall_" + k, sprite, pt.x, pt.y, 1f, flip);
        }
    }

    static void Place(Transform root, string name, string spriteName, float x, float yDown, float scale, bool flipX)
    {
        string path = Folder + "/" + spriteName + ".png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning("Нет спрайта: " + path); return; }
        var ts = new TextureImporterSettings();
        imp.ReadTextureSettings(ts);
        if (ts.spriteAlignment != (int)SpriteAlignment.BottomCenter || imp.spritePixelsPerUnit != 32)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            ts.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            imp.SetTextureSettings(ts);
            imp.SaveAndReimport();
        }
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(x, -yDown, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.flipX = flipX;
    }
}
