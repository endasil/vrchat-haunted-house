using UdonSharp;
using UnityEngine;

namespace Assets._3DStealthGame.Scripts
{
    public class Resettable : UdonSharpBehaviour
    {
        public ResetManager resetManager;
        protected bool registered;

        public virtual void Start()
        {
            TryRegister();
        }

        // Called by ResetManager.ResetAll() between attempts. Subclasses override
        // this to restore their initial state.
        public virtual void ResetState()
        {
            Debug.LogError($"[Resettable] {gameObject.name} registered for reset but does not override ResetState().", gameObject);
        }

        protected void TryRegister()
        {
            if (registered) return;
            if (resetManager == null)
            {
                var go = GameObject.Find("ResetManager");
                if (go != null) resetManager = go.GetComponent<ResetManager>();
                else
                {
                    Debug.LogError($"Resettable ({gameObject.name}): Failed to find ResetManager.");
                }
            }
            if (resetManager == null)
            {
                Debug.LogError($"[Resettable] {gameObject.name} could not find a ResetManager in the scene.", gameObject);
                return;
            }

            resetManager.Register(this);
            registered = true;
        }
    }
}
