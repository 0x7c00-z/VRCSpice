
using System;
using System.ComponentModel;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using static BestHTTP.SecureProtocol.Org.BouncyCastle.Math.EC.ECCurve;

public class Test : UdonSharpBehaviour
{
    //[SerializeField] TMP_InputField netlistfield;
    [SerializeField] TMP_InputField probefield1;
    [SerializeField] TMP_InputField probefield2;
    [SerializeField] TMP_InputField yscalefield;
    [SerializeField] TMP_InputField xscalefield;
    [SerializeField] TMP_InputField stepsPerFrame;
    [SerializeField] TMP_InputField maxError;
    private MNAGen generator;
    private MNASolve solver;
    private int oscind1 = int.MinValue; // -1: hidden, -2: GND, >=0: buffer row
    private int oscind2 = int.MinValue;

    void Start()
    {
        solver = this.gameObject.GetComponent<MNASolve>();
        generator = this.gameObject.GetComponent<MNAGen>();
    }

    void Update() {
        int next1 = probefield1.text == "GND" ? -2 : solver.label2bufferRow(probefield1.text);
        int next2 = probefield2.text == "GND" ? -2 : solver.label2bufferRow(probefield2.text);
        if (oscind1 != next1) { //if changed
            oscind1 = next1;
            this.GetComponent<Renderer>().material.SetInteger("_Row1", oscind1);
            Debug.Log("osc ind : " + oscind1);
        }
        if (oscind2 != next2)
        { //if changed
            oscind2 = next2;
            this.GetComponent<Renderer>().material.SetInteger("_Row2", oscind2);
            Debug.Log("osc ind : " + oscind2);
        }
        float ydiv = 1;
        float xdiv = 1;
        float.TryParse(yscalefield.text, out ydiv);
        float.TryParse(xscalefield.text, out xdiv);
        solver.WriteToMaterial(this.GetComponent<Renderer>().material);

        if (ydiv > 0) {
            this.GetComponent<Renderer>().material.SetFloat("_YScale", 0.1f / ydiv);
        }
        if (xdiv > 0)
        {
            this.GetComponent<Renderer>().material.SetFloat("_XScale", 0.1f / xdiv);
            solver.SetMaxDeltaTime(xdiv);
        }

        int steps = 100;
        float maxerror = 0.05f;
        int.TryParse(stepsPerFrame.text, out steps);
        float.TryParse(maxError.text, out maxerror);
        if (steps > 0) {
            solver.SetStepPerFrame(steps);
            solver.SetPCError(maxerror);
        }
    }

    /*public void restart() {
        //split into lines
        string[] lines = netlistfield.text.Split("\n");
        generator.netlist.Clear();

        for (int i = 0; i < lines.Length; i++) { // for each line
            if (lines[i].Length == 0) continue;

            string[] elemants = lines[i].Split(" ");
            int num_nets = 0;
            int num_consts = 0;

            switch (elemants[0].Substring(0, 1))
            {
                case "R":
                    num_nets = 2;
                    num_consts = 1;
                    break;
                case "C":
                    num_nets = 2;
                    num_consts = 1;
                    break;
                case "V":
                    num_nets = 2;
                    num_consts = 1;
                    break;
                case "L":
                    num_nets = 2;
                    num_consts = 1;
                    break;
                case "D":
                    num_nets = 2;
                    num_consts = 2;
                    break;
                case "Q":
                    num_nets = 3;
                    num_consts = 0;
                    break;
                case "I":
                    num_nets = 2;
                    num_consts = 1;
                    break;
            }

            //add to netlist
            if(elemants.Length >= 1 + num_consts + num_nets && num_nets != 0)
            {
                DataList component = new DataList();
                component.Clear();
                component = new DataList();
                component.Add(elemants[0]);
                component.Add(new DataList());
                component.Add(new DataList());
                ((DataList)component[1]).Clear();
                ((DataList)component[2]).Clear();
                for (int j = 0; j < num_nets; j++)
                {
                    ((DataList)component[1]).Add(elemants[1 + j]);
                }
                for (int j = 0; j < num_consts; j++)
                {
                    ((DataList)component[2]).Add(float.Parse(elemants[1 + num_nets + j]));
                }
                generator.netlist.Add(component);
            }
        }

        generator.UpdateMNA();
    }*/
}
