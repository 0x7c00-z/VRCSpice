using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

public class MNASolve : UdonSharpBehaviour
{
    public const int SchemeRadauIIA5 = 0;
    public const int SchemeBackwardEuler = 1;

    [SerializeField] private Material processor;
    [SerializeField] private Material flowControl;
    [SerializeField] private Material CopyColumn;
    [SerializeField] private Material dumpExtractor;
    // Internal diagnostics only. Public consumers use the stable output dump.
    [SerializeField] private Material[] outputMaterial;
    [SerializeField] private int integrationScheme = SchemeRadauIIA5;
    [SerializeField] private float maxdeltatime = 0.00001f;
    [SerializeField] private int maxNewtonIterations = 32;

    private RenderTexture buffer0, buffer1, tmpbuffer, outputDump;
    private Texture2D A, B, C, rhs, Is, timeEvolution;
    private uint[] C_row;
    private byte[] C_bytes;
    private bool[] needApply;
    private DataList veclabels;
    private int matrixsize;
    private bool outputBuffer0;
    private int stepperframe = 100;
    private bool initialized;
    private int settingsRevision = 1;
    private int clearHistoryRevision = 1;
    private int restartRevision = 1;
    private int dumpGeneration = 1;
    private const int BufferLength = 1000;
    private const int DumpTextureHeight = 1024;

    [SerializeField] private bool stepexecution;
    [SerializeField] private bool EXECUTE;
    [SerializeField] private Material debugMat;

    private void Start()
    {
        veclabels = new DataList();
        needApply = new bool[5];
        if (maxNewtonIterations < 1 || maxNewtonIterations > 256) maxNewtonIterations = 32;
        BuildTimeEvolutionTexture();
    }

    private void BuildTimeEvolutionTexture()
    {
        timeEvolution = new Texture2D(4, 16, TextureFormat.RFloat, false, true);
        timeEvolution.filterMode = FilterMode.Point;
        timeEvolution.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 4; x++) timeEvolution.SetPixel(x, y, Color.black);

        // Backward Euler: D, P/h, c.
        SetCoefficientRow(0, -1f, 1f, 0f, 0f);
        SetCoefficientRow(1, 0f, 1f, 0f, 0f);
        SetCoefficientRow(2, 0f, 1f, 0f, 0f);

        float sqrt6 = Mathf.Sqrt(6f);
        float c1 = (4f - sqrt6) / 10f;
        float c2 = (4f + sqrt6) / 10f;
        SetCoefficientRow(3, -1f, 1f, 0f, 0f);
        SetCoefficientRow(4, -1f, 0f, 1f, 0f);
        SetCoefficientRow(5, -1f, 0f, 0f, 1f);
        SetCoefficientRow(6, 0f, (88f - 7f * sqrt6) / 360f,
            (296f - 169f * sqrt6) / 1800f, (-2f + 3f * sqrt6) / 225f);
        SetCoefficientRow(7, 0f, (296f + 169f * sqrt6) / 1800f,
            (88f + 7f * sqrt6) / 360f, (-2f - 3f * sqrt6) / 225f);
        SetCoefficientRow(8, 0f, (16f - sqrt6) / 36f,
            (16f + sqrt6) / 36f, 1f / 9f);
        SetCoefficientRow(9, 0f, c1, c2, 1f);
        timeEvolution.Apply(false, false);
    }

    private void SetCoefficientRow(int row, float a, float b, float c, float d)
    {
        timeEvolution.SetPixel(0, row, new Color(a, 0f, 0f, 0f));
        timeEvolution.SetPixel(1, row, new Color(b, 0f, 0f, 0f));
        timeEvolution.SetPixel(2, row, new Color(c, 0f, 0f, 0f));
        timeEvolution.SetPixel(3, row, new Color(d, 0f, 0f, 0f));
    }

    private void Update()
    {
        if (initialized && (!stepexecution || EXECUTE))
        {
            Process();
            EXECUTE = false;
        }
    }

    public void SetStepPerFrame(int steps) { if (steps > 0) stepperframe = steps; }

    // Compatibility entry point. Predictor/corrector error control no longer exists.
    public void SetPCError(float unused) { }

    public void SetIntegrationScheme(int scheme)
    {
        SetSolverSettings(scheme, maxdeltatime, maxNewtonIterations);
    }

    public void SetOutputDeltaTime(float value)
    {
        SetSolverSettings(integrationScheme, value, maxNewtonIterations);
    }

    public void SetMaxDeltaTime(float value) { SetOutputDeltaTime(value); }

    public void SetMaxNewtonIterations(int value)
    {
        SetSolverSettings(integrationScheme, maxdeltatime, value);
    }

    public void SetSolverSettings(int scheme, float value, int newtonIterations)
    {
        if ((scheme != SchemeRadauIIA5 && scheme != SchemeBackwardEuler) ||
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ||
            newtonIterations < 1 || newtonIterations > 256) return;
        bool schemeChanged = integrationScheme != scheme;
        bool timeChanged = maxdeltatime != value;
        bool newtonChanged = maxNewtonIterations != newtonIterations;
        if (!schemeChanged && !timeChanged && !newtonChanged) return;
        integrationScheme = scheme;
        maxdeltatime = value;
        maxNewtonIterations = newtonIterations;
        settingsRevision++;
        if (timeChanged) { clearHistoryRevision++; AdvanceDumpGeneration(); }
    }

    public void ApplySynchronizedSettings(int scheme, float value, int newtonIterations,
        int revision, int historyRevision)
    {
        if ((scheme != SchemeRadauIIA5 && scheme != SchemeBackwardEuler) ||
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ||
            newtonIterations < 1 || newtonIterations > 256) return;
        bool historyChanged = clearHistoryRevision != Mathf.Max(1, historyRevision);
        integrationScheme = scheme;
        maxdeltatime = value;
        maxNewtonIterations = newtonIterations;
        settingsRevision = Mathf.Max(1, revision);
        clearHistoryRevision = Mathf.Max(1, historyRevision);
        if (historyChanged) AdvanceDumpGeneration();
    }

    public void RestartSimulation()
    {
        if (restartRevision >= int.MaxValue) return;
        restartRevision++;
        AdvanceDumpGeneration();
    }

    public void ApplySynchronizedRestart(int revision)
    {
        int next = Mathf.Max(1, revision);
        if (restartRevision == next) return;
        restartRevision = next;
        AdvanceDumpGeneration();
    }

    public int GetIntegrationScheme() { return integrationScheme; }
    public float GetOutputDeltaTime() { return maxdeltatime; }
    public int GetMaxNewtonIterations() { return maxNewtonIterations; }
    public int GetSettingsRevision() { return settingsRevision; }
    public int GetClearHistoryRevision() { return clearHistoryRevision; }
    public int GetRestartRevision() { return restartRevision; }

    public int label2bufferRow(string label) { return veclabels.IndexOf(label); }

    private void AdvanceDumpGeneration()
    {
        dumpGeneration = dumpGeneration >= int.MaxValue ? 1 : dumpGeneration + 1;
    }

    private RenderTexture ActiveSolverBuffer()
    {
        return outputBuffer0 ? buffer0 : buffer1;
    }

    public bool HasOutputDump() { return outputDump != null; }

    public void WriteOutputDumpToMaterial(Material mat)
    {
        if (mat == null || outputDump == null) return;
        mat.SetTexture("_SolverDump", outputDump);
        mat.SetInteger("_DATA_N", matrixsize);
        mat.SetFloat("_OutputDeltaTime", maxdeltatime);
    }

    // Compatibility entry point used by Breadboard renderers.
    public void WriteToMaterial(Material mat)
    {
        if (mat == null || outputDump == null) return;
        mat.SetTexture("_MainTex", outputDump);
        mat.SetInteger("_DATA_N", matrixsize);
        mat.SetFloat("_OutputDeltaTime", maxdeltatime);
    }

    private void ExtractOutputDump()
    {
        RenderTexture active = ActiveSolverBuffer();
        if (dumpExtractor == null || outputDump == null || active == null) return;
        dumpExtractor.SetInteger("_DATA_N", matrixsize);
        dumpExtractor.SetInteger("_DumpGeneration", dumpGeneration);
        dumpExtractor.SetFloat("_OutputDeltaTime", maxdeltatime);
        VRCGraphics.Blit(active, outputDump, dumpExtractor);
    }

    private void WriteDiagnostics()
    {
        RenderTexture active = ActiveSolverBuffer();
        if (active == null) return;
        if (outputMaterial != null)
            for (int i = 0; i < outputMaterial.Length; i++)
            {
                Material mat = outputMaterial[i];
                if (mat == null) continue;
                mat.SetTexture("_MainTex", active);
                mat.SetInteger("_DATA_N", matrixsize);
                mat.SetFloat("_OutputDeltaTime", maxdeltatime);
            }
        if (debugMat != null)
        {
            debugMat.SetTexture("_MainTex", active);
            debugMat.SetInteger("_DATA_N", matrixsize);
            debugMat.SetFloat("_OutputDeltaTime", maxdeltatime);
        }
    }

    private void ConfigureMaterial(Material material)
    {
        int stages = integrationScheme == SchemeBackwardEuler ? 1 : 3;
        material.SetTexture("_A", A);
        material.SetTexture("_B", B);
        material.SetTexture("_C", C);
        material.SetTexture("_rhs", rhs);
        material.SetTexture("_Is", Is);
        material.SetTexture("_TimeEvolution", timeEvolution);
        material.SetInteger("_DATA_N", matrixsize);
        material.SetInteger("_STAGE_COUNT", stages);
        material.SetInteger("_SYSTEM_N", matrixsize * stages);
        material.SetInteger("_IntegrationScheme", integrationScheme);
        material.SetInteger("_MaxNewtonIterations", maxNewtonIterations);
        material.SetInteger("_SettingsRevision", settingsRevision);
        material.SetInteger("_ClearHistoryRevision", clearHistoryRevision);
        material.SetInteger("_RestartRevision", restartRevision);
        material.SetFloat("_OutputDeltaTime", maxdeltatime);
    }

    private void Process()
    {
        ApplyTexture();
        ConfigureMaterial(processor);
        ConfigureMaterial(flowControl);

        if (outputBuffer0)
        {
            VRCGraphics.Blit(buffer0, tmpbuffer, processor);
            VRCGraphics.Blit(tmpbuffer, buffer1, flowControl);
            for (int i = 1; i < stepperframe; i++)
            {
                VRCGraphics.Blit(buffer1, tmpbuffer, processor);
                VRCGraphics.Blit(tmpbuffer, buffer1, flowControl);
            }
        }
        else
        {
            VRCGraphics.Blit(buffer1, tmpbuffer, processor);
            VRCGraphics.Blit(tmpbuffer, buffer0, flowControl);
            for (int i = 1; i < stepperframe; i++)
            {
                VRCGraphics.Blit(buffer0, tmpbuffer, processor);
                VRCGraphics.Blit(tmpbuffer, buffer0, flowControl);
            }
        }
        outputBuffer0 = !outputBuffer0;
        ExtractOutputDump();
        WriteDiagnostics();
    }

    private RenderTexture CreateSolverBuffer(int width, int height)
    {
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
        int newmatrixsize = newlabel.Count;
        int width = Mathf.NextPowerOfTwo(Mathf.Max(3 * newmatrixsize, Mathf.Max(newmatrixsize + 1, 16)));
        int height = Mathf.NextPowerOfTwo(4 + 3 * newmatrixsize + BufferLength);
        int mattexsize = Mathf.NextPowerOfTwo(Mathf.Max(1, newmatrixsize));
        RenderTexture next0 = CreateSolverBuffer(width, height);
        RenderTexture next1 = CreateSolverBuffer(width, height);
        RenderTexture nextTmp = CreateSolverBuffer(width, height);
        int dumpWidth = Mathf.NextPowerOfTwo(Mathf.Max(newmatrixsize + 1, 16));
        RenderTexture nextOutputDump = CreateSolverBuffer(dumpWidth, DumpTextureHeight);

        Texture2D indexer = new Texture2D(width, 1, TextureFormat.RG32, false, true);
        byte[] indexerBytes = new byte[width * 4];
        for (int i = 0; i < width; i++)
        {
            int oldIndex = -1;
            if (i < newmatrixsize) oldIndex = veclabels.IndexOf((string)newlabel[i]);
            else if (i == newmatrixsize && matrixsize > 0) oldIndex = matrixsize;
            uint value = oldIndex < 0 ? uint.MaxValue : (uint)oldIndex;
            int offset = i * 4;
            indexerBytes[offset] = (byte)(value & 255u);
            indexerBytes[offset + 1] = (byte)((value >> 8) & 255u);
            indexerBytes[offset + 2] = (byte)((value >> 16) & 255u);
            indexerBytes[offset + 3] = (byte)((value >> 24) & 255u);
        }
        indexer.filterMode = FilterMode.Point;
        indexer.wrapMode = TextureWrapMode.Clamp;
        indexer.LoadRawTextureData(indexerBytes);
        indexer.Apply(false, false);
        CopyColumn.SetTexture("_IndexMat", indexer);
        CopyColumn.SetInteger("_DstMatSize", newmatrixsize);
        // Resume from the most recent completed output. CopyColumn maps
        // surviving labels and discards unfinished Newton/stage work.
        CopyColumn.SetInteger("_SrcMatSize", matrixsize);
        CopyColumn.SetInteger("_DstTexHeight", height);
        RenderTexture current = outputBuffer0 ? buffer0 : buffer1;
        VRCGraphics.Blit(current, next0, CopyColumn);
        VRCGraphics.Blit(current, next1, CopyColumn);
        Destroy(indexer);

        if (buffer0 != null) Destroy(buffer0);
        if (buffer1 != null) Destroy(buffer1);
        if (tmpbuffer != null) Destroy(tmpbuffer);
        if (outputDump != null) Destroy(outputDump);
        if (A != null) Destroy(A);
        if (B != null) Destroy(B);
        if (C != null) Destroy(C);
        if (rhs != null) Destroy(rhs);
        if (Is != null) Destroy(Is);
        buffer0 = next0; buffer1 = next1; tmpbuffer = nextTmp; outputDump = nextOutputDump;
        outputBuffer0 = false;
        AdvanceDumpGeneration();

        A = new Texture2D(mattexsize, mattexsize, TextureFormat.RFloat, false);
        B = new Texture2D(mattexsize, mattexsize, TextureFormat.RFloat, false);
        C = new Texture2D(32, mattexsize, TextureFormat.RG32, false, true);
        C.filterMode = FilterMode.Point; C.wrapMode = TextureWrapMode.Clamp;
        C_row = new uint[C.width * C.height]; C_bytes = new byte[C_row.Length * 4];
        C.LoadRawTextureData(C_bytes);
        rhs = new Texture2D(mattexsize, 1, TextureFormat.RFloat, false);
        Is = new Texture2D(mattexsize, 1, TextureFormat.RFloat, false);
        for (int i = 0; i < mattexsize; i++)
        {
            for (int j = 0; j < mattexsize; j++) { A.SetPixel(i, j, Color.black); B.SetPixel(i, j, Color.black); }
            rhs.SetPixel(i, 0, Color.black); Is.SetPixel(i, 0, Color.black);
        }
        A.Apply(); B.Apply(); C.Apply(false, false); rhs.Apply(); Is.Apply();
        matrixsize = newmatrixsize;
        veclabels = newlabel.ShallowClone();
        ExtractOutputDump();
        WriteDiagnostics();
        initialized = true;
    }

    public void WriteCircuit(string row, string column, string tex, float value)
    {
        int rowindex = veclabels.IndexOf(row);
        int colindex = veclabels.IndexOf(column);
        if (tex == "rhs" || tex == "Is") colindex = 0;
        if (rowindex < 0 || colindex < 0) return;
        Texture2D target = null;
        if (tex == "A") { target = A; needApply[0] = true; }
        else if (tex == "B") { target = B; needApply[1] = true; }
        else if (tex == "rhs") { target = rhs; needApply[3] = true; }
        else if (tex == "Is") { target = Is; needApply[4] = true; }
        else { Debug.LogError("Invalid texture name: " + tex); return; }
        value += target.GetPixel(rowindex, colindex).r;
        target.SetPixel(rowindex, colindex, new Color(value, 0f, 0f, 0f));
    }

    public void WriteNonLinerCircuit(string row, uint[] data)
    {
        int rowindex = veclabels.IndexOf(row);
        if (rowindex < 0) return;
        needApply[2] = true;
        for (int i = 0; i < C.width; i++) C_row[rowindex * C.width + i] = i < data.Length ? data[i] : 0u;
    }

    private void LoadNonlinearTextureData()
    {
        for (int i = 0; i < C_row.Length; i++)
        {
            uint value = C_row[i]; int offset = i * 4;
            C_bytes[offset] = (byte)(value & 255u);
            C_bytes[offset + 1] = (byte)((value >> 8) & 255u);
            C_bytes[offset + 2] = (byte)((value >> 16) & 255u);
            C_bytes[offset + 3] = (byte)((value >> 24) & 255u);
        }
        C.LoadRawTextureData(C_bytes);
    }

    private void ApplyTexture()
    {
        for (int i = 0; i < needApply.Length; i++)
        {
            if (!needApply[i]) continue;
            Texture2D target = i == 0 ? A : i == 1 ? B : i == 2 ? C : i == 3 ? rhs : Is;
            if (i == 2) LoadNonlinearTextureData();
            target.Apply(); needApply[i] = false;
        }
    }
}
