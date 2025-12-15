
using System.Collections.Generic;

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

// No actual movement handling in this script, just retaining the name for easy reference to original code
public class PlayerMovementU : UdonSharpBehaviour
{
    // U# does not support lists, use array instead, pre-alloc a size.
    private string[] m_OwnedKeys = new string[50];
    private int m_KeyCount = 0;
    public void AddKey(string keyName)
    {
        if (m_KeyCount < m_OwnedKeys.Length)
        {
            m_OwnedKeys[m_KeyCount] = keyName;
            m_KeyCount++;
        }
        else 
        {
            // If the size límit is reached, allocate space for 5 more
            // and log an error.
            string[] newArray = new string[m_OwnedKeys.Length + 5];
            m_OwnedKeys.CopyTo(newArray, 0);
            newArray[m_KeyCount] = keyName;
            m_OwnedKeys = newArray;
            Debug.LogWarning("Key array grew larger than buffer and was expanded. Perhaps a bigger preallocated buffer is needed?");
        }
    }

    public bool OwnKey(string keyName)
    {
        // Loop through the array based on owned keys and
        // check for matching name.
        for (int i = 0; i < m_KeyCount; i++)
        {
            if (m_OwnedKeys[i] == keyName)
            {
                return true;
            }
        }
        return false;
    }

}
