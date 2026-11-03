using UnityEngine;

namespace XianXia.Data
{
    public static class DataLoader
    {
        // path 相对 Assets/Resources，不带 .json 后缀。
        // 例：Load<RealmTable>("Data/realms")
        public static T Load<T>(string path) where T : class
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError("[DataLoader] 找不到配置表: Assets/Resources/" + path + ".json");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(asset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[DataLoader] 解析 " + path + ".json 失败: " + e.Message);
                return null;
            }
        }
    }
}
