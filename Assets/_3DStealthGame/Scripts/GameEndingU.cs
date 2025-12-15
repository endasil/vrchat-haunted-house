
using UdonSharp;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

using VRC.SDKBase;
using VRC.Udon;

public class GameEndingU : UdonSharpBehaviour
{
    public float fadeDuration = 1f;
    public float displayImageDuration = 1f;

    private bool m_IsPlayerAtExit;
    private bool m_IsPlayerCaught;
    private float m_Timer = 0;

    public CanvasGroup m_EndScreenCanvasGroup;
    public CanvasGroup m_CaughtScreenCanvasGroup;

    public AudioSource exitAudio;
    public AudioSource caughtAudio;
    bool m_HasAudioPlayed;


    private Vector3 m_SpawnPosition;
    private Quaternion m_SpawnRotation;
    private VRCPlayerApi m_LocalPlayer;
    void Start()
    {
        m_LocalPlayer = Networking.LocalPlayer;

        // Make sure screens start invisible
        if (m_EndScreenCanvasGroup != null)
            m_EndScreenCanvasGroup.alpha = 0f;
        if (m_CaughtScreenCanvasGroup != null)
            m_CaughtScreenCanvasGroup.alpha = 0f;
        
        // Store the spawn position when the player loads in
        m_SpawnPosition = m_LocalPlayer.GetPosition();
        m_SpawnRotation = m_LocalPlayer.GetRotation();
    }

    public void TeleportPlayerToSpawn()
    {
        m_LocalPlayer.TeleportTo(m_SpawnPosition, m_SpawnRotation);
    }

    // Update is called once per frame
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        Debug.Log("OnPlayerTriggerEnter");
        if (player == m_LocalPlayer)
        {
            Debug.Log("player == m_LocalPlayer");

            m_IsPlayerAtExit = true;
        }
    }

    public void CaughtPlayer()
    {
        m_IsPlayerCaught = true;
    }

    public void Update()
    {
        if (m_IsPlayerAtExit)
        {
            EndLevel(m_EndScreenCanvasGroup, exitAudio);
        }
        else if (m_IsPlayerCaught)
        {
            EndLevel(m_CaughtScreenCanvasGroup, caughtAudio);
        }
    }

    void EndLevel(CanvasGroup canvasGroup, AudioSource audioSource)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is null in EndLevel");
            return;
        }


        if (!m_HasAudioPlayed)
        {
            audioSource.Play();
            m_HasAudioPlayed = true;
        }

        m_Timer += Time.deltaTime;
        canvasGroup.alpha = m_Timer / fadeDuration;
        Debug.Log($"EndLevel. alpha {canvasGroup.alpha}");

        if (m_Timer > fadeDuration + displayImageDuration)
        {
                TeleportPlayerToSpawn();
                canvasGroup.alpha = 0;
                m_IsPlayerAtExit = false;
                m_IsPlayerCaught = false;

                m_Timer = 0;
                m_HasAudioPlayed = false;
        }
    }
}
