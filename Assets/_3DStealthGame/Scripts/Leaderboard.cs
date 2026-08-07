using _3DStealthGame.Scripts.Constants;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Leaderboard: UdonSharpBehaviour
{
    public TextMeshProUGUI leaderboardText;

    public int maxRecords = 10;

    private const int MaxPlayers = 80;

    // Every player plus the fake entries.
    private const int MaxRecords = 90;

    private VRCPlayerApi[] _vrcPlayer = new VRCPlayerApi[MaxPlayers];

    private string[] _recordNames = new string[MaxRecords];

    private float[] _recordTimes = new float[MaxRecords];

    private string[] _fakeNames = new string[]
    {
        "Sephiroth", "Aerith", "Cloud Strife",
        "Tifa Lockhart", "Yuffie Kisaragi", "Vincent",
        "Red XIII", "Barret Wallace", "Cid Highwind",
        "Cait Sith"
    };

    // Bunch of fake fastest run to fill empty leaderboard, time in seconds.
    // 
    private float[] _fakeTimes = new float[]
    {
        3972f, 4238f, 4445f, 4671f, 4879f, 5084f, 5307f, 5518f, 5733f, 5949f,
    };

    public void Start()
    {
        //Redraw();
    }

    public void Redraw()
    {
        if(leaderboardText == null)
        {
            Debug.LogError($"Leaderboard ({gameObject.name}): display text is not assigned in the inspector.");
            return;
        }

        int playerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_vrcPlayer);

        int recordCount = 0;
        for (int i = 0; i < _fakeNames.Length; i++)
        {
            _recordNames[recordCount] = _fakeNames[i];
            _recordTimes[recordCount] = _fakeTimes[i];
            recordCount++;
        }

        for (int i = 0; i < playerCount; i++)
        {
            VRCPlayerApi player = _vrcPlayer[i];
            if(!Utilities.IsValid(player)) continue;
            if (!PlayerData.HasKey(player, PlayerDataKeys.BestEscapeTime)) continue;
            _recordNames[recordCount] = player.displayName;
            _recordTimes[recordCount] = PlayerData.GetFloat(player, PlayerDataKeys.BestEscapeTime);
            recordCount++;
        }

        SortRecordsByTime(recordCount);

        int rows = maxRecords;
        if (rows > recordCount)
        {
            rows = recordCount;
        }

        string text = "";
        for (int i = 0; i < rows; i++)
        {
            // Pos is used to alight all times at 72% of the width to get them to display under each other.
            text += (i + 1) + ". " + ShortName(_recordNames[i]) + "<pos=72%>" + TimeFormatter.FormatCapped(_recordTimes[i]) + "\n";
        }
        leaderboardText.text = text;
    }

    private string ShortName(string name)
    {
        if (name.Length <= 16) return name;
        return name.Substring(0, 13) + "...";
    }

    private void SortRecordsByTime(int count)
    {
        for (int currentIndex = 1; currentIndex < count; currentIndex++)
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
            float recordTime = _recordTimes[currentIndex];
            string recordName = _recordNames[currentIndex];
            // first loop comparisonIndex is 0
            // second loop comparisonIndex is 1
            // third loop comparisonIndex is 2
            int comparisonIndex = currentIndex - 1;
            while (comparisonIndex >= 0 && _recordTimes[comparisonIndex] > recordTime)
            {
                // first loop inner first, slot 1 holding 2 is assigned 5 [5, 2, 4, 6, 1]  -> [5, 5, 4, 6, 1]  
                // second loop inner first, slot 2 holding 4 is assigned 5  [2, 5, 4, 6, 1]  -> [2, 5, 5, 6, 1]  
                // third loop inner first, slot 3 holding 6 is assigned 5  [2, 4, 5, 6, 1] -> [2, 4, 5, 5, 1] 
                // third loop, inner second, slot 2 holding 5 is assigned 6 [2, 4, 5, 5, 1]  -> [2, 4, 6, 5, 1] 
                _recordTimes[comparisonIndex + 1] = _recordTimes[comparisonIndex];
                _recordNames[comparisonIndex + 1] = _recordNames[comparisonIndex];
                // first loop comparisonIndex becomes -1 and loop breaks
                // second loop comparisonIndex becomes 0 and breaks
                // third loop comparisonIndex becomes 1 and loops again
                // third loop inner 2 comparisonIndex becomes 0 and loop breaks
                comparisonIndex--;
            }

            // first loop slot 0 holding  5 is assigned 2 [5, 5, 4, 6, 1]  -> [2, 5, 4, 6, 1]  
            // second loop slot 1 holding 5 is assigned 4 [2, 5, 5, 6, 1]  -> [2, 4, 5, 6, 1]  
            // third loop slot 1 holding 5 is assigned 4 [2, 5, 5, 6, 1]  -> [2, 4, 5, 6, 1]  
            _recordTimes[comparisonIndex + 1] = recordTime;
            _recordNames[comparisonIndex + 1] = recordName;
        }
    }


    // Called when vrchat has loaded the persistent data for a player
    // that joined the instance.
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        Redraw();
    }

    // When the persistent player data changes,for example a player got a new highscore, this class is called. 
    public override void OnPlayerDataUpdated(VRCPlayerApi player, PlayerData.Info[] infos)
    {
        Redraw();
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        Redraw();
    }




}