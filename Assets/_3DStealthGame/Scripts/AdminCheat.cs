using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Assets._3DStealthGame.Scripts
{
    // An admin presses left trigger, right trigger, right trigger to switch the cheat on or off for everyone.
    // The cheat makes the fog blue, stops the ghosts from catching players and adds 99 minutes to each run time.
    // Right trigger, left trigger, left trigger, or Ctrl+Alt+Shift+P, gives the local admin the next missing pill.
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AdminCheat : UdonSharpBehaviour
    {
        public AdminList adminList;
        public EscapeTimer escapeTimer;
        public Color cheatFogColor = new Color(0.03f, 0.08f, 0.25f, 1f);
        public float maxSecondsBetweenPresses = 0.5f;

        private const int ComboLength = 3;
        private const bool Left = true;
        private const bool Right = false;

        // The last trigger presses, oldest first. true is a left hand press.
        private bool[] _recentPresses = new bool[ComboLength];
        private int _recentPressCount;
        private float _lastPressTime;

        private bool _isAdmin;
        [UdonSynced] private bool _isCheatActive;
        private bool _isCheatApplied;
        private Color _normalFogColor;
        private ObserverU[] _observers = new ObserverU[0];

        private void Start()
        {
            if (adminList == null || escapeTimer == null)
            {
                Debug.LogError($"AdminCheat ({gameObject.name}): adminList and escapeTimer need to be assigned in the inspector.");
                return;
            }

            _isAdmin = adminList.IsAdmin(Networking.LocalPlayer);
            _normalFogColor = RenderSettings.fogColor;
        }

        private void Update()
        {
            if (!_isAdmin) return;
            if (!Input.GetKeyDown(KeyCode.P)) return;

            bool isCtrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool isAltHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!isCtrlHeld || !isAltHeld || !isShiftHeld) return;

            GiveNextMissingPill();
        }

        private void GiveNextMissingPill()
        {
            PlayerInventory playerInventory = Networking.LocalPlayer.GetPlayerObjects()[0].GetComponent<PlayerInventory>();
            if (playerInventory == null)
            {
                Debug.LogError($"AdminCheat ({gameObject.name}): the local player object has no PlayerInventory.");
                return;
            }
            playerInventory.AddNextMissingPill();
            escapeTimer.MarkPillCheatUsed();
        }

        public void Register(ObserverU observer)
        {
            int count = _observers.Length;
            ObserverU[] next = new ObserverU[count + 1];
            for (int i = 0; i < count; i++)
            {
                next[i] = _observers[i];
            }
            next[count] = observer;
            _observers = next;

            // A player who joins while the cheat is on can get the synced value before every observer has registered.
            observer.enabled = !_isCheatApplied;
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!value || !_isAdmin) return;

            if (Time.time - _lastPressTime > maxSecondsBetweenPresses)
            {
                _recentPressCount = 0;
            }
            _lastPressTime = Time.time;

            RecordPress(args.handType == HandType.LEFT);
            if (_recentPressCount < ComboLength) return;

            if (IsCombo(Left, Right, Right))
            {
                _recentPressCount = 0;
                ToggleCheat();
            }
            else if (IsCombo(Right, Left, Left))
            {
                _recentPressCount = 0;
                GiveNextMissingPill();
            }
        }

        // When the list is full, the oldest press is dropped.
        private void RecordPress(bool isLeft)
        {
            if (_recentPressCount == ComboLength)
            {
                for (int i = 1; i < ComboLength; i++)
                {
                    _recentPresses[i - 1] = _recentPresses[i];
                }
                _recentPressCount--;
            }
            _recentPresses[_recentPressCount] = isLeft;
            _recentPressCount++;
        }

        private bool IsCombo(bool first, bool second, bool third)
        {
            return _recentPresses[0] == first && _recentPresses[1] == second && _recentPresses[2] == third;
        }

        private void ToggleCheat()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _isCheatActive = !_isCheatActive;
            RequestSerialization();
            ApplyCheatState();
        }

        public override void OnDeserialization()
        {
            ApplyCheatState();
        }

        private void ApplyCheatState()
        {
            if (_isCheatActive == _isCheatApplied) return;
            _isCheatApplied = _isCheatActive;

            if (_isCheatActive)
            {
                RenderSettings.fogColor = cheatFogColor;
            }
            else
            {
                RenderSettings.fogColor = _normalFogColor;
            }

            foreach (ObserverU observer in _observers)
            {
                observer.enabled = !_isCheatActive;
            }

            escapeTimer.SetCheatActive(_isCheatActive);
        }
    }
}
