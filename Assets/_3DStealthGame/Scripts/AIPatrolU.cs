
using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.SDKBase;
using VRC.Udon;

public class AIPatrolU : UdonSharpBehaviour
{    // select the radius in which the enemy can find a new random destination
    public float wanderRadius = 8f;
    // set the time the enemy can stay there 
    public float wanderTimer = 1f;

    private NavMeshAgent agent;
    private float timer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;

        // Only the master need to update the navmesh agent position
        agent.enabled = Networking.IsOwner(gameObject);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        // If master logged out or left instance, the new master will take control of the navmesh agent
        Debug.Log($"OnOwnershipTransferred Owner of ghost? {Networking.IsOwner(gameObject)}");
        agent.enabled = Networking.IsOwner(gameObject);
        if (agent.enabled)
        {
            // Since agent was turned off, we need to update it with information about where
            // the ghost should actually start at. 
            agent.Warp(transform.position);
            timer = 0;
        }
    }
    void Update()
    {
        // Only the owner of the object should be updating the navmesh agent
        if (!Networking.IsOwner(gameObject)) return;

        timer += Time.deltaTime;

        //after the enemy has stayed there for the wanderTime you have defined,  
        //it will calculate a new destination and go there

        if (timer >= wanderTimer)
        {
            Vector3 newPos = RandomNavPosition(transform.position, wanderRadius, NavMesh.AllAreas);
            agent.SetDestination(newPos);
            timer = 0;
        }
    }

    // a method where we calculate the new destination point. First, we define a random direction vector 
    // and set its max magnitude to the wanderRandius 
    Vector3 RandomNavPosition(Vector3 origin, float dist, int layermask)
    {
        Vector3 randDirection = Random.insideUnitSphere * dist;
        randDirection += origin;

        // we check if there is an available point to move inside the navmesh surface 
        // within the magnitude and direction of the randDirection vector
        if (NavMesh.SamplePosition(randDirection, out NavMeshHit navHit, dist, layermask))
        {
            //If there is an available point under those conditions, it returns that point position
            return navHit.position;
        }
        else
            // if there is no point available, stay in the same point
            return origin;
    }
}
