using UdonSharp;

using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HauntedPaintings : UdonSharpBehaviour
{
    private const string IntensityParameter = "Intensity";

    public Animator[] paintingAnimators;
    public Transform seekerGhost;

    public float startDistance = 10f;
    public float fullDistance = 2f;

    public float minAnimatorSpeed = 0.5f;
    public float maxAnimatorSpeed = 1.5f;

    public float intensityChangePerSecond = 1f;

    private float[] _intensities;
    private bool _isSetUp;

    private void Start()
    {
        if (paintingAnimators == null || paintingAnimators.Length == 0 || seekerGhost == null)
        {
            Debug.LogError($"HauntedPaintings ({gameObject.name}): paintingAnimators and seekerGhost need to be assigned in the inspector.", gameObject);
            return;
        }

        _intensities = new float[paintingAnimators.Length];

        for (int i = 0; i < paintingAnimators.Length; i++)
        {
            Animator painting = paintingAnimators[i];
            if (painting == null)
            {
                Debug.LogError($"HauntedPaintings ({gameObject.name}): paintingAnimators element {i} needs to be assigned in the inspector.", gameObject);
                return;
            }

            ApplyIntensity(painting, 0f);
        }

        _isSetUp = true;
    }

    private void Update()
    {
        if (!_isSetUp) return;

        float maxChange = intensityChangePerSecond * Time.deltaTime;
        bool isSeekerActive = seekerGhost.gameObject.activeInHierarchy;

        for (int i = 0; i < paintingAnimators.Length; i++)
        {
            Animator painting = paintingAnimators[i];

            float targetIntensity = 0f;
            if (isSeekerActive)
            {
                float distance = Vector3.Distance(painting.transform.position, seekerGhost.position);
                targetIntensity = Mathf.InverseLerp(startDistance, fullDistance, distance);
            }

            _intensities[i] = Mathf.MoveTowards(_intensities[i], targetIntensity, maxChange);
            ApplyIntensity(painting, _intensities[i]);
        }
    }

    private void ApplyIntensity(Animator painting, float intensity)
    {
        painting.SetFloat(IntensityParameter, intensity);
        painting.speed = Mathf.Lerp(minAnimatorSpeed, maxAnimatorSpeed, intensity);
    }
}
