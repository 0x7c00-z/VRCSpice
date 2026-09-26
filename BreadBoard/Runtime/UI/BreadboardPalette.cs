using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardPalette : UdonSharpBehaviour
{
    public BreadboardCatalog catalog;
    public BreadboardLayout layout;
    public BreadboardController controller;
    public TMP_Text selectionLabel, statusLabel, supplyLabel;
    public Renderer[] buttons;
    public TMP_Text[] buttonLabels;
    public Vector3[] buttonCenters;
    public Material normalMaterial, selectedMaterial, hoverMaterial;
    public Vector2 buttonHalfSize = new Vector2(.023f,.013f);
    [HideInInspector] public int kind = 1, orientation, wireLength = 3, model = -1, mode;
    [HideInInspector] public float value = 1000f;
    [HideInInspector] public int selectedId;
    // 0 home, 2 flat parts list, 3 models, 4 supply controls.
    [HideInInspector] public int page, listPage;
    private int hover = -1;
    private string lastStatus;
    private int[] choices = new int[18];

    public bool InPanel(Vector3 point)
    {
        return point.x >= -.32f && point.x <= 0f && point.z >= .02f && point.z <= .19f && point.y >= -.004f && point.y <= .1f;
    }
    public int HitButton(Vector3 point)
    {
        if (!InPanel(point)) return -1;
        for (int i = 0; i < buttonCenters.Length; i++)
        {
            Vector3 d = point - buttonCenters[i];
            if (buttonLabels[i].text != "" && Mathf.Abs(d.x) <= buttonHalfSize.x && Mathf.Abs(d.z) <= buttonHalfSize.y) return i;
        }
        return -1;
    }
    public void Hover(int index) { if (hover != index) { hover = index; RefreshButtons(); } }
    private void Caption(int index, string text) { buttonLabels[index].text = text; }
    public void Refresh()
    {
        for (int i = 0; i < buttons.Length; i++) Caption(i, "");
        selectionLabel.text = catalog.kinds[kind] + " | " + catalog.ParameterLabel(kind,value,model,wireLength) +
            " | " + orientation * 90 + " deg" + (selectedId > 0 ? " | #" + selectedId : "");
        supplyLabel.text = "RED: " + controller.state.supplyVoltage.ToString("0.###") + "V   BLUE: GND";
        if (page == 0)
        {
            Caption(0,"Parts >"); if (kind == 0) Caption(1,"Two-point"); else if (kind >= 4) Caption(1,"Model >");
            Caption(2,"Supply >"); if (kind == 0) Caption(5,"Cancel wire");
            if (CanStepValue()) { Caption(3,"Value +"); Caption(9,"Value -"); }
            Caption(6,"Place"); Caption(7,"Erase"); Caption(8,"Edit");
            if (kind == 0 && mode == 2) { Caption(9,"Shorter"); Caption(10,"Longer"); }
            Caption(12,"CH1"); Caption(13,"CH2"); Caption(14,"Clear CH1"); Caption(15,"Clear CH2");
            if (mode >= 3 && controller.probes != null)
                selectionLabel.text = controller.probes.Label(0) + " | " + controller.probes.Label(1) + " | Choose hole";
        }
        else if (page == 4)
        {
            selectionLabel.text = "Supply | " + controller.state.supplyVoltage.ToString("0.###") + " V | 1 V / step";
            Caption(3,"Voltage +"); Caption(9,"Voltage -"); Caption(18,"Back");
        }
        else
        {
            int count = 0;
            for (int i = 0; i < choices.Length; i++) choices[i] = -1;
            int sourceCount = page == 2 ? catalog.kinds.Length : catalog.modelIds.Length;
            for (int i = 0; i < sourceCount; i++)
            {
                if (page == 3 && catalog.modelKinds[i] != kind) continue;
                int slot = count++ - listPage * 18;
                if (slot < 0 || slot >= 18) continue;
                choices[slot] = i;
                Caption(slot,page == 2 ? catalog.kindLabels[i] : catalog.modelLabels[i]);
            }
            selectionLabel.text = (page == 2 ? "Parts" : "Models") + " | Page " + (listPage+1);
            Caption(18,"Back"); if (listPage > 0) Caption(19,"Previous"); if (count > (listPage+1)*18) Caption(20,"Next"); Caption(23,"Close");
        }
        RefreshButtons();
    }
    private void RefreshButtons()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].enabled = buttonLabels[i].text != "";
            bool selected = page == 0 && (i == (mode < 3 ? mode+6 : mode+9));
            buttons[i].sharedMaterial = i == hover ? hoverMaterial : (selected ? selectedMaterial : normalMaterial);
        }
    }
    public void SetStatus(string text) { if (text != lastStatus) { statusLabel.text = text; lastStatus = text; } }
    public bool CanStepValue()
    {
        return page == 4 || (page == 0 && kind >= 1 && kind <= 3 && (mode == 0 || mode == 2 && selectedId > 0));
    }
    public void StepValue(int direction)
    {
        if (!CanStepValue() || !controller.circuitSync.CanEdit()) return;
        if (page == 4) controller.EditSupply(controller.state.supplyVoltage + (direction > 0 ? 1f : -1f));
        else
        {
            value = catalog.NextValue(kind,value,direction);
            if (mode == 2 && selectedId > 0) controller.EditSelected(false);
        }
        Refresh();
    }
    public void Click(int index)
    {
        if (index < 0 || index >= buttons.Length || buttonLabels[index].text == "") return;
        controller.ResetValueAxis();
        if (page == 4)
        {
            if (index == 3 || index == 9) StepValue(index == 3 ? 1 : -1);
            else if (index == 18) page = 0;
        }
        else if (page != 0)
        {
            if (index == 23) page = 0;
            else if (index == 18) { page = 0; listPage = 0; }
            else if (index == 19) listPage = Mathf.Max(0,listPage-1);
            else if (index == 20) listPage++;
            else if (index < 18 && choices[index] >= 0)
            {
                int selected = choices[index];
                if (page == 2)
                { kind = selected; value = catalog.DefaultValue(kind); model = catalog.DefaultModel(kind); selectedId = 0; mode = 0; page = 0; }
                else { model = selected; if (mode == 2 && selectedId > 0) controller.EditSelected(false); page = 0; }
            }
        }
        else
        {
            if (index == 0) { page = 2; listPage = 0; controller.CancelWire(); }
            else if (index == 1)
            {
                if (kind >= 4) { page = 3; listPage = 0; }
                else controller.ShowFeedback("Wire: choose start, then end hole");
            }
            else if (index == 2) { page = 4; controller.CancelWire(); }
            else if ((index == 3 || index == 9) && kind >= 1 && kind <= 3) StepValue(index == 3 ? 1 : -1);
            else if (index == 5) controller.CancelWire();
            else if (index >= 6 && index <= 8) { mode = index-6; selectedId = 0; controller.CancelWire(); }
            else if (index == 9 || index == 10)
            { wireLength = Mathf.Clamp(wireLength + (index == 9 ? -1 : 1),1,30); if (selectedId > 0) controller.EditSelected(true); }
            else if (index == 12 || index == 13) { mode = index-9; selectedId = 0; controller.CancelWire(); }
            else if ((index == 14 || index == 15) && controller.probes != null) controller.circuitSync.SetProbe(index-14,-1);
        }
        Refresh();
    }
    public void LoadSlot(int slot)
    {
        BreadboardState state = controller.state;
        if (slot < 0 || slot >= state.count) { selectedId = 0; Refresh(); return; }
        selectedId = state.ids[slot]; kind = state.kinds[slot]; orientation = state.orientations[slot];
        value = state.values[slot]; model = state.models[slot];
        if (kind == 0) wireLength = state.lengths[slot];
        Refresh();
    }
}
