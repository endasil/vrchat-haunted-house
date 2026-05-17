
using System;
using StealthGame;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ObserverU : UdonSharpBehaviour
{
    private bool _isPlayerInRange;
    private VRCPlayerApi _localPlayer;
    public GameEndingU _gameEnding;

    public AudioSource exitAudio;
    public AudioSource caughtAudio;
    private bool _HasAudioPlayed;


    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        if (_gameEnding == null)
        {
            Debug.LogError("gameEnding is null, add it in inspector.");
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        Debug.Log("Player caught");        
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

            if (Physics.Raycast(ray, distToPlayer))
            {
                Debug.Log("Player was caught");
                _gameEnding.CaughtPlayer();


            }
        }
    }
}
