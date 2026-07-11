
using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameEndingU : UdonSharpBehaviour
{
    public const int GHOST_BUTLER = 0;
    public const int GHOST_SEEKER = 1;
    public const int GHOST_GARGOYLE = 2;

    public float fadeDuration = 1f;
    public float displayImageDuration = 1f;

    public CanvasGroup endScreenCanvasGroup;
    public CanvasGroup caughtScreenCanvasGroup;

    public AudioSource exitAudio;
    public AudioSource caughtAudio;

    // Resets doors, pills and the inventory for the next attempt after the
    // caught fade finishes. Everything resettable is local-only (sync mode
    // None), so this doesn't disturb other players.
    public ResetManager resetManager;

    // Shown on the full-screen caught overlay (the player who died).
    public TextMeshProUGUI caughtMessageText;
    // Kill-feed line on the HUD, shown to everyone else.
    public TextMeshProUGUI deathFeedText;
    public float deathFeedDuration = 5f;

    private bool _isPlayerAtExit;
    private bool _isPlayerCaught;
    private bool _endLevelStarted;
    private float _timer;

    // Counts pending hide timers so an old timer doesn't clear a newer message.
    private int _pendingFeedHides;

    private string[] _butlerMessages = new string[]
    {
        "[Player] was spectreluched by the Butler. Terribly sorry about that.",
        "[Player] was hauntibbled by the Butler, who found them rather agreeable.",
        "[Player] was apparitasted by the Butler and deemed an acceptable vintage.",
        "[Player] was spectresipped by the Butler between duties.",
        "[Player] was phantomfeasted upon by the Butler. Three courses, apparently.",
        "[Player] was hauntivored by the Butler, who left no crumbs.",
        "[Player] was ghostguzzled by the Butler before the main course even arrived.",
        "[Player] was phantomchewed by the Butler, who was wholly unimpressed.",
        "[Player] was eeriaten by the Butler in the dining room, with the candlestick.",
        "[Player] was moanmunched by the Butler during his evening rounds.",
        "[Player] was boonibled by the Butler, who apologised afterwards.",
        "[Player] was spectreified by the Butler. He has done this before.",
        "[Player] was hauntched by the Butler. The ghost rated it three stars.",
        "[Player] was spiritsnacked by the Butler between guests.",
    };

    private string[] _seekerMessages = new string[]
    {
        "[Player] was phantommed into oblivion by the Seeker.",
        "[Player] was spooknapped by the Seeker and never seen again.",
        "[Player] was wraithed mid-stride by the Seeker, flashlight still on.",
        "[Player] was shimmerslurped through the floorboards by the Seeker.",
        "[Player] was spookswallowed by the Seeker, screaming all the way down.",
        "[Player] was gloomgulped into the shadows by the Seeker.",
        "[Player] was spooksorbed into the wall by the Seeker and never returned.",
        "[Player] was shiverswallowed whole by the Seeker, shivers and all.",
        "[Player] was shadowsnarfed by the Seeker before they even knew it was there.",
        "[Player] was eeriengulfed by the Seeker, despite definitely hearing it coming.",
        "[Player] was nightnibbled into the void by the Seeker.",
        "[Player] was vaporvored by the Seeker, here one moment, gone the next.",
        "[Player] was spookenated by the Seeker before they could run.",
        "[Player] was phantomgobbled by the Seeker before the door even opened.",
    };

    private string[] _gargoyleMessages = new string[]
    {
        "[Player] was cursecrunched by the Gargoyle like a bag of crisps.",
        "[Player] was tombchomped by the Gargoyle. A classic ending.",
        "[Player] was dreaddevoured by the Gargoyle, who seemed very pleased.",
        "[Player] was creepchomped from behind by the Gargoyle.",
        "[Player] was ghoulped whole by the Gargoyle.",
        "[Player] was wraithmunched by the Gargoyle like a midnight snack.",
        "[Player] was ghastrivored into the afterlife by the Gargoyle.",
        "[Player] was ghoulingested by the Gargoyle, who burped shortly after.",
        "[Player] was poltergobled by the Gargoyle at the worst possible moment.",
        "[Player] was ectoplasmed by the Gargoyle without warning.",
        "[Player] was boo'd out of existence by the Gargoyle.",
        "[Player] was phantomgobbled by the Gargoyle in one sitting.",
        "[Player] was ghoulped by the Gargoyle. It did not chew.",
        "[Player] was dreaddevoured by the Gargoyle. Bones and all.",
    };

    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private VRCPlayerApi _localPlayer;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;

        // Store the spawn position when the player loads in
        _spawnPosition = _localPlayer.GetPosition();
        _spawnRotation = _localPlayer.GetRotation();

        if (resetManager == null)
        {
            var go = GameObject.Find("ResetManager");
            if (go != null) resetManager = go.GetComponent<ResetManager>();
        }
        if (resetManager == null)
            Debug.LogError("GameEndingU: could not find a ResetManager in the scene, so doors and pills won't reset after a catch.");
    }

    public void TeleportPlayerToSpawn()
    {
        _localPlayer.TeleportTo(_spawnPosition, _spawnRotation);
    }

    // Called by ExitTriggerU when the local player walks into the exit trigger.
    public void ReachedExit()
    {
        _isPlayerAtExit = true;
    }

    // Called by ObserverU when the local player is caught by a ghost.
    public void CaughtPlayerBy(int ghostType)
    {
        if (_isPlayerCaught) return;

        if (_localPlayer == null)
        {
            Debug.LogError("GameEndingU: _localPlayer is null, Start ran before the local player existed.");
            return;
        }

        _isPlayerCaught = true;

        string[] messages = GetMessagesFor(ghostType);
        int messageIndex = messages == null ? 0 : Random.Range(0, messages.Length);
        string myName = _localPlayer.displayName;

        if (caughtMessageText == null)
            Debug.LogError("GameEndingU: caughtMessageText is not wired up");
        else
            caughtMessageText.text = BuildDeathMessage(ghostType, messageIndex, myName);

        // Everyone gets the feed event, including us, since All includes the sender.
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ShowDeathFeed), myName, ghostType, messageIndex);
    }

    [NetworkCallable]
    public void ShowDeathFeed(string playerName, int ghostType, int messageIndex)
    {
        // The caught player already sees the message on the full-screen overlay.
        if (playerName == Networking.LocalPlayer.displayName) return;

        if (deathFeedText == null)
        {
            Debug.LogError("GameEndingU: deathFeedText is not wired up");
            return;
        }

        deathFeedText.text = BuildDeathMessage(ghostType, messageIndex, playerName);
        _pendingFeedHides++;
        SendCustomEventDelayedSeconds(nameof(_HideDeathFeed), deathFeedDuration);
    }

    public void _HideDeathFeed()
    {
        _pendingFeedHides--;
        if (_pendingFeedHides <= 0)
        {
            _pendingFeedHides = 0;
            if (deathFeedText != null)
                deathFeedText.text = "";
        }
    }

    private string[] GetMessagesFor(int ghostType)
    {
        if (ghostType == GHOST_BUTLER) return _butlerMessages;
        if (ghostType == GHOST_SEEKER) return _seekerMessages;
        if (ghostType == GHOST_GARGOYLE) return _gargoyleMessages;
        return null;
    }

    private string BuildDeathMessage(int ghostType, int messageIndex, string playerName)
    {
        string[] messages = GetMessagesFor(ghostType);
        if (messages == null)
        {
            Debug.LogError($"GameEndingU: unknown ghost type {ghostType}, using fallback death message");
            return playerName + " was claimed by something nameless in the dark.";
        }

        if (messageIndex < 0 || messageIndex >= messages.Length)
        {
            Debug.LogError($"GameEndingU: death message index {messageIndex} out of range, using 0");
            messageIndex = 0;
        }
        return messages[messageIndex].Replace("[Player]", playerName);
    }

    public void Update()
    {
        if (_isPlayerAtExit)
        {
            EndLevel(endScreenCanvasGroup, exitAudio);
        }
        else if (_isPlayerCaught)
        {
            EndLevel(caughtScreenCanvasGroup, caughtAudio);
        }
    }

    void EndLevel(CanvasGroup canvasGroup, AudioSource audioSource)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is null in EndLevel");
            return;
        }

        if (!_endLevelStarted)
        {
            if (_isPlayerCaught)
            {
                TeleportPlayerToSpawn();
            }
            audioSource.Play();
            canvasGroup.alpha = 1;
            _endLevelStarted = true;
        }

        _timer += Time.deltaTime;

        if (_timer > displayImageDuration)
        {
            canvasGroup.alpha = 1 - (_timer - displayImageDuration) / fadeDuration;
        }

        if (_timer > displayImageDuration + fadeDuration)
        {
            if (_isPlayerAtExit)
            {
                TeleportPlayerToSpawn();
            }

            // The caught fade is over and the player is back at spawn, so reset
            // their doors, pills and inventory for the next attempt.
            if (_isPlayerCaught && resetManager != null)
                resetManager.ResetAll();

            canvasGroup.alpha = 0;
            _isPlayerAtExit = false;
            _isPlayerCaught = false;

            _timer = 0;
            _endLevelStarted = false;
        }
    }
}
