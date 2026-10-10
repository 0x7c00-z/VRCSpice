#ifndef SOLVER_VARIABLES_H_INCLUDED
#define SOLVER_VARIABLES_H_INCLUDED

uint _DATA_N;          // Physical MNA vector length.
uint _SYSTEM_N;        // Newton vector length: N for BE, 3N for Radau IIA5.
uint _STAGE_COUNT;     // 1 for BE, 3 for Radau IIA5.
uint _IntegrationScheme;
uint _MaxNewtonIterations;
uint _SettingsRevision;
uint _ClearHistoryRevision;
uint _RestartRevision;
float _OutputDeltaTime;

Texture2D<uint> _MainTex;
float4 _MainTex_TexelSize;

uint SolverLoadUInt(uint2 pixel)
{
    return _MainTex.Load(int3(pixel, 0));
}

float SolverLoadFloat(uint2 pixel)
{
    return asfloat(SolverLoadUInt(pixel));
}

uint SolverStoreUInt(uint value) { return value; }
uint SolverStoreFloat(float value) { return asuint(value); }

// Header row. All normalized time values are dimensionless fractions of
// _OutputDeltaTime. Output samples are uniformly spaced even though accepted
// internal steps are adaptive.
#define OFFSET_SOLVER_STATE          uint2(0, 0)
#define OFFSET_LOOP_COUNTER          uint2(1, 0)
#define OFFSET_NR_ITER_N             uint2(2, 0)
#define OFFSET_NORMALIZED_PROGRESS   uint2(3, 0)
#define OFFSET_NORMALIZED_STEP       uint2(4, 0)
#define OFFSET_REJECT_COUNT          uint2(5, 0)
#define OFFSET_FAILURE_FLAG          uint2(6, 0)
#define OFFSET_CURRENT_TIME          uint2(7, 0)
#define OFFSET_APPLIED_SETTINGS      uint2(8, 0)
#define OFFSET_APPLIED_HISTORY       uint2(9, 0)
#define OFFSET_OUTPUT_COUNT          uint2(10, 0)
#define OFFSET_SCHEME                uint2(11, 0)
#define OFFSET_LAST_OUTPUT_TIME      uint2(12, 0)
#define OFFSET_OUTPUT_SEQUENCE       uint2(13, 0)
#define OFFSET_APPLIED_RESTART       uint2(14, 0)

#define OFFSET_COMMITTED_VECTOR      uint2(0, 1)
#define OFFSET_STAGE_VECTOR          uint2(0, 2)
#define OFFSET_JACOBIAN_MATRIX       uint2(0, 3)
#define OFFSET_NEWTON_VECTOR         uint2(0, 3 + 3 * _DATA_N)
#define OFFSET_OUT_BUFFER            uint2(0, 4 + 3 * _DATA_N)

#define STATE_INITIALIZE             0u
#define STATE_PREPARE_STEP           1u
#define STATE_BUILD_SYSTEM           2u
#define STATE_LU_DECOMPOSITION       3u
#define STATE_SOLVE_VECTOR           4u
#define STATE_UPDATE_NEWTON          5u
#define STATE_ACCEPT_STEP            6u
#define STATE_REJECT_STEP            7u
#define STATE_PUSH_OUTPUT            8u
#define STATE_SETTINGS_RESET         9u
#define STATE_SOLVER_ERROR           10u

#define FAILURE_NONE                 0u
#define FAILURE_NONFINITE            1u
#define FAILURE_SINGULAR             2u
#define FAILURE_NEWTON               3u

#define MAX_REJECT_COUNT             20u
#define NEWTON_PRECISION             1e-6
#define PIVOT_MINIMUM                1e-20
#define OUTPUT_BUFFER_LENGTH         1000u

#define STATE SolverLoadUInt(OFFSET_SOLVER_STATE)
#define LOOP_I SolverLoadUInt(OFFSET_LOOP_COUNTER)

float get_data(uint historyIndex, uint variable)
{
    return SolverLoadFloat(OFFSET_OUT_BUFFER + uint2(variable, historyIndex));
}

float get_data_i_data_im1_timestep(uint historyIndex)
{
    return SolverLoadFloat(OFFSET_OUT_BUFFER + uint2(_DATA_N, historyIndex));
}

#endif
