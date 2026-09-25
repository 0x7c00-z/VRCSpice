using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardController : UdonSharpBehaviour
{
    public Transform boardSpace;
    public VRCPickup pickup;
    public BreadboardState state;
    public BreadboardLayout layout;
    public BreadboardCatalog catalog;
    public BreadboardCodec codec;
    public BreadboardSync circuitSync;
    public BreadboardRenderer boardRenderer;
    public BreadboardPalette palette;
    public BreadboardMnaAdapter adapter;
    public BreadboardProbes probes;
    public Vector3 handPointerOffset = new Vector3(0f, 0f, 0.025f);
    public Vector3 handRotationAxis = Vector3.right;
    [HideInInspector] public int candidateHole = -1;
    [HideInInspector] public int wireStart = -1;
    private bool leftUse, rightUse, armed;
    private int holdingHand;
    private bool pointerValid;
    private Vector3 pointerLocal;
    private Vector3 handDirection;
    private bool hasHandDirection;
    private float nextControlRequest, feedbackUntil;
    private string feedback = "";
    private int valueAxisDirection;
    private bool valueAxisNeutral = true;
    private float nextValueRepeat;

    public void ResetValueAxis() { valueAxisDirection = 0; valueAxisNeutral = false; }

    public override void InputLookVertical(float value, UdonInputEventArgs args)
    {
        // Do not turn desktop mouse-look or the left stick into value edits.
        if (args.handType != HandType.RIGHT || !Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsUserInVR()) return;
        ValueAxis(value);
    }
    public void ValueAxis(float value)
    {
        if (Mathf.Abs(value) < .25f) { valueAxisDirection = 0; valueAxisNeutral = true; return; }
        if (!valueAxisNeutral || Mathf.Abs(value) < .65f || CurrentHoldingHand() == 0 || !circuitSync.CanEdit() || !palette.CanStepValue()) return;
        int direction = value > 0f ? 1 : -1;
        if (direction == valueAxisDirection) return;
        valueAxisDirection = direction; palette.StepValue(direction); nextValueRepeat = Time.time + .45f;
    }

    private void Start()
    {
        state.Initialize(); boardRenderer.Rebuild(); palette.Refresh();
    }

    private int CurrentHoldingHand()
    {
        if (!pickup.IsHeld || !Utilities.IsValid(pickup.currentPlayer) || !pickup.currentPlayer.isLocal) return 0;
        return pickup.currentHand == VRC_Pickup.PickupHand.Left ? 1 : 2;
    }

    private bool ReadPointer()
    {
        hasHandDirection = false;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local)) return false;
        if (local.IsUserInVR())
        {
            VRCPlayerApi.TrackingData pose = local.GetTrackingData(holdingHand == 1 ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand);
            if (pose.position == Vector3.zero) return false;
            pointerLocal = boardSpace.InverseTransformPoint(pose.position + pose.rotation * handPointerOffset);
            handDirection = boardSpace.InverseTransformDirection(pose.rotation * handRotationAxis);
            hasHandDirection = true;
            return true;
        }
        // Desktop/ClientSim aid: analytic head-ray/board-plane intersection.
        // This does not use Physics.Raycast or any component collider.
        VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 origin = boardSpace.InverseTransformPoint(head.position);
        Vector3 direction = boardSpace.InverseTransformDirection(head.rotation * Vector3.forward);
        if (Mathf.Abs(direction.y) < 0.0001f) return false;
        float distance = -origin.y / direction.y;
        if (distance <= 0f || distance > 3f) return false;
        pointerLocal = origin + direction * distance;
        return true;
    }

    private void Update()
    {
        int hand = CurrentHoldingHand();
        if (holdingHand != hand)
        {
            holdingHand = hand; candidateHole = -1; wireStart = -1; palette.selectedId = 0; palette.page = 0; palette.Refresh();
            ResetValueAxis();
            armed = hand != 0 && !(hand == 1 ? rightUse : leftUse);
        }
        if (hand == 0)
        {
            ResetValueAxis();
            CancelVisuals();
            palette.SetStatus("Pick up board / use other hand | " + circuitSync.StatusText());
            return;
        }
        if (!circuitSync.CanEdit())
        {
            ResetValueAxis();
            wireStart = -1;
            CancelVisuals();
            if (Time.time >= nextControlRequest)
            { nextControlRequest = Time.time + 1f; circuitSync.AskForControl(); }
            palette.SetStatus("Waiting for edit control | " + circuitSync.StatusText());
            return;
        }
        if (valueAxisDirection != 0 && palette.CanStepValue() && Time.time >= nextValueRepeat)
        { palette.StepValue(valueAxisDirection); nextValueRepeat = Time.time + .15f; }
        pointerValid = ReadPointer();
        if (!pointerValid) { CancelVisuals(); wireStart = -1; armed = false; return; }
        bool panel = palette.InPanel(pointerLocal);
        palette.Hover(panel ? palette.HitButton(pointerLocal) : -1);
        if (panel)
        {
            boardRenderer.HidePreview(); candidateHole = -1;
            boardRenderer.ShowPointer(pointerLocal, true);
            palette.SetStatus(Time.time < feedbackUntil ? feedback : "Select setting | " + circuitSync.StatusText());
            return;
        }
        if (palette.page != 0)
        {
            CancelVisuals(); palette.SetStatus("Finish or close the menu before placing"); return;
        }
        if (hasHandDirection && palette.kind != 0 && palette.mode == 0)
        {
            int orientation = state.placement.HandOrientation(handDirection,palette.orientation);
            if (orientation != palette.orientation) { palette.orientation = orientation; palette.Refresh(); }
        }
        candidateHole = layout.NearestHole(pointerLocal, candidateHole);
        boardRenderer.ShowPointer(candidateHole < 0 ? pointerLocal : layout.holePositions[candidateHole], candidateHole >= 0);
        string hint = "Move free hand above board";
        if (candidateHole >= 0)
        {
            hint = layout.holeIds[candidateHole];
            if (palette.mode == 0)
            {
                if (palette.kind == 0)
                {
                    if (wireStart < 0) { boardRenderer.HidePreview(); hint += " | Trigger: wire start"; }
                    else if (state.placement.WireBetween(wireStart,candidateHole))
                    {
                        palette.orientation = state.placement.wireOrientation; palette.wireLength = state.placement.wireLength;
                        bool valid = state.CanPlace(0,wireStart,palette.orientation,palette.wireLength,-1) && state.count < state.capacity;
                        boardRenderer.Preview(0,wireStart,palette.orientation,palette.wireLength,0,-1,valid);
                        hint += " | " + palette.wireLength + " pitches | " + (valid ? "Trigger: wire end" : state.error);
                    }
                    else { boardRenderer.HidePreview(); hint += " | Choose an aligned end hole"; }
                }
                else
                {
                    bool valid = state.CanPlace(palette.kind, candidateHole, palette.orientation, palette.wireLength, -1) && state.count < state.capacity;
                    boardRenderer.Preview(palette.kind, candidateHole, palette.orientation, palette.wireLength, palette.value, palette.model, valid);
                    hint += valid ? " | Trigger: place" : " | " + (state.count >= state.capacity ? "Board is full" : state.error);
                }
            }
            else if (palette.mode >= 3)
            {
                boardRenderer.HidePreview(); hint += " | Trigger: " + (palette.mode == 3 ? "CH1" : "CH2");
            }
            else
            {
                boardRenderer.HidePreview();
                int slot = state.occupants[candidateHole];
                hint += slot >= 0 ? " | #" + state.ids[slot] + (palette.mode == 1 ? " Trigger: erase" : " Trigger: select") : " | Select an end pin";
            }
        }
        else boardRenderer.HidePreview();
        palette.SetStatus(Time.time < feedbackUntil ? feedback : hint + " | " + circuitSync.StatusText());
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        bool left = args.handType == HandType.LEFT;
        bool previous = left ? leftUse : rightUse;
        if (left) leftUse = value; else rightUse = value;
        VRCPlayerApi local = Networking.LocalPlayer;
        bool desktop = Utilities.IsValid(local) && !local.IsUserInVR();
        bool editingHand = desktop ? !left : (holdingHand == 1 ? !left : left);
        if (!editingHand || holdingHand == 0) return;
        if (!value) { armed = true; return; }
        if (previous || !armed) return;
        armed = false;
        if (!circuitSync.CanEdit() || !pointerValid) return;
        if (palette.InPanel(pointerLocal)) { palette.Click(palette.HitButton(pointerLocal)); return; }
        if (palette.page != 0) return;
        if (candidateHole < 0) return;
        if (palette.mode >= 3)
        {
            probes.Select(palette.mode-3,candidateHole); palette.Refresh();
            ShowFeedback(probes.Label(palette.mode-3)); return;
        }
        // Commit exactly the previewed anchor, not a newly sampled unseen hole.
        if (palette.mode == 2) { palette.LoadSlot(state.occupants[candidateHole]); return; }
        int anchor = candidateHole;
        if (palette.mode == 0 && palette.kind == 0)
        {
            if (wireStart < 0)
            {
                if (state.occupants[candidateHole] >= 0) { ShowFeedback("Hole occupied"); return; }
                wireStart = candidateHole; ShowFeedback("Choose wire end; Cancel wire to restart"); return;
            }
            if (!state.placement.WireBetween(wireStart,candidateHole)) { ShowFeedback("Choose an aligned end hole"); return; }
            anchor = wireStart; palette.orientation = state.placement.wireOrientation; palette.wireLength = state.placement.wireLength;
        }
        string before = codec.Encode();
        bool changed = palette.mode == 1 ? state.Remove(state.occupants[candidateHole]) :
            state.Add(palette.kind, anchor, palette.orientation, palette.wireLength, palette.value, palette.model);
        FinishEdit(before, changed);
        if (changed) wireStart = -1;
    }

    public void EditSelected(bool geometry)
    {
        if (!circuitSync.CanEdit()) return;
        int slot = state.FindId(palette.selectedId);
        if (slot < 0) return;
        string before = codec.Encode();
        bool changed = geometry ? state.ChangeGeometry(slot, palette.orientation, palette.wireLength) : state.ChangeParameter(slot, palette.value, palette.model);
        FinishEdit(before, changed);
        palette.LoadSlot(state.FindId(palette.selectedId));
    }

    public void EditSupply(float voltage)
    {
        if (!circuitSync.CanEdit()) { ShowFeedback("Waiting for edit control"); return; }
        string before = codec.Encode();
        FinishEdit(before,state.ChangeSupply(voltage));
    }
    public void CancelWire() { wireStart = -1; boardRenderer.HidePreview(); }

    private void FinishEdit(string before, bool changed)
    {
        if (!changed) { ShowFeedback(string.IsNullOrEmpty(state.error) ? "No change" : state.error); return; }
        string after = codec.Encode();
        if (string.IsNullOrEmpty(after) || !circuitSync.Publish(after))
        {
            string reason = string.IsNullOrEmpty(codec.error) ? "Edit control changed" : codec.error;
            if (codec.TryDecode(before)) codec.ApplyDecoded();
            ShowFeedback(reason); return;
        }
        CircuitChanged();
    }

    public void CircuitChanged()
    {
        state.Initialize(); boardRenderer.Rebuild(); adapter.QueueApply();
        if (probes != null) probes.Resolve();
        if (palette.selectedId > 0 && state.FindId(palette.selectedId) < 0) palette.selectedId = 0;
        palette.Refresh();
    }

    public void ShowFeedback(string message) { feedback = message; feedbackUntil = Time.time + 2f; }
    private void CancelVisuals()
    {
        candidateHole = -1; pointerValid = false; boardRenderer.HidePreview();
        boardRenderer.ShowPointer(Vector3.zero, false); palette.Hover(-1);
    }
    public void CancelInteraction() { ResetValueAxis(); armed = false; wireStart = -1; palette.selectedId = 0; palette.page = 0; CancelVisuals(); palette.Refresh(); }
}
