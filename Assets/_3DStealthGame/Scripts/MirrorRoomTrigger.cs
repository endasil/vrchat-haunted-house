using UdonSharp;

using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MirrorRoomTrigger : UdonSharpBehaviour
{
    private const int SnapshotSize = 256;
    private const int SnapshotDepthBits = 16;
    private const float SnapshotViewDistance = 2f;
    private const string LeftEyeReflection = "_ReflectionTex0";
    private const string RightEyeReflection = "_ReflectionTex1";

    private VRCMirrorReflection[] _mirrors;
    private Renderer[] _glassRenderers;
    private Camera _snapshotCamera;
    private MaterialPropertyBlock _emptyBlock;
    private int _snapshotIndex;

    void Start()
    {
        _mirrors = transform.parent.GetComponentsInChildren<VRCMirrorReflection>(true);

        if (_mirrors.Length == 0)
        {
            Debug.LogError($"MirrorRoomTrigger ({gameObject.name}): found no VRCMirrorReflection under {transform.parent.name}.", gameObject);
            return;
        }

        _snapshotCamera = GetComponentInChildren<Camera>(true);

        if (_snapshotCamera == null)
        {
            Debug.LogError($"MirrorRoomTrigger ({gameObject.name}): needs a child object with a disabled Camera.", gameObject);
            return;
        }

        _glassRenderers = new Renderer[_mirrors.Length];
        for (int i = 0; i < _mirrors.Length; i++)
        {
            _glassRenderers[i] = _mirrors[i].GetComponent<Renderer>();
        }

        _emptyBlock = new MaterialPropertyBlock();
        _snapshotCamera.nearClipPlane = SnapshotViewDistance;

        SetMirrorsEnabled(false);
        RenderNextSnapshot();
    }

    public void RenderNextSnapshot()
    {
        if (_snapshotIndex == _mirrors.Length)
        {
            _snapshotCamera.enabled = false;
            return;
        }

        Renderer glass = _glassRenderers[_snapshotIndex];
        Vector3 reflectionDirection = glass.transform.TransformVector(Vector3.back).normalized;
        Vector3 reflectedEyePosition = glass.bounds.center - reflectionDirection * SnapshotViewDistance;
        _snapshotCamera.transform.SetPositionAndRotation(reflectedEyePosition, Quaternion.LookRotation(reflectionDirection));

        RenderTexture snapshot = new RenderTexture(SnapshotSize, SnapshotSize, SnapshotDepthBits);
        _snapshotCamera.targetTexture = snapshot;
        _snapshotCamera.enabled = true;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetTexture(LeftEyeReflection, snapshot);
        block.SetTexture(RightEyeReflection, snapshot);
        glass.SetPropertyBlock(block);

        _snapshotIndex++;
        SendCustomEventDelayedFrames(nameof(RenderNextSnapshot), 1);
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal) return;

        foreach (Renderer glass in _glassRenderers)
        {
            glass.SetPropertyBlock(_emptyBlock);
        }

        SetMirrorsEnabled(true);
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (!player.isLocal) return;

        SetMirrorsEnabled(false);
    }

    private void SetMirrorsEnabled(bool isEnabled)
    {
        foreach (VRCMirrorReflection mirror in _mirrors)
        {
            mirror.enabled = isEnabled;
        }
    }
}
