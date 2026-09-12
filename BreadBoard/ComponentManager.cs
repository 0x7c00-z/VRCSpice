
using System.Runtime.CompilerServices;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ComponentManager : UdonSharpBehaviour
{
    void Start()
    {
        
    }

    /*public enum Direction : int
    {
        Right,
        Up,
        Left,
        Down,
    }*/

    /*public GameObject generateModel(string type, float[] constants)
    {
        //Should be implemented in other script though...

        // Generate a 3D model based on the type and constants
        GameObject model = new GameObject();
        // Add components or set properties based on constants
        return model;
    }*/

    public Rect getRect(int x, int y, int Direction, string type, float[] constants)
    {
        // x, y : the left bottom corner of the component
        // Direction: 0, 1, 2, 3 for right, up, left, down respectively. (clockwise)
        // type: R, C, L, V, I, D, etc.]
        // constants: something
        int up = 0;
        int down = 0;
        int left = 0;
        int right = 0;// 1x1 at this point

        Rect rect = new Rect();

        switch (type)
        {
            case "R":
                left = 2;
                right = 2;
                break;
        }

        switch (Direction)
        {
            case 0:
            default:
                rect.xMax = 1 + right;
                rect.xMin = left;
                rect.yMax = 1 + up;
                rect.yMin = down;
                break;
            case 1:
                rect.xMax = 1 + down;
                rect.xMin = up;
                rect.yMax = 1 + left;
                rect.yMin = right;
                break;
            case 2:
                rect.xMax = 1 + left;
                rect.xMin = right;
                rect.yMax = 1 + down;
                rect.yMin = up;
                break;
            case 3:
                rect.xMax = 1 + up;
                rect.xMin = down;
                rect.yMax = 1 + right;
                rect.yMin = left;
                break;
        }

        rect.x = rect.x + x;
        rect.y = rect.y + y;

        return rect;
    }
}
