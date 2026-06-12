using Assets._3DStealthGame.Scripts;

using UdonSharp;

public class ResetManager : UdonSharpBehaviour
{
    public Resettable[] resettables = new Resettable[0];

    public void Register(Resettable resettable)
    {
        if (resettable == null) return;

        int count = resettables.Length;
        var next = new Resettable[count + 1];
        for (int i = 0; i < count; i++)
        {
            next[i] = resettables[i];
        }
        next[count] = resettable;
        resettables = next;
    }

    public void ResetAll()
    {
        for (int i = 0; i < resettables.Length; i++)
        {
            var resettable = resettables[i];
            if (resettable != null) resettable.ResetState();
        }
    }
}
