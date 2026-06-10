
#pragma warning disable IDE0090 // Use 'new(...)'
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class ObserverU : UdonSharpBehaviour
{
    private bool _isPlayerInRange;
    private VRCPlayerApi _localPlayer;
    private GameEndingU _gameEnding;

    public AudioSource exitAudio;
    public AudioSource caughtAudio;
    private readonly bool _hasAudioPlayed;


    void Start()
    {
        _localPlayer = Networking.LocalPlayer;

        var gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj == null)
        {
            Debug.LogError("ObserverU: could not find a GameManager GameObject in the scene");
            return;
        }

        _gameEnding = gameManagerObj.GetComponent<GameEndingU>();
        if (_gameEnding == null)
            Debug.LogError("ObserverU: GameManager has no GameEndingU component");
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        Vector3 playerPos = player.GetPosition();
        Debug.Log($"OnPlayerTriggerEnter | entity '{transform.root.name}' | player pos {playerPos} | entity pos {transform.position} | distance {Vector3.Distance(transform.position, playerPos):F2}m");
        if (player == _localPlayer)
        {
            _isPlayerInRange = true;
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == _localPlayer)
        {
            _isPlayerInRange = false;
        }
    }

    private void Update()
    {
        if (_isPlayerInRange && _localPlayer != null)
        {
            Vector3 playerPos = _localPlayer.GetPosition();
            Vector3 direction = playerPos - transform.position + Vector3.up;
            float distToPlayer = Vector3.Distance(transform.position, playerPos);

            Ray ray = new Ray(transform.position, direction);

            LayerMask playerMask = 1 << LayerMask.NameToLayer("Player");

            if (!Physics.Raycast(ray, out RaycastHit hitInfo, distToPlayer, playerMask, QueryTriggerInteraction.Ignore))
            {
                string fromDir = DirectionToText(playerPos);
                Debug.Log($"Player caught by '{transform.root.name}' | from the {fromDir} | player pos {playerPos} | catcher pos {transform.position} | distance {distToPlayer:F2}m");
                if (_gameEnding != null)
                    _gameEnding.CaughtPlayer();
            }
        }
    }

    // Works out where the catcher is relative to the way the player is facing,
    // as plain words (in front, behind, to the right, etc.). Flattened to the
    // horizontal plane so up/down doesn't matter.
    private string DirectionToText(Vector3 playerPos)
    {
        Vector3 forward = _localPlayer.GetRotation() * Vector3.forward;
        forward.y = 0f;

        Vector3 toCatcher = transform.position - playerPos;
        toCatcher.y = 0f;

        // Positive angle = catcher is to the player's right, negative = left.
        float angle = Vector3.SignedAngle(forward, toCatcher, Vector3.up);
        float a = Mathf.Abs(angle);

        if (a <= 22.5f) return "front";
        if (a >= 157.5f) return "behind";

        if (angle > 0f)
        {
            if (a <= 67.5f) return "front-right";
            if (a <= 112.5f) return "right";
            return "behind-right";
        }
        else
        {
            if (a <= 67.5f) return "front-left";
            if (a <= 112.5f) return "left";
            return "behind-left";
        }
    }
}
