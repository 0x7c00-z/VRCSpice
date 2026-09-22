#ifndef SOLVER_MODEL_H_INCLUDED
#define SOLVER_MODEL_H_INCLUDED

#include "Solver_variables.hlsl"

//Inputs
Texture2D _A;
Texture2D _B;
Texture2D<float2> _C;
Texture2D _Is;
Texture2D _rhs;

uint C_LoadUInt32(uint2 pixel)
{
    uint2 parts = (uint2) round(
        _C.Load(int3(pixel, 0)) * 65535.0);

    return parts.x | (parts.y << 16);
}

float C_LoadFloat(uint2 pixel)
{
    return asfloat(C_LoadUInt32(pixel));
}

float C_LoadX(uint2 pixel)
{
    uint ind = C_LoadUInt32(pixel);
    return (ind == 0xFFFFFFFF) ? 0 : SolverLoadFloat(OFFSET_NR_VECTOR + uint2(ind, 0));
}

float C_LoadXdot(uint2 pixel)
{
    uint ind = C_LoadUInt32(pixel);
    return (ind == 0xFFFFFFFF) ? 0 : SolverLoadFloat(OFFSET_XDOT_VECTOR + uint2(ind, 0));
}

float f(uint index)
{
    // Linear terms: A*x + B*xdot - rhs.
    float data = -_rhs[uint2(index, 0)].r;
    [loop]
    for (uint i = 0; i < _DATA_N; i++)
    {
        data += _A[uint2(index, i)].r * SolverLoadFloat(OFFSET_NR_VECTOR + uint2(i, 0)) + _B[uint2(index, i)].r * SolverLoadFloat(OFFSET_XDOT_VECTOR + uint2(i, 0));
    }

    switch (C_LoadUInt32(uint2(0, index)))
    {
        case 0:
            break; // do nothing
        case 1:
        {
            // Row layout: [1] float IS, [2] float 1/nVt, [3] uint cathode [4] uint anode
            float a = C_LoadFloat(uint2(1, index));
            float b = C_LoadFloat(uint2(2, index));
            float v1 = C_LoadX(uint2(3, index));
            float v2 = C_LoadX(uint2(4, index));
            data += a * (exp(b * (v2 - v1)) - 1.0);
            break;
        }
    }

    return data;
}

float dfidxj(uint i, uint j)
{
    float data = _A[uint2(i, j)].r;
    switch (C_LoadUInt32(uint2(0, i)))
    {
        case 0:
            break;
        case 1:
        {
            uint v1Index = C_LoadUInt32(uint2(3, i));
            uint v2Index = C_LoadUInt32(uint2(4, i));
            // d(v2 - v1)/dx[j]; equal endpoints cancel exactly.
            float direction = (j == v2Index ? 1.0 : 0.0) - (j == v1Index ? 1.0 : 0.0);
            if (direction != 0.0)
            {
                float a = C_LoadFloat(uint2(1, i));
                float b = C_LoadFloat(uint2(2, i));
                float v1 = C_LoadX(uint2(3, i));
                float v2 = C_LoadX(uint2(4, i));
                data += direction * a * b * exp(b * (v2 - v1));
            }
            break;
        }
    }
    return data;
}

float dfidxdotj(uint i, uint j)
{
    float data = 0;
    data = _B[uint2(i, j)].r;
    return data;
}

#endif
