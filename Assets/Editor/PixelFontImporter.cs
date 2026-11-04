using UnityEditor;
using UnityEngine;

// 像素字体导入设置。
// 关键：RenderingMode 必须关掉抗锯齿（Hinted Raster），否则 12px 的中文
// 放大到 24px 时会糊成一团——这是像素游戏最容易忽略的一步。
public class PixelFontImporter : AssetPostprocessor
{
    const string FontRoot = "Assets/Resources/Fonts/";

    void OnPreprocessAsset()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(FontRoot, System.StringComparison.OrdinalIgnoreCase))
            return;

        var importer = assetImporter as TrueTypeFontImporter;
        if (importer != null) Apply(importer);
    }

    public static void Apply(TrueTypeFontImporter importer)
    {
        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.fontRenderingMode = FontRenderingMode.HintedRaster;
        importer.characterSpacing = 0;
        importer.characterPadding = 1;
        importer.includeFontData = true;
    }
}
