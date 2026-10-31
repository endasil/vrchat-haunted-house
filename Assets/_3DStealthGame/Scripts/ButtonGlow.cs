using UdonSharp;

using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ButtonGlow : UdonSharpBehaviour
{
    public MeshRenderer cap;
    public MeshRenderer halo;
    public Material capOnMaterial;
    public Material capOffMaterial;
    public Material haloOnMaterial;
    public Material haloOffMaterial;

    private void Start()
    {
        if (cap == null || halo == null || capOnMaterial == null || capOffMaterial == null || haloOnMaterial == null || haloOffMaterial == null)
        {
            Debug.LogError($"ButtonGlow ({gameObject.name}): cap, halo, capOnMaterial, capOffMaterial, haloOnMaterial and haloOffMaterial need to be assigned in the inspector.", gameObject);
        }
    }

    public void ShowOn(bool isOn)
    {
        if (isOn)
        {
            cap.sharedMaterial = capOnMaterial;
            halo.sharedMaterial = haloOnMaterial;
        }
        else
        {
            cap.sharedMaterial = capOffMaterial;
            halo.sharedMaterial = haloOffMaterial;
        }
    }
}
