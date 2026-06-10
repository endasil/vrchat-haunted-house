
using UdonSharp;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GameEndingU : UdonSharpBehaviour
{
    public float fadeDuration = 1f;
    public float displayImageDuration = 1f;

    private bool _isPlayerAtExit;
    private bool m_IsPlayerCaught;
    private float m_Timer = 0;

    public CanvasGroup m_EndScreenCanvasGroup;
    public CanvasGroup m_CaughtScreenCanvasGroup;

    public AudioSource exitAudio;
    public AudioSource caughtAudio;
    bool _endLevelStarted;


    private Vector3 m_SpawnPosition;
    private Quaternion m_SpawnRotation;
    private VRCPlayerApi m_LocalPlayer;
    void Start()
    {
        m_LocalPlayer = Networking.LocalPlayer;

        // Make sure screens start invisible
        //if (m_EndScreenCanvasGroup != null)
        //    m_EndScreenCanvasGroup.alpha = 0f;
        //if (m_CaughtScreenCanvasGroup != null)
        //    m_CaughtScreenCanvasGroup.alpha = 0f;
        
        // Store the spawn position when the player loads in
        m_SpawnPosition = m_LocalPlayer.GetPosition();
        m_SpawnRotation = m_LocalPlayer.GetRotation();
    }

    public void TeleportPlayerToSpawn()
    {
        m_LocalPlayer.TeleportTo(m_SpawnPosition, m_SpawnRotation);
    }


    // Called by ExitTriggerU when the local player walks into the exit trigger.
    public void ReachedExit()
    {
        _isPlayerAtExit = true;
    }

    public void CaughtPlayer()
    {
        m_IsPlayerCaught = true;
    }

    public void Update()
    {
        if (_isPlayerAtExit)
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


        if (!_endLevelStarted)
        {
            if (m_IsPlayerCaught)
            {
                TeleportPlayerToSpawn();
            }
            audioSource.Play();
            _endLevelStarted = true;
        }

        m_Timer += Time.deltaTime;
        canvasGroup.alpha = m_Timer / fadeDuration;

        if (m_Timer > fadeDuration + displayImageDuration)
        {
            if (_isPlayerAtExit)
            {
                Debug.Log("Player at exit!");
                TeleportPlayerToSpawn();
            }
            canvasGroup.alpha = 0;
            _isPlayerAtExit = false;
            m_IsPlayerCaught = false;

            m_Timer = 0;
            _endLevelStarted = false;
        }
    }
}
