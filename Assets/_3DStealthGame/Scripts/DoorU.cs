using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

public class DoorU : Resettable
{
    public float openDuration = 1f;
    private bool opened = false;
    private bool opening;
    private float timer;
    private Quaternion startRot;
    private Quaternion targetRot;
    public ResetManager resetManager;

    public override void Start()
    {
        startRot = transform.localRotation;
        targetRot = Quaternion.Euler(-90f, startRot.eulerAngles.y, startRot.eulerAngles.z);
        base.Start();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal || opened) return;
        
        timer = 0f;
        opening = true;
    }

    void Update()
    {
        if (!opening || opened) return;

        timer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(timer / openDuration);

        transform.localRotation = Quaternion.Slerp(startRot, targetRot, normalizedTime );

        if (normalizedTime  >= 1f)
        {
            opened = true;
            opening = false;
        }
    }

    public void ResetState()
    {
        Debug.Log(gameObject.name + "ResetState");
        opening = false;
        opened = false;
        timer = 0f;
        transform.localRotation = startRot;
    }
}