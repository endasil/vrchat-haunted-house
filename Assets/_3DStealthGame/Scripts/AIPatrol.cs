//using UnityEngine;
//using UnityEngine.AI;

//public class AIPatrol : MonoBehaviour
//{
//    // select the radius in which the enemy can find a new random destination
//    public float wanderRadius = 3f;
//    // set the time the enemy can stay there 
//    public float wanderTimer = 1.5f;

//    private NavMeshAgent agent;
//    private float timer;

//    void Start()
//    {
//        agent = GetComponent<NavMeshAgent>();
//        timer = wanderTimer;
//    }

//    void Update()
//    {
//        timer += Time.deltaTime;

//        //after the enemy has stayed there for the wanderTime you have defined,  
//        //it will calculate a new destination and go there

//        if (timer >= wanderTimer)
//        {
//            Vector3 newPos = RandomNavPosition(transform.position, wanderRadius, NavMesh.AllAreas);
//            agent.SetDestination(newPos);
//            timer = 0;
//        }
//    }

//    // a method where we calculate the new destination point. First, we define a random direction vector 
//    // and set its max magnitude to the wanderRandius 
//    Vector3 RandomNavPosition(Vector3 origin, float dist, int layermask)
//    {
//        Vector3 randDirection = Random.insideUnitSphere * dist;
//        randDirection += origin;

//        // we check if there is an avaliable point to move inside the navmesh surface 
//        // within the magnitude and direction of the randDirection vector
//        if (NavMesh.SamplePosition(randDirection, out NavMeshHit navHit, dist, layermask))
//        {
//            //If there is an available point under those conditions, it returns that point position
//            return navHit.position;
//        }
//        else
//            // if there is no point avaliable, stay in the same point
//            return origin;
//    }
//}