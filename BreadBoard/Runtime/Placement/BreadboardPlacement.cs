using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardPlacement : UdonSharpBehaviour
{
    public BreadboardLayout layout;
    public BreadboardCatalog catalog;
    public int pin0 = -1;
    public int pin1 = -1;
    public int pin2 = -1;
    public int wireOrientation, wireLength;

    public bool WireBetween(int start, int end)
    {
        if (start < 0 || end < 0 || start >= layout.holePositions.Length || end >= layout.holePositions.Length || start == end) return false;
        Vector3 delta = layout.holePositions[end] - layout.holePositions[start];
        if (Mathf.Abs(delta.z) < .0005f) wireOrientation = delta.x > 0 ? 0 : 2;
        else if (Mathf.Abs(delta.x) < .0005f) wireOrientation = delta.z > 0 ? 1 : 3;
        else return false;
        wireLength = Mathf.RoundToInt(delta.magnitude / layout.pitch);
        return wireLength >= 1 && wireLength <= 30 && Resolve(0,start,wireOrientation,wireLength) && pin1 == end;
    }

    public int HandOrientation(Vector3 direction, int previous)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < .04f) return previous;
        direction.Normalize();
        int nearest = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z) ? (direction.x >= 0 ? 0 : 2) : (direction.z >= 0 ? 1 : 3);
        // Hysteresis prevents a hand resting near 45 degrees from chattering.
        return Vector3.Dot(direction,layout.Direction(nearest)) - Vector3.Dot(direction,layout.Direction(previous)) > .15f ? nearest : previous;
    }

    public Vector3 PinPosition(int kind, int anchor, int orientation, int length, int terminal)
    {
        int offset = terminal == 0 ? 0 : (kind == 5 ? terminal : catalog.Span(kind, length));
        return layout.holePositions[anchor] + layout.Direction(orientation) * (layout.pitch * offset);
    }

    public bool Resolve(int kind, int anchor, int orientation, int length)
    {
        pin0 = -1; pin1 = -1; pin2 = -1;
        if (kind < 0 || kind >= catalog.kinds.Length || anchor < 0 || anchor >= layout.holeIds.Length || orientation < 0 || orientation > 3) return false;
        pin0 = anchor;
        pin1 = layout.HoleAt(PinPosition(kind, anchor, orientation, length, 1));
        if (kind == 5) pin2 = layout.HoleAt(PinPosition(kind, anchor, orientation, length, 2));
        return pin1 >= 0 && pin1 != pin0 && (kind != 5 || (pin2 >= 0 && pin2 != pin0 && pin2 != pin1));
    }

    public bool Matches(int kind, int a, int b, int c, int orientation, int length)
    {
        return Resolve(kind, a, orientation, length) && pin1 == b && pin2 == c;
    }
}
