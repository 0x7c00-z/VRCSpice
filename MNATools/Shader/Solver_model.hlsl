#ifndef SOLVER_MODEL_H_INCLUDED
#define SOLVER_MODEL_H_INCLUDED

#include "Solver_variables.hlsl"

//Inputs
Texture2D _A;
Texture2D _B;
Texture2D _C;
Texture2D _Is;
Texture2D _rhs;

float f(uint index)
{
    //$$A\mathbf{x}+B\mathbf{\dot{x}}+\mathbf{I_s}\odot\left[\exp\left(C\mathbf{x}\right)-1\right]-\mathbf{b_{rhs}} = f(x, xdot)$$
    float data = -_rhs[uint2(index, 0)].r;
    float exponent = 0;
        [loop]
    for (uint i = 0; i < _DATA_N; i++)
    {
        //data += _A[uint2(index, i)].r * SolverLoadFloat(OFFSET_NR_VECTOR + uint2(i, 0)) + _B[uint2(index, i)].r * (SolverLoadFloat(OFFSET_NR_VECTOR + uint2(i, 0)) - SolverLoadFloat(OFFSET_OUT_BUFFER + uint2(i, 0))) / _DeltaTime;
        data += _A[uint2(index, i)].r * SolverLoadFloat(OFFSET_NR_VECTOR + uint2(i, 0)) + _B[uint2(index, i)].r * (SolverLoadFloat(OFFSET_XDOT_VECTOR + uint2(i, 0)));
        
        exponent += _C[uint2(index, i)].r * SolverLoadFloat(OFFSET_NR_VECTOR + uint2(i, 0));
    }
    data += _Is[uint2(index, 0)].r * (exp(exponent) - 1.0);
    return data;
}

float dfidxj(uint i, uint j)
{
    float exponent = 0;
    for(uint k = 0; k < _DATA_N; k++)
    {
        exponent += _C[uint2(i, k)].r * SolverLoadFloat(OFFSET_NR_VECTOR + uint2(k, 0));
    }
    float data = _A[uint2(i, j)] + _C[uint2(i, j)].r * _Is[uint2(i, 0)].r * exp(exponent);
    return data;
}

float dfidxdotj(uint i, uint j)
{
    float data = 0;
    data = _B[uint2(i, j)].r;
    return data;
}

#endif