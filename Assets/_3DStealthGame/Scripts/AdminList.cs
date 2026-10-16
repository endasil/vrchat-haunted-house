using UdonSharp;
using VRC.SDKBase;

namespace Assets._3DStealthGame.Scripts
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AdminList : UdonSharpBehaviour
    {
        public string[] adminNames;

        public bool IsAdmin(VRCPlayerApi player)
        {
            foreach (string adminName in adminNames)
            {
                if (player.displayName == adminName) return true;
            }
            return false;
        }
    }
}
