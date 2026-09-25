using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BreadboardSync : UdonSharpBehaviour
{
    public BreadboardState state;
    public BreadboardCodec codec;
    public BreadboardController controller;
    public VRCPickup pickup;
    [UdonSynced, TextArea] public string snapshot = "";
    [UdonSynced] public int grantedPlayerId = -1;
    [UdonSynced] public int grantedRevision;
    [UdonSynced] public int grantToken;
    [HideInInspector] public bool hasState;
    [HideInInspector] public bool dirty;
    [HideInInspector] public string syncError = "";
    [HideInInspector] public int lastSentRevision = -1;
    [HideInInspector] public int lastByteCount;
    private string pendingSnapshot;
    private int pendingGrant = -1, pendingGrantRevision, acceptedGrant = -1;
    private int sendingRevision, sendingGrant, sendingToken;
    private int pendingToken, acceptedToken = -1;
    private int lastSentGrant = -1, lastSentToken = -1;
    private string sendingSnapshot;
    private float nextSendTime;
    private bool initializing;
    private int knownOwnerId = -1, previousOwnerId = -1;
    private bool awaitingGrant;
    private string sharedSnapshot;

    private void Start()
    {
        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        if (Utilities.IsValid(owner)) knownOwnerId = owner.playerId;
    }

    private void Update()
    {
        if (!Utilities.IsValid(Networking.LocalPlayer) || !Networking.IsNetworkSettled) return;
        if (!hasState && Networking.IsOwner(gameObject) && !initializing && !awaitingGrant)
        {
            initializing = true;
            if (string.IsNullOrEmpty(snapshot)) snapshot = codec.Encode();
            ReceiveSnapshot();
            if (hasState)
            {
                pendingSnapshot = codec.Encode(); pendingGrant = Networking.LocalPlayer.playerId;
                pendingGrantRevision = state.revision; pendingToken = grantToken + 1; dirty = true;
            }
            initializing = false;
        }
        if (Networking.IsOwner(gameObject) && dirty && Time.time >= nextSendTime && !Networking.IsClogged)
        {
            nextSendTime = Time.time + 1f;
            RequestSerialization();
        }
    }

    public bool CanEdit()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        return Utilities.IsValid(local) && Networking.IsNetworkSettled && hasState &&
            Networking.IsOwner(gameObject) && acceptedGrant == local.playerId && !awaitingGrant &&
            (pendingGrant < 0 || pendingGrant == local.playerId) &&
            pickup.IsHeld && Utilities.IsValid(pickup.currentPlayer) && pickup.currentPlayer.isLocal &&
            Networking.IsOwner(pickup.gameObject);
    }

    public bool Publish(string json)
    {
        if (!CanEdit() || string.IsNullOrEmpty(json)) return false;
        pendingSnapshot = json; pendingGrant = Networking.LocalPlayer.playerId;
        pendingGrantRevision = state.revision; dirty = true; syncError = "";
        return true;
    }

    public void AskForControl()
    {
        if (!LocallyHeld()) return;
        if (!Networking.IsOwner(gameObject) && hasState &&
            acceptedGrant == Networking.LocalPlayer.playerId && acceptedToken == grantToken &&
            state.revision == grantedRevision)
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(AcknowledgeGrant), grantedRevision, grantToken);
        // Re-request as well: an old grant may no longer be the owner's current offer.
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestControl));
    }

    private bool LocallyHeld()
    {
        return Utilities.IsValid(Networking.LocalPlayer) && pickup.IsHeld &&
            Utilities.IsValid(pickup.currentPlayer) && pickup.currentPlayer.isLocal &&
            Networking.IsOwner(pickup.gameObject);
    }

    [NetworkCallable]
    public void RequestControl()
    {
        if (!Networking.IsOwner(gameObject) || !hasState || dirty) return;
        VRCPlayerApi holder = NetworkCalling.CallingPlayer;
        if (!Utilities.IsValid(holder)) return;
        // The pickup's network owner identifies the requester on remote clients.
        // IsHeld/currentPlayer are only used on the requesting local client.
        if (!Networking.IsOwner(holder, pickup.gameObject)) return;
        if (awaitingGrant && Utilities.IsValid(VRCPlayerApi.GetPlayerById(previousOwnerId))) return;
        if (pendingGrant == holder.playerId && pendingGrantRevision == state.revision &&
            lastSentToken == pendingToken) return;
        pendingSnapshot = codec.Encode();
        if (string.IsNullOrEmpty(pendingSnapshot)) return;
        pendingGrant = holder.playerId; pendingGrantRevision = state.revision;
        pendingToken = grantToken + 1;
        dirty = true; nextSendTime = 0f;
    }

    // Kept separate so reordered/replayed ACKs can be regression tested without a network.
    public bool MatchesAcknowledgement(int playerId, int revision, int token)
    {
        return hasState && !dirty && playerId == pendingGrant && playerId == lastSentGrant &&
            revision == state.revision && revision == pendingGrantRevision && revision == lastSentRevision &&
            token == pendingToken && token == lastSentToken;
    }

    [NetworkCallable]
    public void AcknowledgeGrant(int revision, int token)
    {
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!Networking.IsOwner(gameObject) || !Utilities.IsValid(caller) || caller.isLocal ||
            !Networking.IsOwner(caller, pickup.gameObject)) return;
        if (MatchesAcknowledgement(caller.playerId, revision, token)) Networking.SetOwner(caller, gameObject);
    }

    public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)
    {
        if (!Utilities.IsValid(requester) || !Utilities.IsValid(newOwner) ||
            !Networking.IsOwner(requester, gameObject) || !Networking.IsOwner(newOwner, pickup.gameObject)) return false;
        if (Networking.IsOwner(gameObject))
            return MatchesAcknowledgement(newOwner.playerId, grantedRevision, grantToken);
        return hasState && acceptedGrant == newOwner.playerId && acceptedToken == grantToken &&
            state.revision == grantedRevision;
    }

    public override void OnPreSerialization()
    {
        if (!hasState || string.IsNullOrEmpty(pendingSnapshot)) return;
        snapshot = pendingSnapshot; grantedPlayerId = pendingGrant; grantedRevision = pendingGrantRevision; grantToken = pendingToken;
        sendingSnapshot = snapshot; sendingRevision = grantedRevision; sendingGrant = grantedPlayerId; sendingToken = grantToken;
    }

    public override void OnPostSerialization(SerializationResult result)
    {
        lastByteCount = result.byteCount;
        if (!Networking.IsOwner(gameObject)) return;
        if (!result.success)
        {
            syncError = "Send failed - retrying"; dirty = true; nextSendTime = Time.time + 2f; return;
        }
        lastSentRevision = sendingRevision; lastSentGrant = sendingGrant; lastSentToken = sendingToken;
        sharedSnapshot = sendingSnapshot;
        // Completion of an older send must not clear a newer edit.
        dirty = sendingSnapshot != pendingSnapshot || sendingGrant != pendingGrant || sendingToken != pendingToken;
        syncError = "";
        VRCPlayerApi local = Networking.LocalPlayer;
        if (Utilities.IsValid(local) && sendingGrant == local.playerId && !dirty)
        {
            acceptedGrant = local.playerId; acceptedToken = sendingToken; awaitingGrant = false;
        }
        // Sending successfully does not prove the receiver applied the snapshot.
        // Ownership is transferred only by AcknowledgeGrant after that application.
    }

    public override void OnDeserialization() { ReceiveSnapshot(); }

    public void ReceiveSnapshot()
    {
        if (!codec.TryDecode(snapshot)) { syncError = codec.error; return; }
        if (hasState && codec.decodedRevision < state.revision) return;
        bool changed = !hasState || codec.decodedRevision > state.revision;
        if (!changed && !codec.DecodedMatchesState()) { syncError = "Conflicting circuit revision"; return; }
        if (changed) codec.ApplyDecoded();
        sharedSnapshot = snapshot;
        hasState = true;
        if (state.revision == grantedRevision)
        { acceptedGrant = grantedPlayerId; acceptedToken = grantToken; }
        if (Utilities.IsValid(Networking.LocalPlayer) && acceptedGrant == Networking.LocalPlayer.playerId) awaitingGrant = false;
        syncError = "";
        if (changed) controller.CircuitChanged();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        previousOwnerId = knownOwnerId;
        knownOwnerId = Utilities.IsValid(player) ? player.playerId : -1;
        if (dirty && Utilities.IsValid(player) && !player.isLocal && !string.IsNullOrEmpty(sharedSnapshot) && codec.TryDecode(sharedSnapshot))
        {
            codec.ApplyDecoded(); controller.CircuitChanged();
        }
        dirty = false; pendingSnapshot = null;
        if (Utilities.IsValid(player) && player.isLocal)
        {
            awaitingGrant = acceptedGrant != player.playerId;
            // For a normal handoff, wait for the matching grant to deserialize.
            // If the previous owner left, RequestControl can publish a new
            // grant from the last shared state after the network has settled.
            pendingSnapshot = hasState ? codec.Encode() : snapshot;
            pendingGrant = grantedPlayerId; pendingGrantRevision = grantedRevision; pendingToken = grantToken;
            lastSentRevision = -1; lastSentGrant = -1; lastSentToken = -1;
        }
        else { acceptedGrant = -1; acceptedToken = -1; awaitingGrant = false; }
        controller.CancelInteraction();
    }

    public string StatusText()
    {
        if (!string.IsNullOrEmpty(syncError)) return syncError;
        if (!hasState) return "Waiting for circuit sync";
        if (dirty) return "Sending revision " + state.revision;
        return "Revision " + state.revision;
    }
}
