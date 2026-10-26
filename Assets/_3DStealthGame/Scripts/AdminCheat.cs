using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Assets._3DStealthGame.Scripts
{
    // Any player presses the triggers left, right, right, left, left to switch the cheat on or off for themselves.
    // The cheat makes their fog blue, stops the ghosts from catching them and adds 99 minutes to their run time.
    // Right, left, left, right, right, or Ctrl+Alt+Shift+P, gives the local admin the next missing pill.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AdminCheat : UdonSharpBehaviour
    {
        public AdminList adminList;
        public EscapeTimer escapeTimer;
        public Color cheatFogColor = new Color(0.05f, 0.2f, 0.6f, 1f);
        public float maxSecondsBetweenPresses = 0.5f;

        // Played for the local player when they toggle the cheat.
        public AudioSource toggleAudio;
        public AudioClip cheatOnSound;
        public AudioClip cheatOffSound;

        private const int ComboLength = 5;
        private const bool Left = true;
        private const bool Right = false;

        // Five presses so the combos don't go off by accident while playing.
        private bool[] _toggleCheatCombo = { Left, Right, Right, Left, Left };
        private bool[] _nextPillCombo = { Right, Left, Left, Right, Right };

        // The last trigger presses, oldest first. true is a left hand press.
        private bool[] _recentPresses = new bool[ComboLength];
        private int _recentPressCount;
        private float _lastPressTime;

        private bool _isAdmin;
        private bool _isCheatActive;
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

            if (toggleAudio == null || cheatOnSound == null || cheatOffSound == null)
            {
                Debug.LogError($"AdminCheat ({gameObject.name}): toggleAudio, cheatOnSound and cheatOffSound need to be assigned in the inspector.");
            }
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

            observer.enabled = !_isCheatActive;
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!value) return;

            if (Time.time - _lastPressTime > maxSecondsBetweenPresses)
            {
                _recentPressCount = 0;
            }
            _lastPressTime = Time.time;

            RecordPress(args.handType == HandType.LEFT);
            if (_recentPressCount < ComboLength) return;

            if (IsCombo(_toggleCheatCombo))
            {
                _recentPressCount = 0;
                _ToggleCheat();
            }
            else if (_isAdmin && IsCombo(_nextPillCombo))
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

        private bool IsCombo(bool[] combo)
        {
            for (int i = 0; i < ComboLength; i++)
            {
                if (_recentPresses[i] != combo[i]) return false;
            }
            return true;
        }

        // Public so the editor cheat menu can call it. The underscore stops other clients from calling it with a network event.
        public void _ToggleCheat()
        {
            _isCheatActive = !_isCheatActive;
            ApplyCheatState();

            if (_isCheatActive)
            {
                PlayToggleSound(cheatOnSound);
            }
            else
            {
                PlayToggleSound(cheatOffSound);
            }
        }

        private void PlayToggleSound(AudioClip clip)
        {
            if (toggleAudio == null || clip == null) return;
            toggleAudio.PlayOneShot(clip);
        }

        private void ApplyCheatState()
        {
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
