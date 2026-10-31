
using Assets._3DStealthGame.Scripts.Enums;

using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GameEndingU : UdonSharpBehaviour
{
    public float fadeDuration = 1f;
    public float displayImageDuration = 1f;

    public CanvasGroup endScreenCanvasGroup;
    public CanvasGroup caughtScreenCanvasGroup;

    public AudioSource exitAudio;
    public AudioSource caughtAudio;

    // Used to resets things like doors, pills, gargoyle and inventory when 
    // player is caught or win.
    public ResetManager resetManager;

    public MusicFader music;

    public FogFader fog;

	// Showing the death message consist of two component. A TextMeshProUGUI 
	// on the DeathMessageBoard canvas in root the world and separate canvas 
	// with only black. 
	// The idea is to have the game over text detached from head movement 
    // so it looks like the player sees a white text in a black void.


    public TextMeshProUGUI caughtMessageText;

     public TextMeshProUGUI wonMessageText;

    public float wonMessageDuration = 8f;

    public Transform caughtRespawnPoint;

    private const float EndingCanvasDistance = 1.6f;
    
    private bool _isPlayerAtExit;
    private bool _isPlayerCaught;
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
        "[Player] was eeriaten by the Butler, with the candlestick.",
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
        "[Player] was wraithed mid-stride by the Seeker.",
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

    private CanvasGroup _deathMessageGroup;
    private CanvasGroup _wonMessageGroup;

    private CanvasGroup _endingScreenGroup;
    private CanvasGroup _endingMessageGroup;
    private float _endingDisplayDuration;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        _spawnPosition = _localPlayer.GetPosition();
        _spawnRotation = _localPlayer.GetRotation();
        CaptureSpawn();

        if (caughtMessageText == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): caughtMessageText needs to be assigned in the inspector.");
        }
        else
        {
            Transform deathMessageCanvas = caughtMessageText.transform.parent;
            _deathMessageGroup = deathMessageCanvas.GetComponent<CanvasGroup>();
            if (_deathMessageGroup == null) 
                Debug.LogError($"GameEndingU ({gameObject.name}): {deathMessageCanvas.name} has no CanvasGroup, so the death message can't fade.");
        }

        if (wonMessageText == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): wonMessageText needs to be assigned in the inspector.");
        }
        else
        {
            Transform wonMessageCanvas = wonMessageText.transform.parent;
            _wonMessageGroup = wonMessageCanvas.GetComponent<CanvasGroup>();
            if (_wonMessageGroup == null)
                Debug.LogError($"GameEndingU ({gameObject.name}): {wonMessageCanvas.name} has no CanvasGroup Add one to the component.");
        }

        if (resetManager == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): resetManager needs to be assigned in the inspector.");
        }

        if (endScreenCanvasGroup == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): endScreenCanvasGroup needs to be assigned in the inspector.");
        }

        if (caughtScreenCanvasGroup == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): caughtScreenCanvasGroup needs to be assigned in the inspector.");
        }

        if (exitAudio == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): exitAudio needs to be assigned in the inspector.");
        }

        if (caughtAudio == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): caughtAudio needs to be assigned in the inspector.");
        }

        if (music == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): music needs to be assigned in the inspector.");
        }

        if (fog == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): fog needs to be assigned in the inspector.");
        }

        if (caughtRespawnPoint == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): caughtRespawnPoint needs to be assigned in the inspector.", gameObject);
        }
    }

 
    //  Attempt to fix the floating text issue by storing where the player is once they landed in the world.
	//  does not solve the problem, but leaving it here for now. can't decide if the effect is interesting 
	//   or uggly.
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

    // Public so ReachedExit can call it delayed. It also places the message canvas, since
    // that has to wait for the head to arrive at spawn.
    public void TeleportPlayerToSpawn()
    {
        TeleportAndPlaceEndingCanvas(_spawnPosition, _spawnRotation);
    }

    private void TeleportPlayerToCaughtRespawn()
    {
        TeleportAndPlaceEndingCanvas(caughtRespawnPoint.position, caughtRespawnPoint.rotation);
    }

    private void TeleportAndPlaceEndingCanvas(Vector3 position, Quaternion rotation)
    {
        _localPlayer.TeleportTo(position, rotation);

        _placeCanvasAttemptsLeft = MaxPlaceCanvasAttempts;
        SendCustomEventDelayedFrames(nameof(PlaceEndingCanvas), 1);
    }

    // Immobilize only turn off control, it doesn't take away the speed the player
    // already has, so without clearing the velocity they carry on sliding for a
    // bit after the screen goes black. 
    private void FreezePlayer()
    {
        _localPlayer.Immobilize(true);
        _localPlayer.SetVelocity(Vector3.zero);
    }


    // In VR the head can lag behind the body for a few frames after a teleport. Placing
    // the canvas before it arrives leaves the message floating far away from the player.
    private const float MaxHeadDistanceFromBody = 3f;
    private const int MaxPlaceCanvasAttempts = 60;
    private int _placeCanvasAttemptsLeft;

    public void PlaceEndingCanvas()
    {
        Transform canvas = _endingMessageGroup.transform;
        VRCPlayerApi.TrackingData head = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

        // Gives up waiting after a while and places it at the head anyway, since that is where the player sees from.
        bool isHeadAtBody = Vector3.Distance(head.position, _localPlayer.GetPosition()) < MaxHeadDistanceFromBody;
        _placeCanvasAttemptsLeft--;
        if (!isHeadAtBody && _placeCanvasAttemptsLeft > 0)
        {
            SendCustomEventDelayedFrames(nameof(PlaceEndingCanvas), 1);
            return;
        }

        // Only the direction they're facing, so looking up or down while it's placed
        // doesn't cause tilt .
        Quaternion facing = Quaternion.Euler(0f, head.rotation.eulerAngles.y, 0f);

        Vector3 offset = new Vector3(0f, 0, EndingCanvasDistance);
        canvas.position = head.position + facing * offset;
        canvas.rotation = facing;
        _endingMessageGroup.alpha = 1;
    }

    // Called by ExitTrigger script when the local player walks into the exit trigger.
    public void ReachedExit(float runTime, bool hasBestTime, float bestTime, bool isNewBest, int leaderboardPosition)
    {
        if (_isPlayerAtExit || _isPlayerCaught) return;

        _isPlayerAtExit = true;
        FreezePlayer();
        music.PauseMusic();
        fog.Clear();

        wonMessageText.text = BuildWonMessage(runTime, hasBestTime, bestTime, isNewBest, leaderboardPosition);

        ShowEnding(endScreenCanvasGroup, _wonMessageGroup, wonMessageDuration, exitAudio);

        // ReachedExit runs from the exit trigger, which is inside the physics step. Teleporting
        // there in VR left the head stuck halfway to spawn, so it waits until Update.
        SendCustomEventDelayedFrames(nameof(TeleportPlayerToSpawn), 1);
    }

    private string BuildWonMessage(float runTime, bool hasBestTime, float bestTime, bool isNewBest, int leaderboardPosition)
    {
        string message = "<size=150%>You escaped the haunted house!</size>\n\n";
        message += "Your time: " + TimeFormatter.Format(runTime) + "\n";

        if (isNewBest)
        {
            message += "That is your new best time!\n";
        }
        else if (hasBestTime)
        {
            message += "Your best time: " + TimeFormatter.Format(bestTime) + "\n";
        }

        if (leaderboardPosition > 0)
        {
            message += "You are now number " + leaderboardPosition + " on the leaderboard!";
        }

        return message;
    }

    // Called by ghosts when the local player is caught.
    public void CaughtPlayerBy(GhostType ghostType)
    {
        if (_isPlayerAtExit || _isPlayerCaught) return;

        _isPlayerCaught = true;
        FreezePlayer();
        music.PauseMusic();
        fog.Clear();

        caughtMessageText.text = BuildOwnDeathMessage(ghostType);

        TeleportPlayerToCaughtRespawn();
        ShowEnding(caughtScreenCanvasGroup, _deathMessageGroup, displayImageDuration, caughtAudio);
    }

    private void ShowEnding(CanvasGroup screenGroup, CanvasGroup messageGroup, float displayDuration, AudioSource audioSource)
    {
        _endingScreenGroup = screenGroup;
        _endingMessageGroup = messageGroup;
        _endingDisplayDuration = displayDuration;

        audioSource.Play();
        screenGroup.alpha = 1;
    }

    private string[] GetMessagesFor(GhostType ghostType)
    {
        if (ghostType == GhostType.Butler) return _butlerMessages;
        if (ghostType == GhostType.Seeker) return _seekerMessages;
        if (ghostType == GhostType.Gargoyle) return _gargoyleMessages;
        return null;
    }

    private string BuildOwnDeathMessage(GhostType ghostType)
    {
        string message = BuildDeathMessage(ghostType);
        message = message.Replace("[Player] was ", "You were ");
        message = message.Replace(" them ", " you ");
        return message.Replace(" they ", " you ");
    }

    private string BuildDeathMessage(GhostType ghostType)
    {
        string[] messages = GetMessagesFor(ghostType);
        if (messages == null)
        {
            Debug.LogError($"GameEndingU ({gameObject.name}): unknown ghost type {ghostType}, using fallback death message");
            return "[Player] was claimed by something nameless in the dark.";
        }

        return messages[Random.Range(0, messages.Length)];
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        if (_isPlayerAtExit || _isPlayerCaught) return;

        music.PauseMusic();
        fog.Clear();
        resetManager.ResetAll();
    }

    public void Update()
    {
        if (_isPlayerAtExit || _isPlayerCaught)
        {
            EndLevel();
        }
    }

    void EndLevel()
    {
        _timer += Time.deltaTime;

        if (_timer > _endingDisplayDuration)
        {
            float fade = 1 - (_timer - _endingDisplayDuration) / fadeDuration;
            _endingScreenGroup.alpha = fade;
            _endingMessageGroup.alpha = fade;
        }

        if (_timer > _endingDisplayDuration + fadeDuration)
        {
            resetManager.ResetAll();

            _endingScreenGroup.alpha = 0;
            _deathMessageGroup.alpha = 0;
            _wonMessageGroup.alpha = 0;


            _localPlayer.Immobilize(false);

            _isPlayerAtExit = false;
            _isPlayerCaught = false;

            _timer = 0;
        }
    }
}
