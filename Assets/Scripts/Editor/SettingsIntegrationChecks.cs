using System;
using System.IO;
using System.Reflection;

/// <summary>Settings persistence checks use temporary files, never the player's settings.</summary>
public static class SettingsIntegrationChecks
{
    public static FileSettingStore CreateStore(string path)
    {
        var store = new FileSettingStore();
        var settings = new GameSettings();
        settings.ResetToDefault();
        typeof(SettingStore<GameSettings>).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(store, settings);
        typeof(SettingStore<GameSettings>).GetField("filePath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(store, path);
        return store;
    }

    public static void CheckStorage()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PluggerHeadSettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        try
        {
            var store = CreateStore(path);
            store.Data.Audio.MasterVolume = 0.3f;
            Require(store.Save(), "First save must succeed.");
            store.Data.Audio.MasterVolume = 0.7f;
            Require(store.Save(), "Replacing an existing save must succeed.");
            var reopened = ReadStore(path);
            Require(reopened.Data.Audio.MasterVolume == 0.7f, "Saved audio must survive reading a new store.");
            foreach (string field in new[] { "masterVolume", "ostVolume", "sfxVolume" })
            {
                foreach (string value in new[] { "2", "-1", "NaN", "Infinity", "-Infinity", "1e39" })
                {
                    string json = "{\"audio\":{\"masterVolume\":0.2,\"ostVolume\":0.2,\"sfxVolume\":0.2}}";
                    File.WriteAllText(path, json.Replace($"\"{field}\":0.2", $"\"{field}\":{value}"));
                    RequireDefaults(ReadStore(path));
                }
            }
            foreach (string json in new[] { "{}", "{\"audio\":{}}", "{\"audio\":null}", "{broken", "" })
            {
                File.WriteAllText(path, json);
                RequireDefaults(ReadStore(path));
            }
            File.WriteAllText(path, "{\"audio\":{\"masterVolume\":0,\"ostVolume\":1,\"sfxVolume\":0.25}}");
            reopened = ReadStore(path);
            Require(reopened.Data.Audio.MasterVolume == 0 && reopened.Data.Audio.OstVolume == 1 &&
                reopened.Data.Audio.SfxVolume == 0.25f, "Inclusive range boundaries must remain valid.");
            string previous = File.ReadAllText(path);
            // The Windows Editor cannot replace a file held without delete sharing.
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Require(!store.Save(), "Replacement failure must return false.");
            }
            Require(File.ReadAllText(path) == previous, "Failed replacement must preserve the old file byte for byte.");
            Require(Directory.GetFiles(directory, "*.tmp").Length == 0, "Save must clean up its temporary files.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static FileSettingStore ReadStore(string path)
    {
        var store = CreateStore(path);
        typeof(SettingStore<GameSettings>).GetMethod("LoadInto", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(store, new object[] { store.Data });
        return store;
    }

    private static void RequireDefaults(FileSettingStore store)
    {
        Require(store.Data.Audio != null && store.Data.Audio.MasterVolume == 1f &&
            store.Data.Audio.OstVolume == 0.5f && store.Data.Audio.SfxVolume == 0.5f,
            "Invalid audio must restore the entire default object, and omitted fields must keep defaults.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
