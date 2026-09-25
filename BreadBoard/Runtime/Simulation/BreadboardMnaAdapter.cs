using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardMnaAdapter : UdonSharpBehaviour
{
    public BreadboardLayout layout;
    public BreadboardState state;
    public BreadboardCatalog catalog;
    public BreadboardConnectivity connectivity;
    public MNAGen generator;
    public BreadboardProbes probes;
    [HideInInspector] public DataList generatedNetlist;
    [HideInInspector] public int appliedRevision = -1;
    private bool queued;

    public DataList BuildNetlist()
    {
        connectivity.Rebuild();
        DataList result = new DataList();
        DataList supply = new DataList();
        DataList supplyNodes = new DataList();
        DataList supplyValue = new DataList();
        supplyNodes.Add(connectivity.nodeNames[layout.positiveHole]);
        supplyNodes.Add(connectivity.nodeNames[layout.groundHole]);
        supplyValue.Add(state.supplyVoltage);
        supply.Add("V_board_main"); supply.Add(supplyNodes); supply.Add(supplyValue); result.Add(supply);
        for (int i = 0; i < state.count; i++)
        {
            int kind = state.kinds[i];
            if (kind == 0) continue;
            DataList record = new DataList();
            DataList nodes = new DataList();
            nodes.Add(connectivity.nodeNames[state.pin0[i]]); nodes.Add(connectivity.nodeNames[state.pin1[i]]);
            if (kind == 5) nodes.Add(connectivity.nodeNames[state.pin2[i]]);
            record.Add(catalog.kinds[kind] + "_" + catalog.ComponentId(state.ids[i]));
            record.Add(nodes); record.Add(catalog.MnaConstants(kind, state.values[i], state.models[i]));
            result.Add(record);
        }
        generatedNetlist = result;
        return result;
    }

    public void QueueApply()
    {
        if (queued) return;
        queued = true;
        // Let MNASolve.Start finish. Never manipulate its time/history/buffers.
        SendCustomEventDelayedFrames(nameof(ApplyLatest), 2);
    }

    public void ApplyLatest()
    {
        queued = false;
        if (generator == null || appliedRevision == state.revision) return;
        DataList next = BuildNetlist();
        if (generator.preprocessedNetlist == null) generator.preprocessedNetlist = new DataList();
        generator.netlist = next;
        generator.UpdateMNA();
        appliedRevision = state.revision;
        if (probes != null) probes.Resolve();
    }
}
