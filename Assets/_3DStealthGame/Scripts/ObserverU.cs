
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
        Debug.Log("OnPlayerTriggerEnter, setting _isPlayerCaught to true.");        
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
                Debug.Log("Player was caught by raycast");
                if (_gameEnding != null)
                    _gameEnding.CaughtPlayer();
            }
        }
    }
}
