using UdonSharp;

using UnityEngine;

using VRC.Udon;

public class Resettable : UdonSharpBehaviour
{
    [SerializeField] protected ResetManager manager;

    protected bool registered;

    public virtual void Start()
    {
        TryRegister();
    }

    protected void TryRegister()
    {
        if (registered) return;
        if (manager == null) return;

        manager.Register(this);
        registered = true;
    }
}

