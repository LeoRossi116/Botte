using System;
using System.Collections.Generic;

/// <summary>
/// Serializable description of a lobby's configuration, chosen by the host on the
/// create-lobby page. This is the single shared contract used across the UI/session
/// layer (SceneUIManager), the lobby sync layer (RelayManager) and gameplay
/// (BattleManager). Values are also encoded into session properties so remote
/// clients can display them before joining.
/// </summary>
[Serializable]
public class LobbyConfig
{
    // Identity / discovery
    public string lobbyName = "";
    public bool isPublic = true;
    public string hostName = "";

    // Turn segment durations (seconds). Defaults match the previous hardcoded values
    // in BattleManager (Preparation 30, Combat 60, End 20).
    public int prepSeconds = 30;
    public int combatSeconds = 60;
    public int endSeconds = 20;

    // Match format: 1 = Best of 1 (single round), 3 = Best of 3 (first to 2 round wins).
    public int bestOf = 1;

    public LobbyConfig Clone()
    {
        return new LobbyConfig
        {
            lobbyName = lobbyName,
            isPublic = isPublic,
            hostName = hostName,
            prepSeconds = prepSeconds,
            combatSeconds = combatSeconds,
            endSeconds = endSeconds,
            bestOf = bestOf,
        };
    }

    /// <summary>Number of round wins required to win the match.</summary>
    public int RoundsToWin => bestOf >= 3 ? 2 : 1;

    // ---- Session property serialization helpers ----
    // Keys used when storing this config in Unity Multiplayer session properties.
    public const string KeyHostName = "hostName";
    public const string KeyPrep = "prepSeconds";
    public const string KeyCombat = "combatSeconds";
    public const string KeyEnd = "endSeconds";
    public const string KeyBestOf = "bestOf";
    public const string KeyStarted = "started";

    private static int ParseInt(Dictionary<string, string> props, string key, int fallback)
    {
        if (props != null && props.TryGetValue(key, out var v) && int.TryParse(v, out var parsed))
            return parsed;
        return fallback;
    }

    /// <summary>Builds a LobbyConfig from a flat key/value map (e.g. decoded session properties).</summary>
    public static LobbyConfig FromProperties(string lobbyName, bool isPublic, Dictionary<string, string> props)
    {
        var cfg = new LobbyConfig
        {
            lobbyName = lobbyName ?? "",
            isPublic = isPublic,
        };
        if (props != null)
        {
            if (props.TryGetValue(KeyHostName, out var hn)) cfg.hostName = hn;
            cfg.prepSeconds = ParseInt(props, KeyPrep, cfg.prepSeconds);
            cfg.combatSeconds = ParseInt(props, KeyCombat, cfg.combatSeconds);
            cfg.endSeconds = ParseInt(props, KeyEnd, cfg.endSeconds);
            cfg.bestOf = ParseInt(props, KeyBestOf, cfg.bestOf);
        }
        return cfg;
    }

    /// <summary>Flattens the gameplay-relevant fields to a key/value map for session properties.</summary>
    public Dictionary<string, string> ToProperties()
    {
        return new Dictionary<string, string>
        {
            { KeyHostName, hostName ?? "" },
            { KeyPrep, prepSeconds.ToString() },
            { KeyCombat, combatSeconds.ToString() },
            { KeyEnd, endSeconds.ToString() },
            { KeyBestOf, bestOf.ToString() },
        };
    }
}

/// <summary>
/// Process-wide holder for the configuration of the lobby the local player is currently
/// in. The host populates this from the create-lobby page; on clients it is populated by
/// RelayManager syncing the host's config after joining. BattleManager reads it to drive
/// turn durations and the best-of match format.
/// </summary>
public static class ActiveLobby
{
    public static LobbyConfig Config = new LobbyConfig();

    public static void Reset()
    {
        Config = new LobbyConfig();
    }
}
