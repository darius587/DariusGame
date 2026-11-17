using System;
using System.IO;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    public static class SaveSystem
    {
#if UNITY_EDITOR
        public static string TestSavePath;
        public static string SavePath => TestSavePath ?? Path.Combine(Application.persistentDataPath, "save.json");
#else
        public static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
#endif
        // A failed load is read-only until the user explicitly resets. This preserves
        // damaged files for recovery and prevents old builds replacing newer saves.
        public static bool CanWrite { get; private set; } = true;

        public static SaveData Load()
        {
            CanWrite = true;
            try
            {
                string source = SavePath;
                if (!File.Exists(source))
                {
                    if (!File.Exists(SavePath + ".bak")) return new SaveData();
                    source = SavePath + ".bak";
                    Debug.LogWarning("Recovering progress from an interrupted save backup.");
                }
                string json = File.ReadAllText(source);
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") ||
                    !json.Contains("\"saveVersion\"") || !json.Contains("\"unlockedLevels\""))
                    throw new InvalidDataException("Empty or incomplete save JSON.");
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.saveVersion != SaveData.CurrentVersion)
                    throw new InvalidDataException("Unsupported save version or null save.");
                data.Normalize();
                return data;
            }
            catch (Exception ex)
            {
                CanWrite = false;
                Debug.LogWarning("Could not load save; using defaults without overwriting the file. " + ex.Message);
                return new SaveData();
            }
        }

        public static bool Save(SaveData data)
        {
            if (!CanWrite || data == null) return false;
            string temporary = SavePath + ".tmp";
            try
            {
                data.Normalize();
                Directory.CreateDirectory(Application.persistentDataPath);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    string backup = SavePath + ".bak";
                    try { File.Replace(temporary, SavePath, backup); }
                    catch (Exception ex) when (ex is IOException || ex is PlatformNotSupportedException)
                    {
                        // Some Unity/Mono filesystems cannot replace files. Retain a
                        // recoverable copy across the two renames on those platforms.
                        if (!File.Exists(temporary) || !File.Exists(SavePath)) throw;
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(SavePath, backup);
                        try { File.Move(temporary, SavePath); }
                        catch { if (!File.Exists(SavePath)) File.Move(backup, SavePath); throw; }
                    }
                }
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not save progress; previous save retained. " + ex.Message);
                return false;
            }
        }

        public static bool Delete()
        {
            try
            {
                if (File.Exists(SavePath + ".tmp")) File.Delete(SavePath + ".tmp");
                if (File.Exists(SavePath + ".bak")) File.Delete(SavePath + ".bak");
                if (File.Exists(SavePath)) File.Delete(SavePath);
                CanWrite = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not reset progress. " + ex.Message);
                return false;
            }
        }
    }
}
