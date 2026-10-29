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

    private const float SlowestDummyTime = 99f * 60f;

    private const float DummyTimeStep = 5f * 60f;

    // Made-up players with slow times, for new players to beat. Sorted fastest first.
    private string[] _dummyNames =
    {
        "Casper the Slow", "Mr. Boo", "Ghostly Gus", "Sir Spooksalot", "Lady Cobweb",
        "Butler's Cousin", "Headless Harold", "Gargoyle Fan", "Spectral Steve", "Not A Ghost"
    };

    // Seconds added to each dummy time. All under 60, so the order stays the same.
    private float[] _dummyExtraSeconds = { 12.37f, 48.05f, 3.91f, 27.64f, 55.18f, 9.42f, 36.83f, 21.06f, 44.59f, 31.72f };

    // Each player can give 1 best time plus MaxRecordsOnLeaderboard entries from their saved list
    private const int MaxCandidates = MaxPlayers * (MaxRecordsOnLeaderboard + 1) + DummyCount;

    private VRCPlayerApi[] _vrcPlayers = new VRCPlayerApi[MaxPlayers];

    // Holds the best times and saved top list entries of the players in the instance.
    // Rebuild() sorts them and copies the 10 fastest, one per name, into the top list.
    private string[] _allRecordNames = new string[MaxCandidates];

    private float[] _allRecordTimes = new float[MaxCandidates];
    private int _candidateCount;
    private string[] _topNames = new string[MaxRecordsOnLeaderboard];
    private float[] _topTimes = new float[MaxRecordsOnLeaderboard];
    private int _topCount;
    private bool _localRestored;

    void Start()
    {
        if (leaderboardText == null)
        {
            Debug.LogError($"Leaderboard ({gameObject.name}): leaderboardText needs to be assigned in the inspector.", gameObject);
        }
    }

    private void AddCandidate(string displayName, float time)
    {
        if (time <= 0 || float.IsNaN(time))
        {
            Debug.LogError($"Leaderboard: tried to add invalid time {time} for {displayName}");
            return;
        }
        _allRecordNames[_candidateCount] = displayName;
        _allRecordTimes[_candidateCount] = time;
        _candidateCount++;
    }

    // Reads the players top list and add each entry as a candidate.
    private void AddPlayersSavedTopList(VRCPlayerApi player)
    {
        if (!PlayerData.HasKey(player, PlayerDataKeys.SavedHighScoreList))
        {
            return;
        }

        var topRecordJson = PlayerData.GetString(player, PlayerDataKeys.SavedHighScoreList);
        if (!VRCJson.TryDeserializeFromJson(topRecordJson, out DataToken deserializedTopRecords))
        {
            Debug.LogError($"Failed to deserialize data as json from {player.displayName}. Error: {deserializedTopRecords.Error}. json data: {topRecordJson}");
            return;
        }

        if (deserializedTopRecords.TokenType != TokenType.DataList)
        {
            Debug.LogError($"Failed to decode {player.displayName}'s HighScore. result.TokenType is {deserializedTopRecords.TokenType}, not {TokenType.DataList}");
            return;
        }

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
            AddCandidate(displayName.String, (float)time.Double);
        }
    }

    private void AddDummyRecords()
    {
        for (int i = 0; i < DummyCount; i++)
        {
            float stepsFromSlowest = DummyCount - 1 - i;
            AddCandidate(_dummyNames[i], SlowestDummyTime - stepsFromSlowest * DummyTimeStep + _dummyExtraSeconds[i]);
        }
    }

    private void Rebuild()
    {
        _candidateCount = 0;
        AddDummyRecords();

        int playerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_vrcPlayers);

        // Loop trough each player in the scene
        for (int i = 0; i < playerCount; i++)
        {
            VRCPlayerApi player = _vrcPlayers[i];
            if (!Utilities.IsValid(player)) continue;

            // Check if player has a personal best time, add their time to the total candidate pool 
            if (PlayerData.TryGetFloat(player, PlayerDataKeys.SavedPersonalBestEscapeTime, out float playerBestTime))
            {
                AddCandidate(player.displayName, playerBestTime);
            }

            // Add each entry of the players on high score list to the global collection of all players
            // top lists.
            AddPlayersSavedTopList(player);
        }

        if (_candidateCount == 0) return;

        SortRecordsByTime();

        int nextPosition = 0;
        bool changed = false;
        for (int i = 0; i < _candidateCount && nextPosition < MaxRecordsOnLeaderboard; i++)
        {
            // The candidates are sorted, so a name already in the top list has a faster / equal time there.
            if (IsInTopList(_allRecordNames[i], nextPosition)) continue;

            if (_topNames[nextPosition] != _allRecordNames[i] || !Mathf.Approximately(_topTimes[nextPosition], _allRecordTimes[i]))
            {
                changed = true;
            }

            _topNames[nextPosition] = _allRecordNames[i];
            _topTimes[nextPosition] = _allRecordTimes[i];
            nextPosition++;
        }

        if (nextPosition != _topCount)
        {
            changed = true;
        }
        _topCount = nextPosition;

        if (changed)
        {
            SaveHighScoreList();
        }
        Redraw();
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

    private bool IsInTopList(string displayName, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_topNames[i] == displayName) return true;
        }
        return false;
    }

    private void SaveHighScoreList()
    {
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

    private void SortRecordsByTime()
    {
        for (int currentIndex = 1; currentIndex < _candidateCount; currentIndex++)
        {
            // first loop 
            // starts [5, 2, 4, 6, 1]  
            // time is assigned 2 from slot 1

            // second loop 
            // starts [2, 5, 4, 6, 1]  
            // time is assigned 4 from slot 2
            // third loop 
            // starts [2, 4, 5, 6, 1]  
            // time is assigned 6 from slot 3
            float recordTime = _allRecordTimes[currentIndex];
            string recordName = _allRecordNames[currentIndex];
            // first loop comparisonIndex is 0
            // second loop comparisonIndex is 1
            // third loop comparisonIndex is 2
            int comparisonIndex = currentIndex - 1;
            while (comparisonIndex >= 0 && _allRecordTimes[comparisonIndex] > recordTime)
            {
                // first loop inner first, slot 1 holding 2 is assigned 5 [5, 2, 4, 6, 1]  -> [5, 5, 4, 6, 1]  
                // second loop inner first, slot 2 holding 4 is assigned 5  [2, 5, 4, 6, 1]  -> [2, 5, 5, 6, 1]  
                _allRecordTimes[comparisonIndex + 1] = _allRecordTimes[comparisonIndex];
                _allRecordNames[comparisonIndex + 1] = _allRecordNames[comparisonIndex];
                // first loop comparisonIndex becomes -1 and loop breaks
                // second loop comparisonIndex becomes 0 and breaks
                comparisonIndex--;
            }

            // first loop slot 0 holding  5 is assigned 2 [5, 5, 4, 6, 1]  -> [2, 5, 4, 6, 1]  
            // second loop slot 1 holding 5 is assigned 4 [2, 5, 5, 6, 1]  -> [2, 4, 5, 6, 1]  
            _allRecordTimes[comparisonIndex + 1] = recordTime;
            _allRecordNames[comparisonIndex + 1] = recordName;
        }
    }

    // Called when vrchat has loaded the persistent data for a player
    // that joined the instance.
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            _localRestored = true;
        }

        if (_localRestored)
        {
            Rebuild();
        }
    }

    // When the persistent player data changes, for example a player got a new highscore, this class is called.
    public override void OnPlayerDataUpdated(VRCPlayerApi player, PlayerData.Info[] infos)
    {
        if (_localRestored)
        {
            Rebuild();
        }
    }
}
