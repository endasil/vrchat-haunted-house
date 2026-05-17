using UdonSharp;
using UnityEngine;

namespace Assets._3DStealthGame.Scripts
{
    public class Resettable : UdonSharpBehaviour
    {
        public ResetManager manager;
        protected bool registered;

        public virtual void Start()
        {
            TryRegister();
        }

        protected void TryRegister()
        {
            if (registered) return;
            if (manager == null)
            {
                var go = GameObject.Find("ResetManager");
                if (go != null) manager = go.GetComponent<ResetManager>();
                else
                {
                    Debug.LogError("Failed to find ResetManager.");
                }
            }
            if (manager == null)
            {
                Debug.LogError($"[Resettable] {gameObject.name} could not find a ResetManager in the scene.", gameObject);
                return;
            }

            manager.Register(this);
            registered = true;
        }
    }
}

