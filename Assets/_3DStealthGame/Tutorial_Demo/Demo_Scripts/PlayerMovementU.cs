
using System.Collections.Generic;

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

VRCPlayerApi playerApi;

// No actual movement handling in this script, just retaining the name for easy reference to original code
public class PlayerMovementU : UdonSharpBehaviour
{

    private List<string> m_OwnedKeys = new();

    public void AddKey(string keyName)
    {

        m_OwnedKeys.Add(keyName);
    }

    public bool OwnKey(string keyName)
    {
        return m_OwnedKeys.Contains(keyName);
    }

}
