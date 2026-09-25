using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardConnectivity : UdonSharpBehaviour
{
    public BreadboardLayout layout;
    public BreadboardState state;
    [HideInInspector] public string[] nodeNames;
    private int[] parent;

    private int Find(int hole)
    {
        while (parent[hole] != hole)
        {
            parent[hole] = parent[parent[hole]];
            hole = parent[hole];
        }
        return hole;
    }

    private void Join(int a, int b)
    {
        a = Find(a); b = Find(b);
        if (a < b) parent[b] = a;
        else parent[a] = b;
    }

    public void Rebuild()
    {
        state.Initialize();
        int size = layout.holeIds.Length;
        parent = new int[size]; nodeNames = new string[size];
        DataDictionary groups = new DataDictionary();
        for (int i = 0; i < size; i++) parent[i] = i;
        for (int i = 0; i < size; i++)
        {
            int group = layout.conductorGroups[i];
            if (group < 0) continue;
            DataToken first;
            if (groups.TryGetValue(group, out first)) Join(i, first.Int);
            else groups.Add(group, i);
        }
        for (int i = 0; i < state.count; i++)
            if (state.kinds[i] == 0) Join(state.pin0[i], state.pin1[i]);
        int ground = Find(layout.groundHole);
        for (int i = 0; i < size; i++)
        {
            int root = Find(i);
            nodeNames[i] = root == ground ? "GND" : "N_" + layout.holeIds[root].Replace(":", "_");
        }
    }
}
