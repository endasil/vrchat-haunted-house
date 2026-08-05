
using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

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

	// Showing the death message consist of two component. A TextMeshProUGUI 
	// on the DeathMessageBoard canvas in root the world and separate canvas 
	// with only black. 
	// The idea is to have the game over text detached from head movement 
    // so it looks like the player sees a black text on the void instead of 
	// part of the hud.

    public TextMeshProUGUI caughtMessageText;

    // Offset to adjust the position of the death message relative to the player.
    private const float DeathMessageDistance = 1.6f;
    private const float DeathMessageHeight = 0f;

    private bool _isPlayerAtExit;
    private bool _isPlayerCaught;
    private bool _endLevelStarted;
    private float _timer;

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
        "[Player] was creepchomped by the Gargoyle.",
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

    // The gameObject the canvas the message sits on.
    private Transform _deathMessageBoard;
    private CanvasGroup _deathMessageGroup;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        _spawnPosition = _localPlayer.GetPosition();
        _spawnRotation = _localPlayer.GetRotation();
        SendCustomEventDelayedFrames(nameof(CaptureSpawn), 1);

        if (caughtMessageText == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): caughtMessageText need to be set in the inspector.");
        }
        else
        {
            _deathMessageBoard = caughtMessageText.transform.parent;
            _deathMessageGroup = _deathMessageBoard.GetComponent<CanvasGroup>();
            if (_deathMessageGroup == null)
                Debug.LogError($"GameEndingU ({gameObject.name}): {_deathMessageBoard.name} has no CanvasGroup, so the death message can't fade.");
        }

        if (resetManager == null)
        {
            var go = GameObject.Find("ResetManager");
            if (go != null) resetManager = go.GetComponent<ResetManager>();
        }
        if (resetManager == null)
            Debug.LogError($"GameEndingU ({gameObject.name}): could not find a ResetManager in the scene, so doors and pills won't reset after a catch.");
    }

    // Remembers where the player is standing once they've landed, rather than where
    // they were the instant the world loaded, which is in the air above the floor.
	// Placing the player in the air and dropping them causes a ugly effect where
	// the text message is floating up instead of staying at th center. 

    public void CaptureSpawn()
    {
        if (!_localPlayer.IsPlayerGrounded())
        {
            SendCustomEventDelayedFrames(nameof(CaptureSpawn), 1);
            return;
        }

        _spawnPosition = _localPlayer.GetPosition();
        _spawnRotation = _localPlayer.GetRotation();
    }

    public void TeleportPlayerToSpawn()
    {
        _localPlayer.TeleportTo(_spawnPosition, _spawnRotation);
    }

    // Immobilize only turn off control, it doesn't take away the speed the player
    // already has, so without clearing the velocity they carry on sliding for a
    // bit after the screen goes black. 
    private void FreezePlayer()
    {
        _localPlayer.Immobilize(true);
        _localPlayer.SetVelocity(Vector3.zero);
    }


    public void PlaceDeathMessage()
    {
        if (_deathMessageBoard == null || !_isPlayerCaught) return;

        VRCPlayerApi.TrackingData head = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

        // Only the direction they're facing, so looking up or down while it's placed
        // doesn't cause tilt .
        Quaternion facing = Quaternion.Euler(0f, head.rotation.eulerAngles.y, 0f);

        Vector3 offset = new Vector3(0f, DeathMessageHeight, DeathMessageDistance);
        _deathMessageBoard.position = head.position + facing * offset;
        _deathMessageBoard.rotation = facing;

        if (_deathMessageGroup != null)
            _deathMessageGroup.alpha = 1;
    }

    // Called by ExitTrigger script when the local player walks into the exit trigger.
    public void ReachedExit()
    {
        if (_isPlayerAtExit) return;

        _isPlayerAtExit = true;
        FreezePlayer();
    }

    // Called by ghosts when the local player is caught.
    public void CaughtPlayerBy(int ghostType)
    {
        if (_isPlayerCaught) return;

        if (_localPlayer == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): _localPlayer is null, Start ran before the local player existed.");
            return;
        }

        _isPlayerCaught = true;
        FreezePlayer();

        string[] messages = GetMessagesFor(ghostType);
        int messageIndex = messages == null ? 0 : Random.Range(0, messages.Length);

        if (caughtMessageText == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): caughtMessageText need to be set in the inspector.");
        }
        else
        {
            caughtMessageText.text = BuildOwnDeathMessage(ghostType, messageIndex);
        }
    }

    private string[] GetMessagesFor(int ghostType)
    {
        if (ghostType == GHOST_BUTLER) return _butlerMessages;
        if (ghostType == GHOST_SEEKER) return _seekerMessages;
        if (ghostType == GHOST_GARGOYLE) return _gargoyleMessages;
        return null;
    }

    private string BuildOwnDeathMessage(int ghostType, int messageIndex)
    {
        string message = BuildDeathMessage(ghostType, messageIndex, "[Player]");
        message = message.Replace("[Player] was ", "You were ");
        message = message.Replace(" them ", " you ");
        return message.Replace(" they ", " you ");
    }

    private string BuildDeathMessage(int ghostType, int messageIndex, string playerName)
    {
        string[] messages = GetMessagesFor(ghostType);
        if (messages == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): unknown ghost type {ghostType}, using fallback death message");
            return playerName + " was claimed by something nameless in the dark.";
        }

        if (messageIndex < 0 || messageIndex >= messages.Length)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): death message index {messageIndex} out of range, using 0");
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
            Debug.LogError($"GameEndingU ({gameObject.name}): CanvasGroup is null in EndLevel");
            return;
        }


        bool showMessage = _isPlayerCaught && _deathMessageGroup != null;

        if (!_endLevelStarted)
        {
            if (_isPlayerCaught)
            {
                TeleportPlayerToSpawn();
                // Wait one frame for the tracking data to be updated before using it to place 
				// the death message.
                SendCustomEventDelayedFrames(nameof(PlaceDeathMessage), 1);
            }
            audioSource.Play();
            canvasGroup.alpha = 1;
            _endLevelStarted = true;
        }

        _timer += Time.deltaTime;

        if (_timer > displayImageDuration)
        {
            float fade = 1 - (_timer - displayImageDuration) / fadeDuration;
            canvasGroup.alpha = fade;
            if (showMessage) _deathMessageGroup.alpha = fade;
        }

        if (_timer > displayImageDuration + fadeDuration)
        {
            if (_isPlayerAtExit)
            {
                TeleportPlayerToSpawn();
            }


            if (resetManager == null)
                Debug.LogError($"GameEndingU ({gameObject.name}): no ResetManage, unable to reset scene.");
            else
                resetManager.ResetAll();

            canvasGroup.alpha = 0;
            if (_deathMessageGroup != null) _deathMessageGroup.alpha = 0;


            _localPlayer.Immobilize(false);

            _isPlayerAtExit = false;
            _isPlayerCaught = false;

            _timer = 0;
            _endLevelStarted = false;
        }
    }
}
