#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable UNT0026
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Assets._3DStealthGame.Scripts
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DoorU : Resettable
    {
        public float openDuration = 1f;
        private bool _opened = false;
        private bool _opening;
        private float _timer;
        private Quaternion _startRot;
        private Quaternion _targetRot;

        private ResetManager _resetManager;
        public PillColor PillColor;
        public float FloorAngle = -90;
        public override void Start()
        {
            _startRot = transform.localRotation;
            _targetRot = Quaternion.Euler(FloorAngle, _startRot.eulerAngles.y, _startRot.eulerAngles.z);
            base.Start();
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!player.isLocal || _opened) return;

            GameObject[] playerObjects = player.GetPlayerObjects();

            PlayerInventory playerInventory = playerObjects[0].GetComponent<PlayerInventory>();


            if (playerInventory != null)
            {
                if (playerInventory.HasPill(PillColor))
                {
                    Debug.Log($"Pill {PillColor} used");
                    _timer = 0f;
                    _opening = true;
                }
                else
                {
                    Debug.Log($"No {PillColor} pill in player inventory.");
                    return;
                }
            }
            else
            {
                Debug.LogError("Unable to find PlayerInventory script on player object");
            }
        }

        void Update()
        {
            if (!_opening || _opened) return;

            _timer += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(_timer / openDuration);

            transform.localRotation = Quaternion.Slerp(_startRot, _targetRot, normalizedTime );

            if (normalizedTime  >= 1f)
            {
                _opened = true;
                _opening = false;
            }
        }

        public void ResetState()
        {
            Debug.Log(gameObject.name + "ResetState");
            _opening = false;
            _opened = false;
            _timer = 0f;
            transform.localRotation = _startRot;
        }
    }
}