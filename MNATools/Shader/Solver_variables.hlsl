#ifndef SOLVER_VARIABLES_H_INCLUDED
#define SOLVER_VARIABLES_H_INCLUDED

uint _DATA_N; //size of the vector to be solved
float _DeltaTime; //initial time step

Texture2D _MainTex; //texture containing the flowcontrol, matrix and vector data
float4 _MainTex_TexelSize;
//use _MainTex[uint2(row, col) + offset] to get RFloat data, where offset is the offset to the flowcontrol, matrix and vector data respectively
#define OFFSET_SOLVER_STATE uint2(0, 0) //1 pixel to store the current state of the solver, use this to control the flow of the solver in the shader, use asuint() to get the integer value of the state, and use this value to control the flow of the solver in the shader
#define OFFSET_LOOP_COUNTER uint2(1, 0) //1 pixel to store the current loop counter, use this to control the flow of the solver in the shader, use asuint() to get the integer value of the loop counter
#define OFFSET_TIME_STEP uint2(2, 0)
#define OFFSET_STEPS_N uint2(3, 0) //Step counter
#define OFFSET_STEP_ORDER uint2(4, 0) //Step order, must be set until xdot generation
#define OFFSET_COF_PRE_N uint2(5, 0)
#define OFFSET_COF_PRE_NM1 uint2(6, 0)
#define OFFSET_COF_PRE_NM2 uint2(7, 0)
#define OFFSET_COF_COR_NP1 uint2(8, 0)
#define OFFSET_COF_COR_N uint2(9, 0)
#define OFFSET_COF_COR_NM1 uint2(10, 0)
#define OFFSET_XDOT_VECTOR uint2(0, 1)
#define OFFSET_PREDICTOR_VECTOR uint2(0, 2)
#define OFFSET_YACOBI_MATRIX uint2(0, 3)
#define OFFSET_INVERSE_VECTOR uint2(0, 3 +_DATA_N) //vector to store the inverse of the diagonal of the yacobi matrix, used for solving the vector
#define OFFSET_NR_VECTOR uint2(0, 4 + _DATA_N) //vector to be solved and manipulated while LU decomposition and solving
#define OFFSET_OUT_BUFFER uint2(0, 5 + _DATA_N) //assuming the output buffer is stored after the vector in the texture, You can get last step output by using OFFSET_OUT_BUFFER + uint2(0, 0) and OFFSET_OUT_BUFFER + uint2(0, 1) is older

#define LOOP_I asuint(_MainTex[OFFSET_LOOP_COUNTER].r)
#define STATE asuint(_MainTex[OFFSET_SOLVER_STATE].r)

#endif