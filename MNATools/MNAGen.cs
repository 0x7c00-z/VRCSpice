
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
                solver.WriteCircuit("I_" + comp_name + "_0", (string)nets[0], "C", 1.0f / v_t);
                solver.WriteCircuit("I_" + comp_name + "_0", (string)nets[1], "C", -1.0f / v_t);
                solver.WriteCircuit("I_" + comp_name + "_0", "", "Is", i_s);
                solver.WriteCircuit("I_" + comp_name + "_0", "I_" + comp_name + "_0", "A", -1.0f);
                solver.WriteCircuit((string)nets[0], "I_" + comp_name + "_0", "A", 1.0f);
                solver.WriteCircuit((string)nets[1], "I_" + comp_name + "_0", "A", -1.0f);
            }
            else if (comp_name.StartsWith("Q"))//Q1 vc vb ve
            {
                float VT = 0.0259f;
                //KSC1815 Gummel poon model
                //See https://fscdn.rohm.com/jp/products/databook/applinote/discrete/transistor/overview_of_rohms_simulation_models_for_bipolar_transistors_an-j.pdf
                //Model from onsemi.com
                float IS = 2.04174E-14f;
                float BF = 127.6f;
                float NF = 1;
                float BR = 4.595f;
                float NR = 1;
                float ISE = 1.20226E-14f;
                float NE = 1.5f;
                float ISC = 1.31826E-13f;
                float NC = 1.5f;
                float RB = 34;
                float RE = 0.38f;
                float RC = 7.7f;

                //I_0~I_6 refers to Ibc Ibe Iec Icc Vc Vb Ve
                string net_Vc = "I_" + comp_name + "_4";
                string net_Vb = "I_" + comp_name + "_5";
                string net_Ve = "I_" + comp_name + "_6";

                solver.WriteCircuit((string)nets[0], (string)nets[0], "A", 1/RC);
                solver.WriteCircuit((string)nets[0], net_Vc, "A", -1/RC);
                solver.WriteCircuit(net_Vc, (string)nets[0], "A", -1/RC);
                solver.WriteCircuit(net_Vc, net_Vc, "A", 1/RC);

                solver.WriteCircuit((string)nets[1], (string)nets[1], "A", 1 / RB);
                solver.WriteCircuit((string)nets[1], net_Vb, "A", -1 / RB);
                solver.WriteCircuit(net_Vb, (string)nets[1], "A", -1 / RB);
                solver.WriteCircuit(net_Vb, net_Vb, "A", 1 / RB);

                solver.WriteCircuit((string)nets[2], (string)nets[2], "A", 1 / RE);
                solver.WriteCircuit((string)nets[2], net_Ve, "A", -1 / RE);
                solver.WriteCircuit(net_Ve, (string)nets[2], "A", -1 / RE);
                solver.WriteCircuit(net_Ve, net_Ve, "A", 1 / RE);

                solver.WriteCircuit("I_" + comp_name + "_0", net_Vb, "C", 1.0f / VT / NC);
                solver.WriteCircuit("I_" + comp_name + "_0", net_Vc, "C", -1.0f / VT / NC);
                solver.WriteCircuit("I_" + comp_name + "_0", "", "Is", ISC);
                solver.WriteCircuit("I_" + comp_name + "_0", "I_" + comp_name + "_0", "A", -1.0f);
                solver.WriteCircuit(net_Vb, "I_" + comp_name + "_0", "A", 1.0f);
                solver.WriteCircuit(net_Vc, "I_" + comp_name + "_0", "A", -1.0f);

                solver.WriteCircuit("I_" + comp_name + "_1", net_Vb, "C", 1.0f / VT / NE);
                solver.WriteCircuit("I_" + comp_name + "_1", net_Ve, "C", -1.0f / VT / NE);
                solver.WriteCircuit("I_" + comp_name + "_1", "", "Is", ISE);
                solver.WriteCircuit("I_" + comp_name + "_1", "I_" + comp_name + "_1", "A", -1.0f);
                solver.WriteCircuit(net_Vb, "I_" + comp_name + "_1", "A", 1.0f);
                solver.WriteCircuit(net_Ve, "I_" + comp_name + "_1", "A", -1.0f);

                solver.WriteCircuit("I_" + comp_name + "_2", net_Vb, "C", 1.0f / VT / NR);
                solver.WriteCircuit("I_" + comp_name + "_2", net_Vc, "C", -1.0f / VT / NR);
                solver.WriteCircuit("I_" + comp_name + "_2", "", "Is", IS);
                solver.WriteCircuit("I_" + comp_name + "_2", "I_" + comp_name + "_2", "A", -1.0f);
                solver.WriteCircuit(net_Vb, "I_" + comp_name + "_2", "A", 1.0f / BR);
                solver.WriteCircuit(net_Vc, "I_" + comp_name + "_2", "A", -1.0f - 1.0f / BR);
                solver.WriteCircuit(net_Ve, "I_" + comp_name + "_2", "A", 1.0f);

                solver.WriteCircuit("I_" + comp_name + "_3", net_Vb, "C", 1.0f / VT / NF);
                solver.WriteCircuit("I_" + comp_name + "_3", net_Ve, "C", -1.0f / VT / NF);
                solver.WriteCircuit("I_" + comp_name + "_3", "", "Is", IS);
                solver.WriteCircuit("I_" + comp_name + "_3", "I_" + comp_name + "_3", "A", -1.0f);
                solver.WriteCircuit(net_Vb, "I_" + comp_name + "_3", "A", 1.0f / BF);
                solver.WriteCircuit(net_Ve, "I_" + comp_name + "_3", "A", -1.0f - 1.0f / BF);
                solver.WriteCircuit(net_Vc, "I_" + comp_name + "_3", "A", 1.0f);
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
