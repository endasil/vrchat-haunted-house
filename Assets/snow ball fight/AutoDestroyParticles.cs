#pragma warning disable UNT0039 // Use RequireComponent attribute when self-invoking GetComponent
using UdonSharp;
using UnityEngine;

public class AutoDestroyParticles : UdonSharpBehaviour
{
    private ParticleSystem particles;
    
    private void Start()
    {
        particles = GetComponent<ParticleSystem>();


        if (particles != null)
        {
            float lifetime = particles.main.duration + particles.main.startLifetime.constantMax;
            SendCustomEventDelayedSeconds(nameof(DestroyThis), lifetime);
        }
    }
    
    public void DestroyThis()
    {
        Destroy(gameObject);
    }
}

