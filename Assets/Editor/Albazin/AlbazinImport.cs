using UnityEditor;
using UnityEngine;
public sealed class AlbazinImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        bool tiles = assetPath.StartsWith("Assets/AlbazinArt/Tiles/");
        bool parts = assetPath.StartsWith("Assets/Sprites/AlbazinLandmarks/");
        bool sheets = assetPath.StartsWith("Assets/Resources/Albazin/");
        if (!(tiles || parts || sheets)) return;
        var i = (TextureImporter)assetImporter;
        i.mipmapEnabled = false;
        i.filterMode = FilterMode.Point;
        i.textureCompression = TextureImporterCompression.Uncompressed;
        i.maxTextureSize=2048;
        if (sheets) { i.textureType=TextureImporterType.Default; return; }
        i.textureType=TextureImporterType.Sprite; i.spriteImportMode=SpriteImportMode.Single;
        var s=new TextureImporterSettings();
        i.ReadTextureSettings(s);
        s.spriteAlignment=parts ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
        i.SetTextureSettings(s);
        i.spritePixelsPerUnit=32;
    }
}
