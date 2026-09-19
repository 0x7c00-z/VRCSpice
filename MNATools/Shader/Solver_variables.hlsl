#ifndef SOLVER_VARIABLES_H_INCLUDED
#define SOLVER_VARIABLES_H_INCLUDED

uint _DATA_N; //size of the vector to be solved
float _DeltaTime; //initial time step
float _MaxPCError;

// R32_UInt storage: control fields are native uint; numerical fields hold float bits.
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

// Store helpers encode the value returned by a uint SV_Target fragment shader.
// Copy unchanged texels with SolverLoadUInt to preserve every bit.
uint SolverStoreUInt(uint value)
{
    return value;
}

uint SolverStoreFloat(float value)
{
    return asuint(value);
}

#define OFFSET_SOLVER_STATE uint2(0, 0) //uint solver state
#define OFFSET_LOOP_COUNTER uint2(1, 0) //uint loop counter
#define OFFSET_TIME_STEP uint2(2, 0)
#define OFFSET_STEPS_N uint2(3, 0) //uint step counter
#define OFFSET_STEP_ORDER uint2(4, 0) //uint step order, set before xdot generation
#define OFFSET_COF_PRE_N uint2(5, 0)
#define OFFSET_COF_PRE_NM1 uint2(6, 0)
#define OFFSET_COF_PRE_NM2 uint2(7, 0)
#define OFFSET_COF_COR_NP1 uint2(8, 0)
#define OFFSET_COF_COR_N uint2(9, 0)
#define OFFSET_COF_COR_NM1 uint2(10, 0)
#define OFFSET_NR_ITER_N uint2 (11, 0)
#define OFFSET_XDOT_VECTOR uint2(0, 1)
#define OFFSET_PREDICTOR_VECTOR uint2(0, 2)
#define OFFSET_YACOBI_MATRIX uint2(0, 3)
#define OFFSET_INVERSE_VECTOR uint2(0, 3 +_DATA_N) //vector to store the inverse of the diagonal of the yacobi matrix, used for solving the vector
#define OFFSET_NR_VECTOR uint2(0, 4 + _DATA_N) //vector to be solved and manipulated while LU decomposition and solving
#define OFFSET_OUT_BUFFER uint2(0, 5 + _DATA_N) //assuming the output buffer is stored after the vector in the texture, You can get last step output by using OFFSET_OUT_BUFFER + uint2(0, 0) and OFFSET_OUT_BUFFER + uint2(0, 1) is older

#define LOOP_I SolverLoadUInt(OFFSET_LOOP_COUNTER)
#define STATE SolverLoadUInt(OFFSET_SOLVER_STATE)
#define STEP_ORDER SolverLoadUInt(OFFSET_STEP_ORDER)

#endif