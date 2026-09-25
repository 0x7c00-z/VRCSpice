using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardLayout : UdonSharpBehaviour
{
    public string layoutId = "breadboard-fbx-400";
    public int layoutVersion = 1;
    public string[] holeIds;
    public Vector3[] holePositions;
    public int[] conductorGroups;
    public int groundHole = 325;
    public int positiveHole = 300;
    public float supplyVoltage = 5f;
    public float pitch = 0.01f;
    public float maximumHandHeight = 0.10f;
    private DataDictionary lookup;

    public void Initialize()
    {
        if (lookup != null) return;
        lookup = new DataDictionary();
        for (int i = 0; i < holeIds.Length; i++) lookup.Add(holeIds[i], i);
    }

    public int HoleIndex(string id)
    {
        Initialize();
        DataToken token;
        if (!lookup.TryGetValue(id, out token)) return -1;
        return token.Int;
    }

    public bool InPlacementRange(Vector3 local)
    {
        return local.x >= -0.325f && local.x <= 0.005f &&
               local.z >= -0.225f && local.z <= 0.005f &&
               local.y >= -0.004f && local.y <= maximumHandHeight;
    }

    public int NearestHole(Vector3 local, int previous)
    {
        if (!InPlacementRange(local)) return -1;
        int best = -1;
        float distance = float.MaxValue;
        for (int i = 0; i < holePositions.Length; i++)
        {
            Vector3 d = holePositions[i] - local;
            float sq = d.x * d.x + d.z * d.z;
            if (sq < distance) { distance = sq; best = i; }
        }
        if (previous >= 0 && previous < holePositions.Length)
        {
            Vector3 d = holePositions[previous] - local;
            if (d.x * d.x + d.z * d.z <= distance + 0.000004f) return previous;
        }
        return best;
    }

    public int HoleAt(Vector3 local)
    {
        // Geometry lookup only: no Collider, raycast or scene-object lookup.
        for (int i = 0; i < holePositions.Length; i++)
        {
            Vector3 d = holePositions[i] - local;
            if (d.x * d.x + d.z * d.z < 0.00000025f) return i;
        }
        return -1;
    }

    public Vector3 Direction(int orientation)
    {
        if (orientation == 0) return Vector3.right;
        if (orientation == 1) return Vector3.forward;
        if (orientation == 2) return Vector3.left;
        return Vector3.back;
    }
}
