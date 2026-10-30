using _3DStealthGame.Scripts.Constants;

using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDK3.Data;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Leaderboard : UdonSharpBehaviour
{
    public TextMeshProUGUI leaderboardText;

    private const int MaxRecordsOnLeaderboard = 10;

    private const int MaxPlayers = 80;

    private const int MaxNameLength = 16;

    private const string Ellipsis = "...";

    private const int DummyCount = 10;

    private const float SecondsPerMinute = 60f;

    // A new record makes every player in the instance save the top list. Each player
    // waits a random time first, so the saves happen at different times. Changes during the wait go into one save.
    private const float MinSaveDelay = 1f;

    private const float MaxSaveDelay = 5f;

    // Records that are always on the top list, for new players to beat. Sorted fastest first.
    private string[] _dummyNames =
    {
        "lancewusky", "Penguin_Fish27", "Endasil", "DiegoNC", "Psychowench",
        "Casper the Slow", "Mr. Boo", "Ghostly Gus", "Sir Spooksalot", "Lady Cobweb"
    };

    private float[] _dummyTimes =
    {
        1f * SecondsPerMinute + 15.19f,
        1f * SecondsPerMinute + 15.62f,
        1f * SecondsPerMinute + 35.05f,
        1f * SecondsPerMinute + 55.73f,
        2f * SecondsPerMinute + 55.61f,
        54f * SecondsPerMinute + 12.37f,
        59f * SecondsPerMinute + 48.05f,
        64f * SecondsPerMinute + 3.91f,
        69f * SecondsPerMinute + 27.64f,
        74f * SecondsPerMinute + 55.18f
    };

    private VRCPlayerApi[] _vrcPlayers = new VRCPlayerApi[MaxPlayers];

    // The 10 fastest times, one per name, fastest first.
    private string[] _topNames = new string[MaxRecordsOnLeaderboard];
    private float[] _topTimes = new float[MaxRecordsOnLeaderboard];
    private int _topCount;
    private bool _localRestored;
    private bool _isSaveScheduled;

    void Start()
    {
        if (leaderboardText == null)
        {
            Debug.LogError($"Leaderboard ({gameObject.name}): leaderboardText needs to be assigned in the inspector.", gameObject);
        }
    }

    // Adds the time to the top list if it is fast enough. A name already on the list
    // keeps only its fastest time. Returns true when the list changed.
    private bool AddRecord(string displayName, float time)
    {
        if (time <= 0 || float.IsNaN(time))
        {
            Debug.LogError($"Leaderboard: tried to add invalid time {time} for {displayName}");
            return false;
        }

        int existingIndex = IndexInTopList(displayName);
        if (existingIndex >= 0)
        {
            if (time >= _topTimes[existingIndex]) return false;
            RemoveFromTopList(existingIndex);
        }

        int position = 0;
        while (position < _topCount && _topTimes[position] <= time)
        {
            position++;
        }

        if (position >= MaxRecordsOnLeaderboard) return false;

        // Shift the slower records down one index. On a full list, this removes the slowest record.
        int lastIndex = Mathf.Min(_topCount, MaxRecordsOnLeaderboard - 1);
        for (int i = lastIndex; i > position; i--)
        {
            _topNames[i] = _topNames[i - 1];
            _topTimes[i] = _topTimes[i - 1];
        }

        _topNames[position] = displayName;
        _topTimes[position] = time;
        if (_topCount < MaxRecordsOnLeaderboard)
        {
            _topCount++;
        }
        return true;
    }

    private void RemoveFromTopList(int index)
    {
        for (int i = index; i < _topCount - 1; i++)
        {
            _topNames[i] = _topNames[i + 1];
            _topTimes[i] = _topTimes[i + 1];
        }
        _topCount--;
    }

    // Reads the player's best time and saved top list. Returns true when the top list changed.
    private bool AddPlayerRecords(VRCPlayerApi player)
    {
        bool changed = false;

        if (PlayerData.TryGetFloat(player, PlayerDataKeys.SavedPersonalBestEscapeTime, out float playerBestTime))
        {
            if (AddRecord(player.displayName, playerBestTime)) changed = true;
        }

        if (AddPlayersSavedTopList(player)) changed = true;

        return changed;
    }

    // Reads the players top list and adds each entry. Returns true when the top list changed.
    private bool AddPlayersSavedTopList(VRCPlayerApi player)
    {
        if (!PlayerData.HasKey(player, PlayerDataKeys.SavedHighScoreList))
        {
            return false;
        }

        var topRecordJson = PlayerData.GetString(player, PlayerDataKeys.SavedHighScoreList);
        if (!VRCJson.TryDeserializeFromJson(topRecordJson, out DataToken deserializedTopRecords))
        {
            Debug.LogError($"Failed to deserialize data as json from {player.displayName}. Error: {deserializedTopRecords.Error}. json data: {topRecordJson}");
            return false;
        }

        if (deserializedTopRecords.TokenType != TokenType.DataList)
        {
            Debug.LogError($"Failed to decode {player.displayName}'s HighScore. result.TokenType is {deserializedTopRecords.TokenType}, not {TokenType.DataList}");
            return false;
        }

        bool changed = false;
        var nrEntries = Mathf.Min(deserializedTopRecords.DataList.Count, MaxRecordsOnLeaderboard);
        for (int i = 0; i < nrEntries; i++)
        {
            if (!deserializedTopRecords.DataList.TryGetValue(i, TokenType.DataDictionary, out DataToken item))
            {
                Debug.LogError($"Leaderboard: entry {i} is not a DataDictionary.");
                continue;
            }

            DataDictionary record = item.DataDictionary;
            if (!record.TryGetValue("name", TokenType.String, out DataToken displayName))
            {
                Debug.LogError($"Failed to fetch name from Leaderboard: entry {i}, from {player.displayName} ");
                continue;
            }

            // We use double here
            // because when a float is serialized to json, it stores the numbers as
            // text without information if this is a float or a double.
            // because of this VRCJson always decode it as double and not float.
            if (!record.TryGetValue("time", TokenType.Double, out DataToken time))
            {
                Debug.LogError($"Failed to fetch time from Leaderboard: entry {i}, from {player.displayName} ");
                continue;
            }

            if (AddRecord(displayName.String, (float)time.Double)) changed = true;
        }

        return changed;
    }

    private void AddDummyRecords()
    {
        for (int i = 0; i < DummyCount; i++)
        {
            AddRecord(_dummyNames[i], _dummyTimes[i]);
        }
    }

    // Runs once, when the local player's data is loaded. The local player's saved list
    // has the records of players who left the instance.
    private void AddAllPlayers()
    {
        AddDummyRecords();

        int playerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_vrcPlayers);

        for (int i = 0; i < playerCount && i < _vrcPlayers.Length; i++)
        {
            VRCPlayerApi player = _vrcPlayers[i];
            if (!Utilities.IsValid(player)) continue;

            AddPlayerRecords(player);
        }

        // The list started out empty, so it always changed.
        OnTopListChanged();
    }

    private void OnTopListChanged()
    {
        Redraw();

        if (_isSaveScheduled) return;

        _isSaveScheduled = true;
        SendCustomEventDelayedSeconds(nameof(_SaveHighScoreList), Random.Range(MinSaveDelay, MaxSaveDelay));
    }

    // Returns the place on the top list for the time.
    // 1 is best, 0 not on the list at all.
    public int GetPositionFor(string displayName, float time)
    {
        int position = 1;
        for (int i = 0; i < _topCount; i++)
        {
            if (_topNames[i] == displayName) continue;
            if (_topTimes[i] < time)
            {
                position++;
            }
        }

        if (position > MaxRecordsOnLeaderboard) return 0;
        return position;
    }

    // Returns -1 when the name is not on the top list.
    private int IndexInTopList(string displayName)
    {
        for (int i = 0; i < _topCount; i++)
        {
            if (_topNames[i] == displayName) return i;
        }
        return -1;
    }

    // Public so SendCustomEventDelayedSeconds can call it. The underscore stops other
    // clients from calling it with a network event.
    public void _SaveHighScoreList()
    {
        _isSaveScheduled = false;

        var dataList = new DataList();

        for (int i = 0; i < _topCount; i++)
        {
            DataDictionary record = new DataDictionary();
            record["name"] = _topNames[i];
            record["time"] = _topTimes[i];
            dataList.Add(record);
        }

        if (!VRCJson.TrySerializeToJson(dataList, JsonExportType.Minify, out DataToken json))
        {
            Debug.LogError($"Leaderboard: could not serialize the top list: {json}");
            return;
        }

        // VRChat sends each PlayerData write to all clients. The scene has two leaderboards
        // that save the same list, so skip the write when the saved list is the same.
        if (PlayerData.TryGetString(Networking.LocalPlayer, PlayerDataKeys.SavedHighScoreList, out string savedJson)
            && savedJson == json.String)
        {
            return;
        }

        PlayerData.SetString(PlayerDataKeys.SavedHighScoreList, json.String);
    }

    private void Redraw()
    {
        string text = "";
        for (int i = 0; i < _topCount; i++)
        {
            // Pos is used to alight all times at 72% of the width to get them to display under each other.
            text += (i + 1) + ". " + ShortName(_topNames[i]) + "<pos=72%>" + TimeFormatter.FormatCapped(_topTimes[i]) + "\n";
        }
        leaderboardText.text = text;
    }

    private string ShortName(string name)
    {
        if (name.Length <= MaxNameLength) return name;
        return name.Substring(0, MaxNameLength - Ellipsis.Length) + Ellipsis;
    }

    // Called when vrchat has loaded the persistent data for a player
    // that joined the instance.
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            _localRestored = true;
            AddAllPlayers();
            return;
        }

        // AddAllPlayers reads the players who were restored before the local player.
        if (!_localRestored) return;

        if (AddPlayerRecords(player))
        {
            OnTopListChanged();
        }
    }

    // When the persistent player data changes, for example a player got a new highscore, this class is called.
    public override void OnPlayerDataUpdated(VRCPlayerApi player, PlayerData.Info[] infos)
    {
        if (!_localRestored) return;

        if (AddPlayerRecords(player))
        {
            OnTopListChanged();
        }
    }
}
