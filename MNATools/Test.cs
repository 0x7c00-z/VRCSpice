using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

// Manual solver harness. Oscilloscope display responsibilities live in
// OscilloscopeController.
public class Test : UdonSharpBehaviour
{
    [SerializeField] private TMP_InputField netlistfield;
    [SerializeField] private TMP_InputField stepsPerFrame;
    [SerializeField] private TMP_InputField maxError; // Scene compatibility only.
    private MNAGen generator;
    private MNASolve solver;

    private void Start()
    {
        solver = GetComponent<MNASolve>();
        generator = GetComponent<MNAGen>();
    }

    private void Update()
    {
        if (solver == null) return;
        int steps = 100;
        if (stepsPerFrame != null) int.TryParse(stepsPerFrame.text, out steps);
        if (steps > 0) solver.SetStepPerFrame(steps);
    }

    public void restart()
    {
        if (netlistfield == null || generator == null) return;
        string[] lines = netlistfield.text.Split('\n');
        DataList nextNetlist = new DataList();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            string[] elements = lines[i].Split(' ');
            int netCount = 0, constantCount = 0;
            string prefix = elements[0].Substring(0, 1);
            if (prefix == "R" || prefix == "C" || prefix == "V" || prefix == "L" || prefix == "I")
            { netCount = 2; constantCount = 1; }
            else if (prefix == "D")
            {
                netCount = 2; constantCount = 7;
                if (elements.Length != 10) { Debug.LogError(elements[0] + ": expected Dname anode cathode Is Vt TT Cjo Vj m Fc."); return; }
            }
            else if (prefix == "Q") { netCount = 3; constantCount = 0; }
            if (elements.Length < 1 + netCount + constantCount || netCount == 0) continue;
            DataList component = new DataList(); DataList nets = new DataList(); DataList constants = new DataList();
            for (int n = 0; n < netCount; n++) nets.Add(elements[1 + n]);
            for (int c = 0; c < constantCount; c++)
            {
                float value;
                if (!float.TryParse(elements[1 + netCount + c], out value))
                { Debug.LogError(elements[0] + ": invalid numeric constant."); return; }
                constants.Add(value);
            }
            component.Add(elements[0]); component.Add(nets); component.Add(constants); nextNetlist.Add(component);
        }
        generator.netlist = nextNetlist;
        generator.UpdateMNA();
    }
}
