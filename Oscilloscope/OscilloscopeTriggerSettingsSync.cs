using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class OscilloscopeTriggerSettingsSync : UdonSharpBehaviour
{
    [SerializeField] public OscilloscopeController controller;

    [UdonSynced, HideInInspector] public bool triggerEnabled;
    [UdonSynced, HideInInspector] public int triggerMode;
    [UdonSynced, HideInInspector] public int triggerSource;
    [UdonSynced, HideInInspector] public bool triggerFalling;
    [UdonSynced, HideInInspector] public float triggerLevel;
    [UdonSynced, HideInInspector] public int timeDivisionIndex = 2;
    [UdonSynced, HideInInspector] public float ch1VoltageDivision = 5f;
    [UdonSynced, HideInInspector] public float ch2VoltageDivision = 5f;
    [UdonSynced, HideInInspector] public int settingsRevision;

    private bool dirty;
    private bool serializationPending;
    private int sendingRevision;
    private float nextSendTime;

    private void Start()
    {
        ApplyToController();
    }

    public void InitializeController()
    {
        ApplyToController();
    }

    public void SubmitSettings(bool enabled, int mode, int source, bool falling, float level)
    {
        if (float.IsNaN(level) || float.IsInfinity(level))
        {
            ApplyToController();
            return;
        }
        int nextMode = Mathf.Clamp(mode, 0, 2);
        int nextSource = Mathf.Clamp(source, 0, 1);
        VRCPlayerApi local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local))
        {
            StoreSettings(enabled, nextMode, nextSource, falling, level);
            ApplyToController();
            return;
        }
        if (!Networking.IsNetworkSettled)
        {
            ApplyToController();
            return;
        }
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
        if (!Networking.IsOwner(gameObject))
        {
            ApplyToController();
            return;
        }

        if (!StoreSettings(enabled, nextMode, nextSource, falling, level))
        {
            ApplyToController();
            return;
        }
        settingsRevision = NextRevision(settingsRevision);
        ApplyToController();
        dirty = true;
        nextSendTime = 0f;
        TrySerialize();
    }

    public void SubmitViewSettings(int timeIndex, float ch1Div, float ch2Div)
    {
        int nextTimeIndex = Mathf.Clamp(timeIndex, 0, 4);
        if (!IsValidVoltageDivision(ch1Div) || !IsValidVoltageDivision(ch2Div))
        {
            ApplyToController();
            return;
        }
        VRCPlayerApi local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local))
        {
            StoreViewSettings(nextTimeIndex, ch1Div, ch2Div);
            ApplyToController();
            return;
        }
        if (!Networking.IsNetworkSettled)
        {
            ApplyToController();
            return;
        }
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
        if (!Networking.IsOwner(gameObject))
        {
            ApplyToController();
            return;
        }

        if (!StoreViewSettings(nextTimeIndex, ch1Div, ch2Div))
        {
            ApplyToController();
            return;
        }
        settingsRevision = NextRevision(settingsRevision);
        ApplyToController();
        dirty = true;
        nextSendTime = 0f;
        TrySerialize();
    }

    private bool StoreSettings(bool enabled, int mode, int source, bool falling, float level)
    {
        bool changed = triggerEnabled != enabled || triggerMode != mode ||
            triggerSource != source || triggerFalling != falling || triggerLevel != level;
        triggerEnabled = enabled;
        triggerMode = mode;
        triggerSource = source;
        triggerFalling = falling;
        triggerLevel = level;
        return changed;
    }

    private bool StoreViewSettings(int timeIndex, float ch1Div, float ch2Div)
    {
        bool changed = timeDivisionIndex != timeIndex || ch1VoltageDivision != ch1Div ||
            ch2VoltageDivision != ch2Div;
        timeDivisionIndex = timeIndex;
        ch1VoltageDivision = ch1Div;
        ch2VoltageDivision = ch2Div;
        return changed;
    }

    private bool IsValidVoltageDivision(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private int NextRevision(int value)
    {
        return value >= int.MaxValue ? 1 : value + 1;
    }

    private void ApplyToController()
    {
        triggerMode = Mathf.Clamp(triggerMode, 0, 2);
        triggerSource = Mathf.Clamp(triggerSource, 0, 1);
        if (float.IsNaN(triggerLevel) || float.IsInfinity(triggerLevel)) triggerLevel = 0f;
        timeDivisionIndex = Mathf.Clamp(timeDivisionIndex, 0, 4);
        if (!IsValidVoltageDivision(ch1VoltageDivision)) ch1VoltageDivision = 5f;
        if (!IsValidVoltageDivision(ch2VoltageDivision)) ch2VoltageDivision = 5f;
        if (controller != null)
        {
            controller.ApplySharedTriggerSettings(triggerEnabled, triggerMode, triggerSource,
                triggerFalling, triggerLevel);
            controller.ApplySharedViewSettings(timeDivisionIndex, ch1VoltageDivision,
                ch2VoltageDivision);
        }
    }

    private void Update()
    {
        if (dirty) TrySerialize();
    }

    private void TrySerialize()
    {
        if (!dirty || serializationPending || Time.time < nextSendTime ||
            !Networking.IsOwner(gameObject)) return;
        if (Networking.IsClogged)
        {
            nextSendTime = Time.time + 0.25f;
            return;
        }
        serializationPending = true;
        RequestSerialization();
    }

    public override void OnPreSerialization()
    {
        sendingRevision = settingsRevision;
    }

    public override void OnPostSerialization(SerializationResult result)
    {
        serializationPending = false;
        if (!Networking.IsOwner(gameObject)) return;
        if (result.success && sendingRevision == settingsRevision) dirty = false;
        else
        {
            dirty = true;
            nextSendTime = Time.time + 1f;
        }
    }

    public override void OnDeserialization()
    {
        ApplyToController();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        serializationPending = false;
        if (!Networking.IsOwner(gameObject)) dirty = false;
        ApplyToController();
    }
}
