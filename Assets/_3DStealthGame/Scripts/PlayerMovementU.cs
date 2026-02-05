using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

public class PlayerMovementU : UdonSharpBehaviour
{
    // Track key counts by type
    private string[] keyTypes = new string[10];
    private int[] keyTypeCounts = new int[10];
    private int uniqueTypeCount = 0;

    public void AddKey(string keyType)
    {
        // Find if this type already exists
        int typeIndex = -1;
        for (int i = 0; i < uniqueTypeCount; i++)
        {
            if (keyTypes[i] == keyType)
            {
                typeIndex = i;
                break;
            }
        }

        // If type doesn't exist, add it
        if (typeIndex == -1)
        {
            if (uniqueTypeCount < keyTypes.Length)
            {
                keyTypes[uniqueTypeCount] = keyType;
                keyTypeCounts[uniqueTypeCount] = 1;
                uniqueTypeCount++;
            }
        }
        else
        {
            // Increment existing type count
            keyTypeCounts[typeIndex]++;
        }
    }

    public int GetKeyCount(string keyType)
    {
        for (int i = 0; i < uniqueTypeCount; i++)
        {
            if (keyTypes[i] == keyType)
            {
                return keyTypeCounts[i];
            }
        }
        return 0;
    }

    public void RemoveKey(string keyType)
    {
        for (int i = 0; i < uniqueTypeCount; i++)
        {
            if (keyTypes[i] == keyType && keyTypeCounts[i] > 0)
            {
                keyTypeCounts[i]--;
                return;
            }
        }
    }
}