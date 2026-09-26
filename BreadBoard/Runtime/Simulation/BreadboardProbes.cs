using UdonSharp;
using UnityEngine;
using TMPro;

// Observation display; BreadboardSync owns the shared hole IDs and their revision.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardProbes : UdonSharpBehaviour
{
    public BreadboardLayout layout;
    public BreadboardState state;
    public BreadboardConnectivity connectivity;
    public BreadboardMnaAdapter adapter;
    public TMP_InputField channel1Input, channel2Input;
    public Transform marker1, marker2;
    [HideInInspector] public string channel1Hole = "", channel2Hole = "";

    public bool ValidHoles(string first, string second)
    {
        return first != null && second != null &&
            (first == "" || layout.HoleIndex(first) >= 0) &&
            (second == "" || layout.HoleIndex(second) >= 0);
    }
    public bool ApplyShared(string first, string second)
    {
        if (!ValidHoles(first, second)) return false;
        bool clearFirst = first == "";
        bool clearSecond = second == "";
        channel1Hole = first; channel2Hole = second;
        if (clearFirst && channel1Input != null) channel1Input.text = "";
        if (clearSecond && channel2Input != null) channel2Input.text = "";
        Resolve(); return true;
    }
    public bool Select(int channel, int hole)
    {
        if (channel < 0 || channel > 1 || hole < 0 || hole >= layout.holeIds.Length) return false;
        if (channel == 0) channel1Hole = layout.holeIds[hole];
        else channel2Hole = layout.holeIds[hole];
        Resolve(); return true;
    }
    public void Clear(int channel)
    {
        if (channel == 0) { channel1Hole = ""; if (channel1Input != null) channel1Input.text = ""; }
        else if (channel == 1) { channel2Hole = ""; if (channel2Input != null) channel2Input.text = ""; }
        Resolve();
    }
    public string NodeForHole(string holeId)
    {
        if (string.IsNullOrEmpty(holeId)) return "";
        int hole = layout.HoleIndex(holeId);
        if (hole < 0 || connectivity.nodeNames == null || hole >= connectivity.nodeNames.Length) return "";
        return connectivity.nodeNames[hole];
    }
    public void Resolve()
    {
        ResolveChannel(0,channel1Hole,marker1,channel1Input);
        ResolveChannel(1,channel2Hole,marker2,channel2Input);
    }
    private void ResolveChannel(int channel, string holeId, Transform marker, TMP_InputField input)
    {
        int hole = string.IsNullOrEmpty(holeId) ? -1 : layout.HoleIndex(holeId);
        if (marker != null)
        {
            marker.gameObject.SetActive(hole >= 0);
            if (hole >= 0) marker.localPosition = layout.holePositions[hole] + new Vector3(channel == 0 ? -.004f : .004f,.018f,0);
        }
        if (string.IsNullOrEmpty(holeId) || input == null) return;
        // Clear a stale node while a new MNA input is queued. The adapter
        // resolves again after publishing the new netlist to MNAGen.
        string node = adapter.appliedRevision == state.revision ? NodeForHole(holeId) : "";
        if (input.text != node) input.text = node;
    }
    public string Label(int channel)
    {
        string hole = channel == 0 ? channel1Hole : channel2Hole;
        return (channel == 0 ? "CH1: " : "CH2: ") + (string.IsNullOrEmpty(hole) ? "--" : hole);
    }
}
