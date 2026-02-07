using UdonSharp;

public class ResetManager : UdonSharpBehaviour
{
    public UdonSharpBehaviour[] resettables = new UdonSharpBehaviour[0];

    public void Register(UdonSharpBehaviour udonScript)
    {
        if (udonScript == null) return;

        int count = resettables.Length;
        var next = new UdonSharpBehaviour[count  + 1];
        for (int i = 0; i < count ; i++)
        {
            next[i] = resettables[i];
        }
        next[count ] = udonScript;
        resettables = next;
    }

    public void ResetAll()
    {
        for (int i = 0; i < resettables.Length; i++)
        {
            var b = resettables[i];
            if (b != null) b.SendCustomEvent("ResetState");
        }
    }
}