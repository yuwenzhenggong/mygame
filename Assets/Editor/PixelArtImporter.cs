using UnityEditor;
using UnityEngine;
using XianXia;

// 自动给 Assets/Resources/Art/ 下的所有贴图套用像素风导入设置。
// 你以后往这个目录拖图，不用再手动改 Filter Mode / Compression / PPU。
public class PixelArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(PixelArtSettings.ArtRoot,
                System.StringComparison.OrdinalIgnoreCase))
            return;

        var importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelArtSettings.PixelsPerUnit;

        // 这三项是像素游戏的关键：不设就是糊的
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.textureShape = TextureImporterShape.Texture2D;

        // Full Rect 是必须的：UI 里 Image.Type.Tiled 用 Tight 网格会警告且平铺错位。
        // 这个属性不在 TextureImporter 上，得通过 TextureImporterSettings 改。
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
