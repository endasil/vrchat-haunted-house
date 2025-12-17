
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SnowballTracker : UdonSharpBehaviour
{
    private const int MAX_SNOWBALLS = 1;
    private int currentSnowballCount = 0;
    
    public bool CanPickupSnowball()
    {
        return currentSnowballCount < MAX_SNOWBALLS;
    }
    
    public void OnSnowballPickedUp()
    {
        currentSnowballCount++;
    }
    
    public void OnSnowballDropped()
    {
        currentSnowballCount--;
        if (currentSnowballCount < 0)
        {
            currentSnowballCount = 0;
        }
    }
    
    public int GetCurrentCount()
    {
        return currentSnowballCount;
    }
}
