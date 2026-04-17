using UdonSharp;
using UnityEngine;

namespace Assets._3DStealthGame.Scripts
{
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
}

