using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

using _3DStealthGame.Scripts.Constants;
using Assets._3DStealthGame.Scripts.Enums;

namespace Assets._3DStealthGame.Scripts
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RoofTrigger : UdonSharpBehaviour
    {
        private GameEndingU _gameEnding;

        void Start()
        {
            var gameManagerObj = GameObject.Find("GameManager");
            if (gameManagerObj == null)
            {
                Debug.LogError($"RoofTrigger ({gameObject.name}): could not find a GameManager GameObject in the scene", gameObject);
                return;
            }

            _gameEnding = gameManagerObj.GetComponent<GameEndingU>();
            if (_gameEnding == null)
            {
                Debug.LogError($"RoofTrigger ({gameObject.name}): GameManager has no GameEndingU component", gameObject);
            }
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!player.isLocal) return;
            // The admin cheat also turns off the ghost catches.
            if (PlayerData.TryGetBool(player, PlayerDataKeys.IsCheatActive, out bool isCheatActive) && isCheatActive) return;

            _gameEnding.CaughtPlayerBy(GhostType.House);
        }
    }
}
