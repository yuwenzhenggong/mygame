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

        // 角色整体放大倍数。精灵本身按 48x64 出图，乘以这个值后
        // 主角在房间里的身高才能对上门的比例（门 148px，角色应约 96px）。
        // 主角和敌人共用同一个倍数，否则主角会比怪高出一头。
        // 取 1.5 是为了在 2 倍窗口下每个像素仍是整数倍放大，不会糊边。
        public const float CharacterScale = 1.5f;
    }
}
