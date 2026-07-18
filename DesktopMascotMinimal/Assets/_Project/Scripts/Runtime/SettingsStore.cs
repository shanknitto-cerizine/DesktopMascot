using System.IO;
using UnityEngine;

namespace DesktopMascot
{
    public static class SettingsStore
    {
        private static string PathName => Path.Combine(Application.persistentDataPath, "settings.json");

        public static MascotSettings Load()
        {
            try
            {
                if (!File.Exists(PathName)) return new MascotSettings();
                return JsonUtility.FromJson<MascotSettings>(File.ReadAllText(PathName)) ?? new MascotSettings();
            }
            catch
            {
                return new MascotSettings();
            }
        }

        public static void Save(MascotSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(PathName, JsonUtility.ToJson(settings, true));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
