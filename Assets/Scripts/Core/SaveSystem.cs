using System.IO;
using System.Text;
using UnityEngine;

namespace XianXia
{
    public static class SaveSystem
    {
        const string FileName = "save_01.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Save(GameState state)
        {
            if (state == null) return false;
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(SavePath, JsonUtility.ToJson(state, true), new UTF8Encoding(false));
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] 存档失败: " + e.Message);
                return false;
            }
        }

        public static GameState Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                var json = File.ReadAllText(SavePath, Encoding.UTF8);
                return JsonUtility.FromJson<GameState>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] 读档失败: " + e.Message);
                return null;
            }
        }

        public static bool HasSave() => File.Exists(SavePath);

        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] 删除存档失败: " + e.Message);
            }
        }
    }
}
