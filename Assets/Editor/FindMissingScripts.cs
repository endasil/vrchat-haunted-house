using UnityEditor;
using UnityEngine;

public class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts")]
    private static void Find()
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null)
                {
                    Debug.Log(GetFullPath(go), go);
                }
            }
        }
    }

    static string GetFullPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}