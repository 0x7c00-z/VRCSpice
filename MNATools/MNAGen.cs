
using System;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class MNAGen : UdonSharpBehaviour
{
    public DataList netlist = new DataList();
    //Datalist netlist
    //  DataLists components
    //      0.string name
    //      1.Datalist nets
    //          string nets
    //      2.Datalist constants
    //          float constants

    public DataList preprocessedNetlist; //netlist but only contains R, C, L, V, D, V/I controled V/I source.
    private DataList vector = new DataList();
    private MNASolve solver;

    

    void Start()
    {
        solver = this.gameObject.GetComponent<MNASolve>();
    }

    private void preprocess()
    {
        preprocessedNetlist.Clear();
        for(int i = 0; i < netlist.Count; i++)
        {
            preprocessedNetlist.Add(netlist[i]);
        }
    }

    public void UpdateMNA() {
        preprocess();

        //Setup voltage/current vector
        vector.Clear();
        for(int i = 0; i < preprocessedNetlist.Count; i++)
        {
            DataList comp = (DataList)preprocessedNetlist[i];
            for(int j=0;j< IsCurrentNeeded((string)comp[0]); j++)
            {
                vector.Add("I_" + (string)comp[0] + "_" + j.ToString());
            }
            for(int j = 0; j < ((DataList)comp[1]).Count; j++)
            {
                string netname = (string)((DataList)comp[1])[j];
                if (!vector.Contains(netname) && netname.ToUpper() != "GND")
                {
                    vector.Add(netname);
                }
            }
            //Debug.Log("Processed " + (string)comp[0]);
        }

        if(solver == null)
        {
            solver = this.gameObject.GetComponent<MNASolve>();
        }
        solver.ChangeCircuit(vector);

        for(int i = 0; i < preprocessedNetlist.Count; i++)
        {
            DataList comp = (DataList)preprocessedNetlist[i];
            string comp_name = (string)comp[0];
            DataList nets = (DataList)comp[1];
            DataList consts = (DataList)comp[2];
            if (comp_name.StartsWith("R"))
            {
                //Register
                float g = 1.0f / (float)consts[0];
                solver.WriteCircuit((string)nets[0], (string)nets[0], "A", g);
                solver.WriteCircuit((string)nets[0], (string)nets[1], "A", -g);
                solver.WriteCircuit((string)nets[1], (string)nets[0], "A", -g);
                solver.WriteCircuit((string)nets[1], (string)nets[1], "A", g);
            }
            else if (comp_name.StartsWith("C"))
            {
                //Capacitor
                float c = (float)consts[0];
                solver.WriteCircuit((string)nets[0], (string)nets[0], "B", c);
                solver.WriteCircuit((string)nets[0], (string)nets[1], "B", -c);
                solver.WriteCircuit((string)nets[1], (string)nets[0], "B", -c);
                solver.WriteCircuit((string)nets[1], (string)nets[1], "B", c);
            }
            else if (comp_name.StartsWith("V"))
            {
                float v = (float)consts[0];
                solver.WriteCircuit((string)nets[0], "I_" + comp_name + "_0", "A", 1);
                solver.WriteCircuit((string)nets[1], "I_" + comp_name + "_0", "A", -1);
                solver.WriteCircuit("I_" + comp_name + "_0", (string)nets[0], "A", 1);
                solver.WriteCircuit("I_" + comp_name + "_0", (string)nets[1], "A", -1);
                solver.WriteCircuit("I_" + comp_name + "_0", "", "rhs", v);
            }
            else if (comp_name.StartsWith("L"))
            {
                //Inductor
                float l = (float)consts[0];
                solver.WriteCircuit((string)nets[0], "I_" + comp_name + "_0", "A", 1);
                solver.WriteCircuit((string)nets[1], "I_" + comp_name + "_0", "A", -1);
                solver.WriteCircuit("I_" + comp_name + "_0", (string)nets[0], "A", 1);
                solver.WriteCircuit("I_" + comp_name + "_0", (string)nets[1], "A", -1);
                solver.WriteCircuit("I_" + comp_name + "_0", "I_" + comp_name + "_0", "B", -l);
            }
            else if (comp_name.StartsWith("D"))
            {
                float i_s = (float)consts[0];
                float v_t = (float)consts[1];
                int cathodeIndex = solver.label2bufferRow((string)nets[1]);
                int anodeIndex = solver.label2bufferRow((string)nets[0]);
                // f adds a * (exp(b * (v2 - v1)) - 1): v1 = cathode, v2 = anode.
                uint[] diodeData = new uint[]
                {
                    1u,
                    BitConverter.ToUInt32(BitConverter.GetBytes(i_s), 0),
                    BitConverter.ToUInt32(BitConverter.GetBytes(1.0f / v_t), 0),
                    cathodeIndex < 0 ? uint.MaxValue : (uint)cathodeIndex,
                    anodeIndex < 0 ? uint.MaxValue : (uint)anodeIndex
                };
                solver.WriteNonLinerCircuit("I_" + comp_name + "_0", diodeData);
                solver.WriteCircuit("I_" + comp_name + "_0", "I_" + comp_name + "_0", "A", -1.0f);
                solver.WriteCircuit((string)nets[0], "I_" + comp_name + "_0", "A", 1.0f);
                solver.WriteCircuit((string)nets[1], "I_" + comp_name + "_0", "A", -1.0f);
            }
            else if (comp_name.StartsWith("Q"))//Q1 vc vb ve
            {
                float VT = 0.0259f;
                // NPN, terminal order C B E [S]; S defaults to GND (ROHM Figure 2).
                // Topology: https://fscdn.rohm.com/jp/products/databook/applinote/discrete/transistor/overview_of_rohms_simulation_models_for_bipolar_transistors_an-j.pdf
                // KSC1815: https://www.onsemi.com/pub/Collateral/KSC1815.lib
                // Fixed temperature, fixed RB and TF; no excess-phase or breakdown model.
                float IS = 2.04174E-14f;
                float BF = 127.6f;
                float NF = 1.0f;
                float BR = 4.595f;
                float NR = 1.0f;
                float ISE = 1.20226E-14f;
                float NE = 1.5f;
                float ISC = 1.31826E-13f;
                float NC = 1.5f;
                float VAF = 121.25f;
                float VAR = 24.03f;
                float IKF = 1.13f;
                float IKR = 0.793f;
                float NK = 0.853f; // 0.5 selects the standard square-root base-charge law.
                float RB = 34.0f;
                float RE = 0.38f;
                float RC = 7.7f;
                float CJE = 2.041E-11f;
                float VJE = 0.692258f;
                float MJE = 0.315545f;
                float CJC = 6.29422E-12f;
                float VJC = 0.410107f;
                float MJC = 0.247613f;
                float XCJC = 0.45f;
                float FC = 0.5f;
                float TF = 1.99E-9f;
                // Not specified by KSC1815: retain SPICE defaults, with editable parameters.
                float TR = 0.0f;
                float CJS = 0.0f;
                float VJS = 0.75f;
                float MJS = 0.0f;

                // I_0 = intrinsic Ic, I_1 = intrinsic Ib, I_2 = CJX current B -> C',
                // I_3 = CCS current S -> C'; slots I_4..I_6 hold voltages C', B', E'.
                string prefix = "I_" + comp_name + "_";
                string[] externalNodes = new string[] { (string)nets[0], (string)nets[1], (string)nets[2] };
                string[] internalNodes = new string[] { prefix + "4", prefix + "5", prefix + "6" };
                float[] resistances = new float[] { RC, RB, RE };
                string[] coreNodes = new string[3];
                for (int terminal = 0; terminal < 3; terminal++)
                {
                    if (resistances[terminal] > 0.0f)
                    {
                        coreNodes[terminal] = internalNodes[terminal];
                        float conductance = 1.0f / resistances[terminal];
                        solver.WriteCircuit(externalNodes[terminal], externalNodes[terminal], "A", conductance);
                        solver.WriteCircuit(externalNodes[terminal], internalNodes[terminal], "A", -conductance);
                        solver.WriteCircuit(internalNodes[terminal], externalNodes[terminal], "A", -conductance);
                        solver.WriteCircuit(internalNodes[terminal], internalNodes[terminal], "A", conductance);
                    }
                    else
                    {
                        // A zero series resistance aliases the external node; fix the unused slot.
                        coreNodes[terminal] = externalNodes[terminal];
                        solver.WriteCircuit(internalNodes[terminal], internalNodes[terminal], "A", 1.0f);
                    }
                }

                int cIndex = solver.label2bufferRow(coreNodes[0]);
                int bIndex = solver.label2bufferRow(coreNodes[1]);
                int eIndex = solver.label2bufferRow(coreNodes[2]);
                uint[] bjtData = new uint[28];
                // case 2: [1] 0=Ic / 1=Ib, [2..4] C'/B'/E' indices, [5..27] parameters.
                bjtData[0] = 2u;
                bjtData[2] = cIndex < 0 ? uint.MaxValue : (uint)cIndex;
                bjtData[3] = bIndex < 0 ? uint.MaxValue : (uint)bIndex;
                bjtData[4] = eIndex < 0 ? uint.MaxValue : (uint)eIndex;
                float[] bjtParameters = new float[]
                {
                    IS, 1.0f / BF, 1.0f / (NF * VT), 1.0f / BR, 1.0f / (NR * VT),
                    ISE, 1.0f / (NE * VT), ISC, 1.0f / (NC * VT),
                    VAF > 0.0f ? 1.0f / VAF : 0.0f, VAR > 0.0f ? 1.0f / VAR : 0.0f,
                    IKF > 0.0f ? 1.0f / IKF : 0.0f, IKR > 0.0f ? 1.0f / IKR : 0.0f,
                    CJE, VJE, MJE, CJC * XCJC, VJC, MJC, FC, TF, TR, NK
                };
                for (int parameter = 0; parameter < bjtParameters.Length; parameter++)
                    bjtData[parameter + 5] = BitConverter.ToUInt32(BitConverter.GetBytes(bjtParameters[parameter]), 0);

                for (int branch = 0; branch < 2; branch++)
                {
                    string current = prefix + branch.ToString();
                    bjtData[1] = (uint)branch;
                    solver.WriteNonLinerCircuit(current, bjtData);
                    solver.WriteCircuit(current, current, "A", -1.0f);
                    solver.WriteCircuit(coreNodes[branch], current, "A", 1.0f);
                    solver.WriteCircuit(coreNodes[2], current, "A", -1.0f);
                }

                // CJX connects the external base to C', bypassing RB. CCS uses Vs - Vc'.
                string substrate = nets.Count > 3 ? (string)nets[3] : "GND";
                string[] capPositiveNodes = new string[] { externalNodes[1], substrate };
                float[] capValues = new float[] { CJC * (1.0f - XCJC), CJS };
                float[] capPotentials = new float[] { VJC, VJS };
                float[] capExponents = new float[] { MJC, MJS };
                float[] capCutoffs = new float[] { FC, 0.0f };
                for (int branch = 0; branch < 2; branch++)
                {
                    string current = prefix + (branch + 2).ToString();
                    int positiveIndex = solver.label2bufferRow(capPositiveNodes[branch]);
                    // case 3: [1..2] positive/negative indices, [3..6] C0, VJ, M, FC.
                    uint[] capData = new uint[]
                    {
                        3u,
                        positiveIndex < 0 ? uint.MaxValue : (uint)positiveIndex,
                        cIndex < 0 ? uint.MaxValue : (uint)cIndex,
                        BitConverter.ToUInt32(BitConverter.GetBytes(capValues[branch]), 0),
                        BitConverter.ToUInt32(BitConverter.GetBytes(capPotentials[branch]), 0),
                        BitConverter.ToUInt32(BitConverter.GetBytes(capExponents[branch]), 0),
                        BitConverter.ToUInt32(BitConverter.GetBytes(capCutoffs[branch]), 0)
                    };
                    solver.WriteNonLinerCircuit(current, capData);
                    solver.WriteCircuit(current, current, "A", -1.0f);
                    solver.WriteCircuit(capPositiveNodes[branch], current, "A", 1.0f);
                    solver.WriteCircuit(coreNodes[0], current, "A", -1.0f);
                }
            }
            else if (comp_name.StartsWith("I")) {
                float j = (float)consts[0];
                solver.WriteCircuit((string)nets[0], "", "rhs", -j);
                solver.WriteCircuit((string)nets[1], "", "rhs", j);
            }
        }
    }

    private int IsCurrentNeeded(string name)
    {
        if (name.StartsWith("L") || name.StartsWith("V") || name.StartsWith("D")) {
            return 1;
        }
        if (name.StartsWith("Q"))
        {
            return 7;
        }
        return 0;
    }

    public override string ToString()
    {
        string ret = "";
        return ret;
    }
}
