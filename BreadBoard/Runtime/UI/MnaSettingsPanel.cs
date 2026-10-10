using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class MnaSettingsPanel : UdonSharpBehaviour
{
    public BreadboardState state;
    public BreadboardSync circuitSync;
    public MNASolve solver;
    public TMP_Dropdown schemeDropdown;
    public TMP_InputField maxDeltaTimeInput;
    public TMP_InputField maxNewtonIterationsInput;
    public TMP_Text statusText;
    private int observedScheme = -1;
    private string observedTime = "";
    private string observedNewton = "";
    private int pendingScheme = -1;
    private float pendingTime;
    private int pendingNewton;
    private float nextRequestTime;
    private int pendingRestartRevision = -1;
    private float nextRestartRequestTime;
    private float timeInputChangedAt = float.MaxValue;
    private float newtonInputChangedAt = float.MaxValue;

    private void Start() { Refresh(); }

    private bool SharedMode() { return state != null && circuitSync != null; }
    private int CurrentScheme() { return SharedMode() ? state.integrationScheme : (solver == null ? 0 : solver.GetIntegrationScheme()); }
    private float CurrentTime() { return SharedMode() ? state.maxDeltaTime : (solver == null ? 0.01f : solver.GetOutputDeltaTime()); }
    private int CurrentNewton() { return SharedMode() ? state.maxNewtonIterations : (solver == null ? 32 : solver.GetMaxNewtonIterations()); }

    private void Update()
    {
        if (SharedMode()) state.Initialize();
        else if (solver == null) return;

        if (schemeDropdown != null && schemeDropdown.value != observedScheme)
        {
            observedScheme = schemeDropdown.value;
            QueueRequest(observedScheme, CurrentTime(), CurrentNewton());
        }
        if (maxDeltaTimeInput != null && maxDeltaTimeInput.text != observedTime)
        {
            observedTime = maxDeltaTimeInput.text;
            timeInputChangedAt = Time.time;
        }
        if (maxNewtonIterationsInput != null && maxNewtonIterationsInput.text != observedNewton)
        {
            observedNewton = maxNewtonIterationsInput.text;
            newtonInputChangedAt = Time.time;
        }
        if (maxDeltaTimeInput != null && Time.time >= timeInputChangedAt + 0.5f && !maxDeltaTimeInput.isFocused)
        {
            float value;
            if (float.TryParse(maxDeltaTimeInput.text, out value) && !float.IsNaN(value) &&
                !float.IsInfinity(value) && value > 0f)
                QueueRequest(CurrentScheme(), value, CurrentNewton());
            else Refresh();
            timeInputChangedAt = float.MaxValue;
        }
        if (maxNewtonIterationsInput != null && Time.time >= newtonInputChangedAt + 0.5f && !maxNewtonIterationsInput.isFocused)
        {
            int value;
            if (int.TryParse(maxNewtonIterationsInput.text, out value) && value >= 1 && value <= 256)
                QueueRequest(CurrentScheme(), CurrentTime(), value);
            else Refresh();
            newtonInputChangedAt = float.MaxValue;
        }

        if (pendingRestartRevision >= 0)
        {
            if (state.solverRestartRevision != pendingRestartRevision)
                pendingRestartRevision = -1;
            else if (Time.time >= nextRestartRequestTime)
            {
                nextRestartRequestTime = Time.time + 1f;
                circuitSync.RequestSimulationRestart();
            }
        }

        if (pendingScheme >= 0)
        {
            if (CurrentScheme() == pendingScheme && CurrentTime() == pendingTime && CurrentNewton() == pendingNewton)
            { pendingScheme = -1; Refresh(); }
            else if (Time.time >= nextRequestTime)
            {
                nextRequestTime = Time.time + 1f;
                if (SharedMode()) circuitSync.RequestSolverSettings(pendingScheme, pendingTime, pendingNewton);
                else solver.SetSolverSettings(pendingScheme, pendingTime, pendingNewton);
            }
        }

        if (statusText != null)
        {
            string method = CurrentScheme() == 1 ? "BACKWARD EULER" : "RADAU IIA 5";
            string authority = SharedMode() ? circuitSync.StatusText() : "LOCAL SOLVER";
            statusText.text = method + "  max_dt=" + CurrentTime().ToString("G6") +
                "  NR=" + CurrentNewton() + "\n" + authority;
        }
        if (pendingScheme < 0 && InputsIdle())
        {
            string canonicalTime = CurrentTime().ToString("G9");
            string canonicalNewton = CurrentNewton().ToString();
            if ((schemeDropdown != null && schemeDropdown.value != CurrentScheme()) ||
                (maxDeltaTimeInput != null && maxDeltaTimeInput.text != canonicalTime) ||
                (maxNewtonIterationsInput != null && maxNewtonIterationsInput.text != canonicalNewton)) Refresh();
        }
    }

    private bool InputsIdle()
    {
        return (maxDeltaTimeInput == null || !maxDeltaTimeInput.isFocused) &&
            (maxNewtonIterationsInput == null || !maxNewtonIterationsInput.isFocused);
    }

    private void QueueRequest(int scheme, float outputDeltaTime, int newtonIterations)
    {
        if ((scheme != 0 && scheme != 1) || float.IsNaN(outputDeltaTime) ||
            float.IsInfinity(outputDeltaTime) || outputDeltaTime <= 0f ||
            newtonIterations < 1 || newtonIterations > 256) { Refresh(); return; }
        pendingScheme = scheme; pendingTime = outputDeltaTime;
        pendingNewton = newtonIterations; nextRequestTime = 0f;
    }

    public void RestartSimulation()
    {
        if (SharedMode())
        {
            state.Initialize();
            pendingRestartRevision = state.solverRestartRevision;
            nextRestartRequestTime = 0f;
            circuitSync.RequestSimulationRestart();
        }
        else if (solver != null) solver.RestartSimulation();
    }

    public void ChangeSettings()
    {
        float time = CurrentTime(); int iterations = CurrentNewton();
        if (maxDeltaTimeInput != null && !float.TryParse(maxDeltaTimeInput.text, out time)) { Refresh(); return; }
        if (maxNewtonIterationsInput != null && !int.TryParse(maxNewtonIterationsInput.text, out iterations)) { Refresh(); return; }
        QueueRequest(schemeDropdown == null ? 0 : schemeDropdown.value, time, iterations);
    }

    public void Refresh()
    {
        if (SharedMode()) state.Initialize();
        else if (solver == null) return;
        if (schemeDropdown != null) { schemeDropdown.value = CurrentScheme(); observedScheme = schemeDropdown.value; }
        if (maxDeltaTimeInput != null)
        {
            maxDeltaTimeInput.text = CurrentTime().ToString("G9"); observedTime = maxDeltaTimeInput.text;
        }
        if (maxNewtonIterationsInput != null)
        {
            maxNewtonIterationsInput.text = CurrentNewton().ToString(); observedNewton = maxNewtonIterationsInput.text;
        }
        timeInputChangedAt = float.MaxValue; newtonInputChangedAt = float.MaxValue;
    }
}
