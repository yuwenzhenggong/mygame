namespace XianXia
{
    // 像素管线的唯一真源。改分辨率/PPU 只改这里，导入器、UI、摄像机都跟着变。
    public static class PixelArtSettings
    {
        // 每个世界单位对应多少像素。所有精灵必须一致，否则尺寸对不齐。
        public const int PixelsPerUnit = 32;

        // 逻辑基准分辨率（窗口放大 2 倍 = 1280x720）
        public const int ReferenceWidth = 640;
        public const int ReferenceHeight = 360;

        // 一块地形格的边长（像素）
        public const int TileSize = 32;

        // 美术资源根目录：只有这个目录下的贴图会被自动套用像素导入设置
        public const string ArtRoot = "Assets/Resources/Art/";
        public const string ArtResourceRoot = "Art/";
    }
}
