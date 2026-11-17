using System;
using System.IO;
using UnityEngine;

namespace Mourneveil
{
    public static class SaveSystem
    {
        private const string FileName = "mourneveil_save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static GameState Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return new GameState();

                string json = File.ReadAllText(SavePath);
                GameState loaded = JsonUtility.FromJson<GameState>(json);
                return loaded ?? new GameState();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Mourneveil] Save could not be loaded: {exception.Message}");
                return new GameState();
            }
        }

        public static void Save(GameState state)
        {
            if (state == null)
                return;

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string json = JsonUtility.ToJson(state, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Mourneveil] Save failed: {exception.Message}");
            }
        }

        
        public static void DeleteSaveForTesting()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
    }
}
