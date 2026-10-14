using Assets._3DStealthGame.Scripts;

using UdonSharp;

using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GargoyleU : Resettable
{
    public Material opaqueMaterial;
    public Material fadeMaterial;
    public float fadeDuration = 1.5f;

    private Renderer _renderer;
    private Material[] _originalMaterials;
    private Collider _solidCollider;
    private ObserverU _observer;

    private bool _isFading;
    private float _fadeTimer;

    private Material[] _fadingMaterials;
    private float[] _startAlphas;

    public override void Start()
    {
        base.Start();

        _renderer = GetComponentInChildren<Renderer>();
        _solidCollider = GetComponent<Collider>();
        _observer = GetComponentInChildren<ObserverU>();

        if (opaqueMaterial == null || fadeMaterial == null)
        {
            Debug.LogError($"GargoyleU ({gameObject.name}): opaqueMaterial and fadeMaterial need to be assigned in the inspector.");
        }

        if (_renderer == null)
        {
            Debug.LogError($"GargoyleU ({gameObject.name}): Failed to get renderer in the children.");
        }

        if (_solidCollider == null)
        {
            Debug.LogError($"GargoyleU ({gameObject.name}): no Collider on the gargoyle root.");
        }

        if (_observer == null)
        {
            Debug.LogError($"GargoyleU ({gameObject.name}):.");
        }

        _originalMaterials = _renderer.sharedMaterials;

        Material[] materials = _renderer.sharedMaterials;
		// The renderer has both the main gargoyle material
		// and the flashlight material. The flashlight material
		// is already transparent so no need to swap it.
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == opaqueMaterial)
            {
                materials[i] = fadeMaterial;
            }
        }
        _renderer.sharedMaterials = materials;

        _fadingMaterials = _renderer.materials;
        _renderer.sharedMaterials = _originalMaterials;

        _startAlphas = new float[_fadingMaterials.Length];
        for (int i = 0; i < _fadingMaterials.Length; i++)
        {
            _startAlphas[i] = _fadingMaterials[i].color.a;
        }
    }

    public void FadeOut()
    {
        if (!_solidCollider.enabled) return;

        _observer.gameObject.SetActive(false);
        _solidCollider.enabled = false;
        _renderer.sharedMaterials = _fadingMaterials;

        _fadeTimer = 0f;
        _isFading = true;
    }

    void Update()
    {
        if (!_isFading) return;

        _fadeTimer += Time.deltaTime;
        float remaining = 1f - Mathf.Clamp01(_fadeTimer / fadeDuration);
        SetVisibility(remaining);

        if (remaining > 0f) return;

        _isFading = false;
        _renderer.enabled = false;
    }

    public override void ResetState()
    {
        _isFading = false;

        _renderer.sharedMaterials = _originalMaterials;
        _renderer.enabled = true;
        SetVisibility(1f);

        _solidCollider.enabled = true;
        _observer.gameObject.SetActive(true);
    }

    private void SetVisibility(float visibility)
    {
        for (int i = 0; i < _fadingMaterials.Length; i++)
        {
            Color color = _fadingMaterials[i].color;
            color.a = _startAlphas[i] * visibility;
            _fadingMaterials[i].color = color;
        }
    }
}
