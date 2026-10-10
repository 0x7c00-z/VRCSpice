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
    [HideInInspector] public int appliedCircuitRevision = -1;
    [HideInInspector] public int appliedSolverSettingsRevision = -1;
    [HideInInspector] public int appliedSolverHistoryRevision = -1;
    [HideInInspector] public int appliedSolverRestartRevision = -1;
    private bool queued;

    public DataList BuildNetlist()
    {
        connectivity.Rebuild();
        DataList result = new DataList();
        DataList supply = new DataList(); DataList supplyNodes = new DataList(); DataList supplyValue = new DataList();
        supplyNodes.Add(connectivity.nodeNames[layout.positiveHole]);
        supplyNodes.Add(connectivity.nodeNames[layout.groundHole]);
        supplyValue.Add(state.supplyVoltage);
        supply.Add("V_board_main"); supply.Add(supplyNodes); supply.Add(supplyValue); result.Add(supply);
        for (int i = 0; i < state.count; i++)
        {
            int kind = state.kinds[i]; if (kind == 0) continue;
            DataList record = new DataList(); DataList nodes = new DataList();
            nodes.Add(connectivity.nodeNames[state.pin0[i]]); nodes.Add(connectivity.nodeNames[state.pin1[i]]);
            if (kind == 5) nodes.Add(connectivity.nodeNames[state.pin2[i]]);
            record.Add((kind == 6 ? "R" : kind == 7 ? "D" : catalog.kinds[kind]) + "_" + catalog.ComponentId(state.ids[i]));
            record.Add(nodes); record.Add(catalog.MnaConstants(kind, state.values[i], state.models[i])); result.Add(record);
        }
        generatedNetlist = result; return result;
    }

    public void QueueApply()
    {
        if (queued) return;
        queued = true;
        SendCustomEventDelayedFrames(nameof(ApplyLatest), 2);
    }

    public void QueueSettingsApply() { QueueApply(); }
    public void QueueRestartApply() { QueueApply(); }

    public void ApplyLatest()
    {
        queued = false;
        if (generator == null) return;
        state.Initialize();
        MNASolve solver = generator.GetComponent<MNASolve>();
        if (solver != null && (appliedSolverSettingsRevision != state.solverSettingsRevision ||
            appliedSolverHistoryRevision != state.solverHistoryRevision))
        {
            solver.ApplySynchronizedSettings(state.integrationScheme, state.maxDeltaTime, state.maxNewtonIterations,
                state.solverSettingsRevision, state.solverHistoryRevision);
            appliedSolverSettingsRevision = state.solverSettingsRevision;
            appliedSolverHistoryRevision = state.solverHistoryRevision;
        }
        if (solver != null && appliedSolverRestartRevision != state.solverRestartRevision)
        {
            solver.ApplySynchronizedRestart(state.solverRestartRevision);
            appliedSolverRestartRevision = state.solverRestartRevision;
        }

        if (appliedCircuitRevision != state.circuitRevision)
        {
            DataList next = BuildNetlist();
            if (generator.preprocessedNetlist == null) generator.preprocessedNetlist = new DataList();
            generator.netlist = next; generator.UpdateMNA();
            appliedCircuitRevision = state.circuitRevision;
            BreadboardRenderer views = GetComponent<BreadboardRenderer>();
            if (views != null) views.BindSolver(solver);
        }
        appliedRevision = state.revision;
        if (probes != null) probes.Resolve();
    }
}
