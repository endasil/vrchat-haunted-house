#pragma warning disable UNT0039
using MMMaellon;

using System;

using UdonSharp;

using UnityEngine;

using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

// Inherit from SmartObjectSyncListener so we can subscribe to OnChangeState
// events from SmartObjectSync. This way we can switch to non-kinematic state
// when the snowball is thrown so smart
public class Snowball : SmartObjectSyncListener
{
    [Header("RespawnAndEnable Settings")]
    public Vector3 respawnPosition;
    public float respawnDelay = 2f;

    [Header("Impact Effects")]
    public GameObject impactParticles;
    public float minImpactVelocity = 1.4f;

    [Header("Ghost Alerts")]
    // OnSnowballImpact goes only to seekers within this distance of an impact.
    public float seekerAlertRadius = 20f;

    [Header("Desktop Throw")]
    // Speed in m/s along the view direction when a desktop player lets go of the snowball.
    public float desktopThrowSpeed = 12f;

    [Header("Collider Setup")]
    public SphereCollider pickupCollider;
    public SphereCollider physicsCollider;

    [UdonSynced] private Vector3 _syncedImpactPosition;
    [UdonSynced] private Vector3 _syncedImpactNormal;
    [UdonSynced] private int _syncedImpactEventId;
    private SmartObjectSync _smartObjectSync;
    private int _lastHandledImpactEventId;

    private bool _hasImpacted;
    // The hit player shows the burst before the owner syncs the impact, so OnDeserialization skips it.
    private bool _burstShownLocally;
    private VRCPickup _vrcPickup;
    private Rigidbody _rb;
    private MeshRenderer _meshRenderer;
    private bool _hasInitialized;
    private int _ownerLayer;
    private int _remoteLayer;
    private void Start()
    {
        _ownerLayer = LayerMask.NameToLayer("Snowball");
        _remoteLayer = LayerMask.NameToLayer("SnowballRemote");
        if (_ownerLayer == -1 || _remoteLayer == -1)
        {
            LogError("The physics layers Snowball and SnowballRemote need to exist in the project settings.");
        }
        UpdateLayerForOwner();

        _lastHandledImpactEventId = _syncedImpactEventId;

        _vrcPickup = GetComponent<VRCPickup>();
        _rb = GetComponent<Rigidbody>();
        _meshRenderer = GetComponent<MeshRenderer>();

        respawnPosition = transform.position;

        if (_rb) _rb.isKinematic = true;

        if (pickupCollider)
        {
            pickupCollider.enabled = true;
            pickupCollider.isTrigger = true;
        }

        // An idle snowball has its collider off, so other snowballs pass through it.
        if (physicsCollider) physicsCollider.enabled = false;

        _smartObjectSync = GetComponent<SmartObjectSync>();

        if (_smartObjectSync)
        {
            // We need to listen to state change events from SmartObjectSync and switch kinematic mode there.
            // Without that, position syncing does not work correctly for clients that do not own the object.
            _smartObjectSync.AddListener(this);
        }
    }

    public override void OnPickup()
    {
        _hasImpacted = false;
        // The ownership transfer can come after OnPickup.
        gameObject.layer = _ownerLayer;

        // Need to turn off kinematic when picked up so that throwing works correctly.
        if (_rb) _rb.isKinematic = false;

        if (physicsCollider != null) physicsCollider.enabled = true;
    }

    // A desktop hand barely moves, so letting go of the mouse button would only drop the
    // snowball. On desktop a release throws it along the view. VR players throw with their hand.
    public override void OnDrop()
    {
        if (Networking.LocalPlayer.IsUserInVR()) return;

        // One frame after a drop, VRChat sets the hand's velocity on the pickup and
        // SmartObjectSync.OnDropDelayed runs. Two frames is after both, so neither replaces the throw velocity.
        SendCustomEventDelayedFrames(nameof(_ThrowAlongView), 2);
    }

    public void _ThrowAlongView()
    {
        Quaternion headRotation = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
        ReleaseFromHand(headRotation * Vector3.forward * desktopThrowSpeed);
    }

    // Serialize runs last, so the other clients get the throw velocity and not a velocity of zero.
    private void ReleaseFromHand(Vector3 velocity)
    {
        if (physicsCollider) physicsCollider.enabled = true;

        _smartObjectSync.state = SmartObjectSync.STATE_INTERPOLATING;

        if (_rb)
        {
            _rb.isKinematic = false;
            _rb.velocity = velocity;
        }

        _smartObjectSync.Serialize();
    }

    // A player collision is only reliable for the local player, so the hit player's client
    // detects the hit and sends it to the owner.
    public override void OnPlayerCollisionEnter(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        // The owner is the thrower, touching their own snowball.
        if (Networking.IsOwner(gameObject)) return;
        if (_hasImpacted || !IsFlying()) return;

        _hasImpacted = true;
        _burstShownLocally = true;

        // A player collision has no contact point or normal, so use the snowball centre
        // and point the particles opposite to its velocity.
        Vector3 position = transform.position;
        Vector3 normal = Vector3.up;
        if (_rb.velocity.sqrMagnitude > 0.0001f)
        {
            normal = -_rb.velocity.normalized;
        }

        DisableSnowball();
        SpawnParticles(position, normal);
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnHitPlayer), position, normal);
    }

    // The hit player already checked the hit, so the owner skips the speed check. The
    // snowball has slowed down or bounced on the owner's client by the time this arrives.
    [NetworkCallable]
    public void OnHitPlayer(Vector3 position, Vector3 normal)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_hasImpacted) return;
        if (_vrcPickup && _vrcPickup.IsHeld) return;

        RegisterImpact(position, normal);
        DisableSnowball();
        SpawnParticles(position, normal);
        AlertSeekersNearImpact(position);

        SendCustomEventDelayedSeconds(nameof(_RespawnAndEnable), respawnDelay);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        UpdateLayerForOwner();
    }

    // Snowball does not collide with PlayerLocal, so a held snowball does not push the holder.
    // SnowballRemote does, so the hit player's client gets OnPlayerCollisionEnter.
    private void UpdateLayerForOwner()
    {
        if (Networking.IsOwner(gameObject))
        {
            gameObject.layer = _ownerLayer;
        }
        else
        {
            gameObject.layer = _remoteLayer;
        }
    }

    private bool IsFlying()
    {
        if (!_smartObjectSync) return false;

        int state = _smartObjectSync.state;
        return state == SmartObjectSync.STATE_INTERPOLATING || state == SmartObjectSync.STATE_FALLING;
    }

    // Called whenever there are network updates for this class.
    public override void OnDeserialization(DeserializationResult result)
    {
        if (!_hasInitialized)
        {
            _hasInitialized = true;
            _lastHandledImpactEventId = _syncedImpactEventId;
        }

        // Since syncing variables across the network is slower
        // than sending network events, we can't rely on network events
        // to create the snowball particles because it is possible that the
        // function would be called before we have new values for syncedImpactPosition
        // and normal and it would spawn particles on a previous position. instead
        // solve it incrementing a synced syncedImpactCounter variable every time we want
        // to spawn particles and then when a client checks for latest messages on network
        // it checks if the variable has been updated to a newer value. If it ha
        // we know we have received a new impact position that particles has not yet been spawned
        // at yet for this client, and can perform the spawning.
        if (_syncedImpactEventId > _lastHandledImpactEventId)
        {
            // Stops OnChangeState from turning the collider on again for a hidden snowball.
            _hasImpacted = true;
            DisableSnowball();
            if (!_burstShownLocally)
            {
                SpawnParticles(_syncedImpactPosition, _syncedImpactNormal);
            }
            _burstShownLocally = false;
            _lastHandledImpactEventId = _syncedImpactEventId;
        }
    }

    private bool TryRegisterImpact(Vector3 pos, Vector3 normal)
    {
        if (!Networking.IsOwner(gameObject)) return false;
        if (_vrcPickup && _vrcPickup.IsHeld) return false;
        // A snowball grabbed over the shoulder is attached to a hand bone without VRCPickup, so IsHeld
        // is false for it. Without this check, a player who walks into a wall with it makes an impact.
        if (_smartObjectSync && _smartObjectSync.IsAttachedToPlayer()) return false;
        if (_hasImpacted) return false;
        if (_rb && _rb.velocity.magnitude < minImpactVelocity) return false;

        RegisterImpact(pos, normal);
        return true;
    }

    private void RegisterImpact(Vector3 pos, Vector3 normal)
    {
        _hasImpacted = true;

        _syncedImpactPosition = pos;
        _syncedImpactNormal = normal;
        _syncedImpactEventId++;
        _lastHandledImpactEventId = _syncedImpactEventId;
        RequestSerialization();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contacts.Length == 0)
            return;

        var contact = collision.contacts[0];

        SeekerGhost ghost = collision.gameObject.GetComponent<SeekerGhost>();
        ButlerGhost butler = collision.gameObject.GetComponent<ButlerGhost>();
        GargoyleU gargoyle = collision.gameObject.GetComponent<GargoyleU>();

        if (!TryRegisterImpact(contact.point, contact.normal))
            return;

        DisableSnowball();
        SpawnParticles(contact.point, contact.normal);
        SendCustomEventDelayedSeconds(nameof(_RespawnAndEnable), respawnDelay);

        if (ghost != null)
        {
            NotifyGhostHit(ghost);
        }
        else
        {
            AlertSeekersNearImpact(contact.point);
        }

        if (butler != null)
            NotifyButlerHit(butler);

        // TryRegisterImpact only passes on the thrower's client, so the gargoyle fades for the thrower alone.
        if (gargoyle != null)
        {
            gargoyle.FadeOut();
        }
    }

    // Sends the thrower's position to the ghost's owner, so the ghost can investigate it.
    private void NotifyGhostHit(SeekerGhost ghost)
    {
        VRCPlayerApi thrower = Networking.GetOwner(gameObject);
        if (thrower == null || !thrower.IsValid()) return;
        ghost.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(SeekerGhost.OnSnowballHit), thrower.GetPosition());
    }

    // OnSnowballImpact runs on the seeker's owner and checks the impact against the seeker's vision cone and hearing range.
    private void AlertSeekersNearImpact(Vector3 impactPosition)
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(impactPosition, seekerAlertRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider nearbyCollider in nearbyColliders)
        {
            // VRChat does not give Udon the player colliders, so OverlapSphere returns null for a player inside the sphere.
            if (nearbyCollider == null) continue;

            SeekerGhost seeker = nearbyCollider.GetComponent<SeekerGhost>();
            if (seeker == null) continue;

            seeker.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(SeekerGhost.OnSnowballImpact), impactPosition);
        }
    }

    // The butler chases the thrower while they move, so it needs the player, not
    // a position. The butler's owner gets the player from the id.
    private void NotifyButlerHit(ButlerGhost butler)
    {
        VRCPlayerApi thrower = Networking.GetOwner(gameObject);
        if (thrower == null || !thrower.IsValid()) return;
        butler.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(ButlerGhost.OnSnowballHit), thrower.playerId);
    }

    private void SpawnParticles(Vector3 position, Vector3 normal)
    {
        if (impactParticles != null)
        {
            GameObject particles = Instantiate(impactParticles);
            particles.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
        }
        else
        {
            LogError("No impact particles assigned!");
        }
    }

    private void DisableSnowball()
    {
        if (_meshRenderer) _meshRenderer.enabled = false;

        if (physicsCollider) physicsCollider.enabled = false;

        if (_vrcPickup) _vrcPickup.pickupable = false;
    }

    public void EnableSnowball()
    {
        // A hit player's client sets _hasImpacted without owning the snowball, and nothing
        // else resets it there.
        _hasImpacted = false;
        _burstShownLocally = false;
        if (_meshRenderer) _meshRenderer.enabled = true;
        if (pickupCollider) pickupCollider.enabled = true;
        if (_vrcPickup) _vrcPickup.pickupable = true;
    }
    public void _RespawnAndEnable()
    {
        if (!Networking.IsOwner(gameObject)) return;

        if (_smartObjectSync)
        {
            _smartObjectSync.TeleportToWorldSpace(respawnPosition, Quaternion.identity, Vector3.zero, Vector3.zero);
        }
        else
        {
            LogError("No SmartObjectSync on snowball.");
        }

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EnableSnowball));
    }

    // Automatically switch off kinematic when the object is moving and switch to kinematic
    // when it is at rest. SmartObjectSync would not correctly update the position of objects
    // that are kinematic on non owners system. Subscribing to the smartobjectsyncs state change
    // in start and toggling kinematic here makes sure that the toggling is happening for clients too.
    public override void OnChangeState(SmartObjectSync sync, int oldState, int newState)
    {
        // Enable physics while flying
        if (newState == SmartObjectSync.STATE_INTERPOLATING ||
            newState == SmartObjectSync.STATE_FALLING)
        {
            _rb.isKinematic = false;
            // Only the owner turns the collider on in OnPickup. The hit player's client needs it for OnPlayerCollisionEnter.
            if (!_hasImpacted)
            {
                physicsCollider.enabled = true;
            }
            return;
        }

        // Disable physics when respawned / teleported / sleeping
        if (newState == SmartObjectSync.STATE_TELEPORTING ||
            newState == SmartObjectSync.STATE_SLEEPING)
        {
            _rb.isKinematic = true;
            physicsCollider.enabled = false;
        }
    }

    private void LogError(string message)
    {
        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        string ownerName = owner != null ? owner.displayName : "None";
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Debug.LogError($"<color=cyan>[{timestamp}]</color>[{gameObject.name}][Owner: {ownerName}] {message}");
    }

    public override void OnChangeOwner(SmartObjectSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner) { }
}
