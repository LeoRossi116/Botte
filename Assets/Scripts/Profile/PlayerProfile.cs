using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Per-character gameplay record. Character is stored by HeroClass name so the save file
// stays readable and JsonUtility-serializable (it can't serialize Dictionaries).
[Serializable]
public class CharacterStat
{
    public string character;
    public int games;
    public int wins;
}

// The full, locally-saved player profile. Serialized to JSON via JsonUtility.
[Serializable]
public class PlayerProfileData
{
    public string profileName = "Player";
    public string profileId = "";
    public int totalWins = 0;
    public int totalLosses = 0;
    public List<CharacterStat> characterStats = new List<CharacterStat>();
}

// Loads, saves and mutates the local player profile. The profile lives in a dedicated
// "user-info" folder under Application.persistentDataPath. Account safety / cloud sync are
// out of scope for now (handled later) — this is a plain local save file.
public static class PlayerProfileManager
{
    private const string FolderName = "user-info";
    private const string FileName = "profile.json";

    private static PlayerProfileData _current;

    public static PlayerProfileData Current
    {
        get
        {
            if (_current == null) Load();
            return _current;
        }
    }

    public static string DirectoryPath => Path.Combine(Application.persistentDataPath, FolderName);
    public static string FilePath => Path.Combine(DirectoryPath, FileName);

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                _current = JsonUtility.FromJson<PlayerProfileData>(json) ?? new PlayerProfileData();
            }
            else
            {
                _current = new PlayerProfileData();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerProfile] Failed to load profile ({e.Message}); creating a fresh one.");
            _current = new PlayerProfileData();
        }

        if (_current.characterStats == null) _current.characterStats = new List<CharacterStat>();

        // The ID is generated the first time the profile is ever accessed and never changes.
        if (string.IsNullOrEmpty(_current.profileId))
        {
            _current.profileId = GenerateId();
            Save();
        }

        if (string.IsNullOrEmpty(_current.profileName))
        {
            _current.profileName = "Player";
        }
    }

    public static void Save()
    {
        if (_current == null) return;
        try
        {
            if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonUtility.ToJson(_current, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerProfile] Failed to save profile: {e.Message}");
        }
    }

    // Renames the profile (the display name shown in lobby and games). Empty names are ignored.
    public static void SetProfileName(string newName)
    {
        newName = (newName ?? "").Trim();
        if (string.IsNullOrEmpty(newName)) return;
        Current.profileName = newName;
        Save();
    }

    // Records one finished match for the local player: which character they used and the result.
    public static void RecordMatch(HeroClass heroClass, bool won)
    {
        var data = Current;
        if (won) data.totalWins++;
        else data.totalLosses++;

        string key = heroClass.ToString();
        CharacterStat stat = data.characterStats.Find(s => s.character == key);
        if (stat == null)
        {
            stat = new CharacterStat { character = key };
            data.characterStats.Add(stat);
        }
        stat.games++;
        if (won) stat.wins++;

        Save();
    }

    private static string GenerateId()
    {
        // Compact, human-readable, effectively-unique ID (immutable once generated).
        return Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
    }

    // ---------- Convenience read-only stat helpers (used by the Account page) ----------

    public static float WinLossRatio =>
        Current.totalLosses == 0 ? Current.totalWins : (float)Current.totalWins / Current.totalLosses;

    public static float WinPercentage
    {
        get
        {
            int total = Current.totalWins + Current.totalLosses;
            return total == 0 ? 0f : 100f * Current.totalWins / total;
        }
    }

    public static float WinPercentageFor(HeroClass hc)
    {
        CharacterStat stat = Current.characterStats.Find(s => s.character == hc.ToString());
        if (stat == null || stat.games == 0) return 0f;
        return 100f * stat.wins / stat.games;
    }

    public static int GamesFor(HeroClass hc)
    {
        CharacterStat stat = Current.characterStats.Find(s => s.character == hc.ToString());
        return stat == null ? 0 : stat.games;
    }

    public static string MostUsedCharacter(out int games)
    {
        games = 0;
        string best = "-";
        foreach (CharacterStat s in Current.characterStats)
        {
            if (s.games > games)
            {
                games = s.games;
                best = s.character;
            }
        }
        return best;
    }
}
