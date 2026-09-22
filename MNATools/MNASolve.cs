
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

public class MNASolve : UdonSharpBehaviour
{
    [SerializeField] private Material processor;
    [SerializeField] private Material flowControl;
    [SerializeField] private Material CopyColumn;
    [SerializeField] private Material[] outputMaterial; // deprecated

    private RenderTexture buffer0, buffer1, tmpbuffer;
    private Texture2D A, B, C, rhs, Is;
    private uint[] C_row;
    private byte[] C_bytes;
    private bool[] needApply;//A, B, C, rhs, Is
    private DataList veclabels;
    private int matrixsize = 0;

    bool outputBuffer0 = false;//For double buffering
    private int stepperframe = 100;
    private float deltatime = 0.0001f;
    private float maxPCError = 0.05f;
    bool initialized = false;
    const int buflen = 100;

    //DEBUG
    [SerializeField] private bool stepexecution = false;
    [SerializeField] private bool EXECUTE = false;
    [SerializeField] private Material debugMat;

    private void Start()
    {
        veclabels = new DataList();
        needApply = new bool[5];
        for(int i = 0; i < needApply.Length; i++)
        {
            needApply[i] = false;
        }
    }

    private void Update()
    {
        processor.SetFloat("_DeltaTime", deltatime);
        processor.SetFloat("_MaxPCError", maxPCError);
        if(initialized && (!stepexecution || EXECUTE)) {
            Process();
            EXECUTE = false;
        }
    }

    public void SetStepPerFrame(int steps)
    {
        stepperframe = steps;
    }
    public void SetPCError(float err)
    {
        maxPCError = err;
    }

    public int label2bufferRow(string label)
    {
        return veclabels.IndexOf(label);
    }

    public void WriteToMaterial(Material mat)
    {
        mat.SetTexture("_MainTex", outputBuffer0 ? buffer0 : buffer1);
        mat.SetInteger("_DATA_N", matrixsize);
    }

    void Process()
    {
        //time step
        ApplyTexture();
        processor.SetTexture("_A", A);
        processor.SetTexture("_B", B);
        processor.SetTexture("_C", C);
        processor.SetTexture("_rhs", rhs);
        processor.SetTexture("_Is", Is);
        processor.SetInteger("_DATA_N", matrixsize);
        flowControl.SetInteger("_DATA_N", matrixsize);

        for (int i = 0; i < outputMaterial.Length; i++)
        {
            WriteToMaterial(outputMaterial[i]);
        }

        //CALL ONLY ONCE PER FRAME, OTHERWISE THE TWO BUFFERS WILL BE OUT OF SYNC AND THE RESULT WILL BE WRONG
        if (outputBuffer0)
        {
            //output buffer0, process buffer1 
            
            
            VRCGraphics.Blit(buffer0, tmpbuffer, processor);
            VRCGraphics.Blit(tmpbuffer, buffer1, flowControl);

            for (int i = 0; i < stepperframe -1; i++)
            {
                // Processing logic for buffer1
                VRCGraphics.Blit(buffer1, tmpbuffer, processor);
                VRCGraphics.Blit(tmpbuffer, buffer1, flowControl);
            }
        }
        else
        {
            //output buffer1, process buffer0


            VRCGraphics.Blit(buffer1, tmpbuffer, processor);
            VRCGraphics.Blit(tmpbuffer, buffer0, flowControl);

            for (int i = 0; i < stepperframe -1; i++)
            {
                // Processing logic for buffer0
                VRCGraphics.Blit(buffer0, tmpbuffer, processor);
                VRCGraphics.Blit(tmpbuffer, buffer0, flowControl);
            }

        }
        outputBuffer0 = !outputBuffer0;
    }

    private RenderTexture CreateSolverBuffer(int width, int height)
    {
        // Udon exposes the copy constructor, but not the GraphicsFormat constructor.
        // The processor material references an R32_UInt template asset.
        RenderTexture buffer = new RenderTexture((RenderTexture)processor.GetTexture("_BufferTemplate"));
        buffer.width = width;
        buffer.height = height;
        buffer.filterMode = FilterMode.Point;
        buffer.wrapMode = TextureWrapMode.Clamp;
        buffer.useMipMap = false;
        buffer.autoGenerateMips = false;
        buffer.antiAliasing = 1;
        buffer.Create();
        return buffer;
    }

    public void ChangeCircuit(DataList newlabel)
    {
        //generate new buffer
        //used when netlist itself was changed
        //Matrix and vectors must be rewitten after this method

        //setup new buffer RenderTexture
        RenderTexture buffer0, buffer1, tmpbuffer;
        int newmatrixsize = newlabel.Count;
        int width = Mathf.NextPowerOfTwo(Mathf.Max(newmatrixsize + 1, 12));
        int height = Mathf.NextPowerOfTwo(5 + newmatrixsize + buflen);
        int mattexsize = Mathf.NextPowerOfTwo(newmatrixsize);

        buffer0 = CreateSolverBuffer(width, height);
        buffer1 = CreateSolverBuffer(width, height);
        tmpbuffer = CreateSolverBuffer(width, height);

        //copy old buffer to new buffer
        Texture2D indexerbuf = new Texture2D(width, 1, TextureFormat.RFloat, false);
        for (int i = 0; i < newmatrixsize; i++)
        {
            indexerbuf.SetPixel(i, 0, new Color(veclabels.IndexOf((string)newlabel[i]), 0, 0));
        }
        for(int i = newmatrixsize; i < width; i++)
        {
            indexerbuf.SetPixel(i, 0, new Color(-1, 0, 0));
        }

        // Preserve the time-step column alongside the remapped solution history.
        indexerbuf.SetPixel(newmatrixsize, 0, new Color(matrixsize > 0 ? matrixsize : -1, 0, 0));
        indexerbuf.filterMode = FilterMode.Point;
        indexerbuf.wrapMode = TextureWrapMode.Clamp;
        indexerbuf.Apply();
        
        CopyColumn.SetTexture("_IndexMat", indexerbuf);
        CopyColumn.SetInteger("_DstMatSize", newmatrixsize);
        CopyColumn.SetInteger("_SrcMatSize", matrixsize);
        CopyColumn.SetInteger("_DstTexHeight", height);

        VRCGraphics.Blit(this.buffer0, buffer0, CopyColumn);
        VRCGraphics.Blit(this.buffer1, buffer1, CopyColumn);

        //replace old buffer with new buffer
        this.buffer0 = buffer0;
        this.buffer1 = buffer1;
        this.tmpbuffer = tmpbuffer;

        //Setup new matrix texture;
        this.A = new Texture2D(mattexsize, mattexsize, TextureFormat.RFloat, false);
        this.B = new Texture2D(mattexsize, mattexsize, TextureFormat.RFloat, false);
        this.C = new Texture2D(32, mattexsize, TextureFormat.RG32, false, true);
        C.filterMode = FilterMode.Point;
        C.wrapMode = TextureWrapMode.Clamp;
        C_row = new uint[C.width * C.height];
        C_bytes = new byte[C_row.Length * 4];
        C.LoadRawTextureData(C_bytes);
        this.rhs = new Texture2D(mattexsize, 1, TextureFormat.RFloat, false);
        this.Is = new Texture2D(mattexsize, 1, TextureFormat.RFloat, false);

        for(int i=0; i<mattexsize; i++)
        {
            for(int j=0; j<mattexsize; j++)
            {
                this.A.SetPixel(i, j, new Color(0, 0, 0));
                this.B.SetPixel(i, j, new Color(0, 0, 0));
            }
            this.rhs.SetPixel(i, 0, new Color(0, 0, 0));
            this.Is.SetPixel(i, 0, new Color(0, 0, 0));
        }

        A.Apply();
        B.Apply();
        C.Apply(false, false);
        rhs.Apply();
        Is.Apply();

        matrixsize = newmatrixsize;
        veclabels = newlabel;
        initialized = true;
    }

    //名前からRFloatのテクスチャの該当箇所に書き込むための関数。
    public void WriteCircuit(string row, string column, string tex, float value)
    {
        //used when only a single element of the netlist was changed, so we can just update the corresponding element in the buffer instead of regenerating the whole buffer
        //row and column are the labels of the row and column of the matrix to be updated, tex is the name of the texture to be updated (A, B, C, rhs or Is), value is the new value to be written
        int rowindex = veclabels.IndexOf(row);
        int colindex = veclabels.IndexOf(column);

        //for vectors do not have column
        switch (tex)
        {
            case "rhs":
            case "Is":
                colindex = 0;
                break;
        }

        if(rowindex == -1 || colindex == -1)
        {
            return;
        }
        Texture2D targettex = null;
        switch(tex)
        {
            case "A":
                targettex = A;
                needApply[0] = true;
                break;
            case "B":
                targettex = B;
                needApply[1] = true;
                break;
            case "rhs":
                targettex = rhs;
                needApply[3] = true;
                break;
            case "Is":
                targettex = Is;
                needApply[4] = true;
                break;
            default:
                Debug.LogError("Invalid texture name: " + tex);
                return;
        }
        //Debug.Log(tex + "(" + rowindex + ", " + colindex + ") = " + value);
        value = value + targettex.GetPixel(rowindex, colindex).r;
        targettex.SetPixel(rowindex, colindex, new Color(value, 0, 0));
    }

    public void WriteNonLinerCircuit(string row, uint[] data)
    {
        //see Solver_model.hlsl about implemantation of nonliner things:)
        needApply[2] = true;
        int rowindex = veclabels.IndexOf(row);
        for (int i = 0; i < C.width; i++)
        {
            C_row[rowindex* C.width + i] = (i < data.Length) ? data[i] : 0;
        }

    }

    private void LoadNonlinearTextureData()
    {
        // RG32 stores low 16 bits in R and high 16 bits in G.
        for (int i = 0; i < C_row.Length; i++)
        {
            uint value = C_row[i];
            int offset = i * 4;
            C_bytes[offset] = (byte)(value & 0xffu);
            C_bytes[offset + 1] = (byte)((value >> 8) & 0xffu);
            C_bytes[offset + 2] = (byte)((value >> 16) & 0xffu);
            C_bytes[offset + 3] = (byte)((value >> 24) & 0xffu);
        }
        C.LoadRawTextureData(C_bytes);
    }

    private void ApplyTexture()
    {
        //used to upload textures to GPU
        Texture2D targettex = null;
        for(int i=0; i<needApply.Length; i++)
        {
            if(needApply[i])
            {
                switch(i)
                {
                    case 0:
                        targettex = A;
                        break;
                    case 1:
                        targettex = B;
                        break;
                    case 2:
                        targettex = C;
                        LoadNonlinearTextureData();
                        break;
                    case 3:
                        targettex = rhs;
                        break;
                    case 4:
                        targettex = Is;
                        break;
                }
                targettex.Apply();
                needApply[i] = false;
            }
        }
    }
}
