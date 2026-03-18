
using System;
using StealthGame;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ObserverU : UdonSharpBehaviour
{
    private bool m_IsPlayerInRange;
    private VRCPlayerApi localPlayer;
    public GameEndingU gameEnding;

    public AudioSource exitAudio;
    public AudioSource caughtAudio;
    bool m_HasAudioPlayed;


    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (gameEnding == null)
        {
            Debug.LogError("gameEnding is null, add it in inspector.");
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        Debug.Log("Player caught");
        return;
        if (player == localPlayer)
        {
            m_IsPlayerInRange = true;
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == localPlayer)
        {
            m_IsPlayerInRange = false;
        }
    }

    private void Update()
    {
        if (m_IsPlayerInRange && localPlayer != null)
        {
            Vector3 playerPos = localPlayer.GetPosition();
            Vector3 direction = playerPos - transform.position + Vector3.up;
            float distToPlayer = Vector3.Distance(transform.position, playerPos);
            Ray ray = new Ray(transform.position, direction);

            if (Physics.Raycast(ray, distToPlayer))
            {
                Debug.Log("Player was caught");
                gameEnding.CaughtPlayer();


            }
        }
    }
}
