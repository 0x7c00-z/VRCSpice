
using Newtonsoft.Json;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;



public class Board : UdonSharpBehaviour
{
    private DataToken[][] holes;
    private DataList components;
    //Each DataToken is DataList
    // 0.string type
    // 1.int x
    // 2.int y
    // 3.int direction(0:left, 1:up, 2:right, 3:down)
    // 4.DataList<float> constants
    // 5.GameObject model

    private DataToken freeToken, filledToken;

    private ComponentManager compMan;

    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private Rect[] filledArea;


    void Start()
    {
        freeToken = (DataToken)"FREE!";
        filledToken = (DataToken)"there is no hole here btw";
        //Initialize the holes array with the specified width and height
        holes = new DataToken[width][];
        for(int i = 0; i < width; i++)
        {
            holes[i] = new DataToken[height];
            for(int j=0;j<height;j++)
            {
                holes[i][j] = freeToken; //Initialize all holes as empty
            }
        }

        //Fill the specified areas
        for(int i=0; i<filledArea.Length; i++)
        {
            for (int x = (int)filledArea[i].xMin; x < (int)filledArea[i].xMax; x++)
            {
                for(int y = (int)filledArea[i].yMin; y < (int)filledArea[i].yMax; y++)
                {
                    holes[x][y] = filledToken;
                }
            }
        }
    }

    public bool isPlaceable(Rect area)
    {
        for (int x = Mathf.RoundToInt(area.xMin); x < Mathf.RoundToInt(area.xMax); x++)
        {
            for (int y = Mathf.RoundToInt(area.yMin); y < Mathf.RoundToInt(area.yMax); y++)
            {
                if (holes[x][y] != freeToken)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void Place(int x, int y, int Direction, string type, float[] constants)
    {
        if(compMan == null)
        {
            compMan = this.gameObject.GetComponent<ComponentManager>();
        }
        //Place a component in the specified area and update the holes array accordingly
        /*new DataList componet = new DataList();
        Component.Add(type);
        Component.Add(x);
        Component.Add(y);
        Component.Add(Direction);
        DataList constList = new DataList();
        for(int i = 0; i < constants.Length; i++)
        {
            constList.Add(constants[i]);
        }
        Component.Add(constList);
        Component.Add(compMan.generateModel(type, constants));*/
    }
   

    void Update() {
        //show 3D models of components in the holes based on the components DataList
    }
}
