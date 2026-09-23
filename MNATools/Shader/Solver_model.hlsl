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
            //Diode
            // Row layout: [1] float IS, [2] float 1/nVt, [3] uint cathode [4] uint anode
                float Is = C_LoadFloat(uint2(1, index));
                float nVtinv = C_LoadFloat(uint2(2, index));
                float v = C_LoadX(uint2(4, index)) - C_LoadX(uint2(3, index));
                float vdot = C_LoadXdot(uint2(4, index)) - C_LoadXdot(uint2(3, index));
                float TT = 2e-9;
                float Cjo = 2e-12;
                float Vj = 0.6;
                float m = 0.5;
                float Fc = 0.5;
            
                float Cj = Cjo * ((v < Fc * Vj) ?
                    pow(1 - v / Vj, -m) :
                    pow(1 - Fc, -m - 1.0) * (1 - Fc * (1.0 + m) + m * v / Vj)
                );
                data += Is * (exp(v * nVtinv) - 1.0)
                    + (Cj + TT * Is * nVtinv * exp(v * nVtinv)) * vdot;
                break;
            }
        case 2:
        {
            // NPN GP core, ROHM Figure 2 topology (CBE, intrinsic CBC, RB/RC/RE).
            // [1] 0=Ic / 1=Ib, [2..4] C'/B'/E', [5..27] packed MNAGen parameters.
            bool baseRow = C_LoadUInt32(uint2(1, index)) != 0u;
            float u = C_LoadX(uint2(3, index)) - C_LoadX(uint2(4, index));
            float w = C_LoadX(uint2(3, index)) - C_LoadX(uint2(2, index));
            float udot = C_LoadXdot(uint2(3, index)) - C_LoadXdot(uint2(4, index));
            float wdot = C_LoadXdot(uint2(3, index)) - C_LoadXdot(uint2(2, index));
            float IS = C_LoadFloat(uint2(5, index));
            float invBF = C_LoadFloat(uint2(6, index));
            float invNfVt = C_LoadFloat(uint2(7, index));
            float invBR = C_LoadFloat(uint2(8, index));
            float invNrVt = C_LoadFloat(uint2(9, index));
            float ISE = C_LoadFloat(uint2(10, index));
            float invNeVt = C_LoadFloat(uint2(11, index));
            float ISC = C_LoadFloat(uint2(12, index));
            float invNcVt = C_LoadFloat(uint2(13, index));
            float invVAF = C_LoadFloat(uint2(14, index));
            float invVAR = C_LoadFloat(uint2(15, index));
            float invIKF = C_LoadFloat(uint2(16, index));
            float invIKR = C_LoadFloat(uint2(17, index));
            float CJE = C_LoadFloat(uint2(18, index));
            float VJE = C_LoadFloat(uint2(19, index));
            float MJE = C_LoadFloat(uint2(20, index));
            float CJC = C_LoadFloat(uint2(21, index));
            float VJC = C_LoadFloat(uint2(22, index));
            float MJC = C_LoadFloat(uint2(23, index));
            float FC = C_LoadFloat(uint2(24, index));
            float TF = C_LoadFloat(uint2(25, index));
            float TR = C_LoadFloat(uint2(26, index));
            float NK = C_LoadFloat(uint2(27, index));
            float expF = exp(u * invNfVt);
            float expR = exp(w * invNrVt);
            float IF = IS * (expF - 1.0);
            float IR = IS * (expR - 1.0);
            float gF = IS * invNfVt * expF;
            float gR = IS * invNrVt * expR;

            // r = 1/qb = a * h(z), z = IF/IKF + IR/IKR.
            // NK=0.5 gives the standard GP square root. All derivatives follow this same r.
            float a = 1.0 - w * invVAF - u * invVAR;
            float z = IF * invIKF + IR * invIKR;
            float s = 1.0 + 4.0 * z;
            float d = pow(s, NK);
            float dp = 4.0 * NK * pow(s, NK - 1.0);
            float invOnePlusD = 1.0 / (1.0 + d);
            float h = 2.0 * invOnePlusD;
            float hp = -2.0 * dp * invOnePlusD * invOnePlusD;
            float zu = gF * invIKF;
            float zw = gR * invIKR;
            float r = a * h;
            float ru = -invVAR * h + a * hp * zu;
            float rw = -invVAF * h + a * hp * zw;

            // CJC here is already multiplied by XCJC; the remainder is a case-3 CJX row.
            float2 junctionCap = float2(0.0, 0.0);
            if (CJE != 0.0)
            {
                if (u < FC * VJE)
                {
                    float oneMinusV = 1.0 - u / VJE;
                    junctionCap.x = CJE * pow(oneMinusV, -MJE);
                }
                else
                {
                    float scale = CJE * pow(1.0 - FC, -MJE - 1.0);
                    junctionCap.x = scale * (1.0 - FC * (1.0 + MJE) + MJE * u / VJE);
                }
            }
            if (CJC != 0.0)
            {
                if (w < FC * VJC)
                {
                    float oneMinusV = 1.0 - w / VJC;
                    junctionCap.y = CJC * pow(oneMinusV, -MJC);
                }
                else
                {
                    float scale = CJC * pow(1.0 - FC, -MJC - 1.0);
                    junctionCap.y = scale * (1.0 - FC * (1.0 + MJC) + MJC * w / VJC);
                }
            }
            // Charge control: QBE = QJE + TF*IF*r, QBC = QJC + TR*IR.
            // QBE depends on both u and w, so retain the cross-capacitance Cx.
            float Ce = junctionCap.x + TF * (gF * r + IF * ru);
            float Cx = TF * IF * rw;
            float Cc = junctionCap.y + TR * gR;
            float Ibe = IF * invBF + ISE * (exp(u * invNeVt) - 1.0);
            float Ibc = IR * invBR + ISC * (exp(w * invNcVt) - 1.0);
            if (baseRow)
                data += Ibe + Ibc + Ce * udot + (Cx + Cc) * wdot;
            else
                data += (IF - IR) * r - Ibc - Cc * wdot;
            break;
        }
        case 3:
        {
            // Nonlinear junction-capacitance branch (CJX or CCS), current positive -> negative.
            // [1..2] node indices, [3..6] C0, VJ, M, FC. FC=0 selects the CCS continuation.
            float C0 = C_LoadFloat(uint2(3, index));
            if (C0 == 0.0) break;
            float v = C_LoadX(uint2(1, index)) - C_LoadX(uint2(2, index));
            float VJ = C_LoadFloat(uint2(4, index));
            float M = C_LoadFloat(uint2(5, index));
            float FC = C_LoadFloat(uint2(6, index));
            float vdot = C_LoadXdot(uint2(1, index)) - C_LoadXdot(uint2(2, index));
            float capacitance;
            if (v < FC * VJ)
                capacitance = C0 * pow(1.0 - v / VJ, -M);
            else
                capacitance = C0 * pow(1.0 - FC, -M - 1.0)
                            * (1.0 - FC * (1.0 + M) + M * v / VJ);
            data += capacitance * vdot;
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
                    float Is = C_LoadFloat(uint2(1, i));
                    float nVtinv = C_LoadFloat(uint2(2, i));
                    float v = C_LoadX(uint2(4, i)) - C_LoadX(uint2(3, i));
                    float vdot = C_LoadXdot(uint2(4, i)) - C_LoadXdot(uint2(3, i));
                    float TT = 2e-9;
                    float Cjo = 2e-12;
                    float Vj = 0.6;
                    float m = 0.5;
                    float Fc = 0.5;
            
                    float Cjdiff = m * Cjo / Vj * ((v < Fc * Vj) ?
                        pow(1.0 - v / Vj, -m - 1.0) :
                        pow(1 - Fc, -m - 1.0)
                    );
                    data += direction * Is * nVtinv * exp(nVtinv * v)
                    + (Cjdiff + TT * Is * nVtinv * nVtinv * exp(nVtinv * v) * vdot);
                }
                break;
            }
        case 2:
        {
            // NPN GP core, ROHM Figure 2 topology (CBE, intrinsic CBC, RB/RC/RE).
            // [1] 0=Ic / 1=Ib, [2..4] C'/B'/E', [5..27] packed MNAGen parameters.
            bool baseRow = C_LoadUInt32(uint2(1, i)) != 0u;
            float u = C_LoadX(uint2(3, i)) - C_LoadX(uint2(4, i));
            float w = C_LoadX(uint2(3, i)) - C_LoadX(uint2(2, i));
            uint cIndex = C_LoadUInt32(uint2(2, i));
            uint bIndex = C_LoadUInt32(uint2(3, i));
            uint eIndex = C_LoadUInt32(uint2(4, i));
            float du = (j == bIndex ? 1.0 : 0.0) - (j == eIndex ? 1.0 : 0.0);
            float dw = (j == bIndex ? 1.0 : 0.0) - (j == cIndex ? 1.0 : 0.0);
            if (du == 0.0 && dw == 0.0) break;
            float udot = C_LoadXdot(uint2(3, i)) - C_LoadXdot(uint2(4, i));
            float wdot = C_LoadXdot(uint2(3, i)) - C_LoadXdot(uint2(2, i));
            float IS = C_LoadFloat(uint2(5, i));
            float invBF = C_LoadFloat(uint2(6, i));
            float invNfVt = C_LoadFloat(uint2(7, i));
            float invBR = C_LoadFloat(uint2(8, i));
            float invNrVt = C_LoadFloat(uint2(9, i));
            float ISE = C_LoadFloat(uint2(10, i));
            float invNeVt = C_LoadFloat(uint2(11, i));
            float ISC = C_LoadFloat(uint2(12, i));
            float invNcVt = C_LoadFloat(uint2(13, i));
            float invVAF = C_LoadFloat(uint2(14, i));
            float invVAR = C_LoadFloat(uint2(15, i));
            float invIKF = C_LoadFloat(uint2(16, i));
            float invIKR = C_LoadFloat(uint2(17, i));
            float CJE = C_LoadFloat(uint2(18, i));
            float VJE = C_LoadFloat(uint2(19, i));
            float MJE = C_LoadFloat(uint2(20, i));
            float CJC = C_LoadFloat(uint2(21, i));
            float VJC = C_LoadFloat(uint2(22, i));
            float MJC = C_LoadFloat(uint2(23, i));
            float FC = C_LoadFloat(uint2(24, i));
            float TF = C_LoadFloat(uint2(25, i));
            float TR = C_LoadFloat(uint2(26, i));
            float NK = C_LoadFloat(uint2(27, i));
            float expF = exp(u * invNfVt);
            float expR = exp(w * invNrVt);
            float IF = IS * (expF - 1.0);
            float IR = IS * (expR - 1.0);
            float gF = IS * invNfVt * expF;
            float gR = IS * invNrVt * expR;

            // r = 1/qb = a * h(z), z = IF/IKF + IR/IKR.
            // NK=0.5 gives the standard GP square root. All derivatives follow this same r.
            float a = 1.0 - w * invVAF - u * invVAR;
            float z = IF * invIKF + IR * invIKR;
            float s = 1.0 + 4.0 * z;
            float d = pow(s, NK);
            float dp = 4.0 * NK * pow(s, NK - 1.0);
            float invOnePlusD = 1.0 / (1.0 + d);
            float h = 2.0 * invOnePlusD;
            float hp = -2.0 * dp * invOnePlusD * invOnePlusD;
            float zu = gF * invIKF;
            float zw = gR * invIKR;
            float r = a * h;
            float ru = -invVAR * h + a * hp * zu;
            float rw = -invVAF * h + a * hp * zw;

            // CJC here is already multiplied by XCJC; the remainder is a case-3 CJX row.
            float2 junctionCap = float2(0.0, 0.0);
            float2 junctionSlope = float2(0.0, 0.0);
            if (CJE != 0.0)
            {
                if (u < FC * VJE)
                {
                    float oneMinusV = 1.0 - u / VJE;
                    junctionCap.x = CJE * pow(oneMinusV, -MJE);
                    junctionSlope.x = MJE * junctionCap.x / (VJE * oneMinusV);
                }
                else
                {
                    float scale = CJE * pow(1.0 - FC, -MJE - 1.0);
                    junctionCap.x = scale * (1.0 - FC * (1.0 + MJE) + MJE * u / VJE);
                    junctionSlope.x = scale * MJE / VJE;
                }
            }
            if (CJC != 0.0)
            {
                if (w < FC * VJC)
                {
                    float oneMinusV = 1.0 - w / VJC;
                    junctionCap.y = CJC * pow(oneMinusV, -MJC);
                    junctionSlope.y = MJC * junctionCap.y / (VJC * oneMinusV);
                }
                else
                {
                    float scale = CJC * pow(1.0 - FC, -MJC - 1.0);
                    junctionCap.y = scale * (1.0 - FC * (1.0 + MJC) + MJC * w / VJC);
                    junctionSlope.y = scale * MJC / VJC;
                }
            }
            float hF = invNfVt * gF;
            float hR = invNrVt * gR;
            float gbe = gF * invBF + ISE * invNeVt * exp(u * invNeVt);
            float gbc = gR * invBR + ISC * invNcVt * exp(w * invNcVt);
            float Ccw = junctionSlope.y + TR * hR;
            if (baseRow)
            {
                // Hessian of 1/qb and of QBE: required by d(C(v)*vdot)/dv.
                float dpp = 16.0 * NK * (NK - 1.0) * pow(s, NK - 2.0);
                float hpp = 4.0 * dp * dp * invOnePlusD * invOnePlusD * invOnePlusD
                          - 2.0 * dpp * invOnePlusD * invOnePlusD;
                float ruu = -2.0 * invVAR * hp * zu + a * (hpp * zu * zu + hp * hF * invIKF);
                float ruw = -invVAR * hp * zw - invVAF * hp * zu + a * hpp * zu * zw;
                float rww = -2.0 * invVAF * hp * zw + a * (hpp * zw * zw + hp * hR * invIKR);
                float Qeuu = junctionSlope.x + TF * (hF * r + 2.0 * gF * ru + IF * ruu);
                float Qeuw = TF * (gF * rw + IF * ruw);
                float Qeww = TF * IF * rww;
                data += (gbe + Qeuu * udot + Qeuw * wdot) * du
                      + (gbc + Qeuw * udot + (Qeww + Ccw) * wdot) * dw;
            }
            else
            {
                float Itu = gF * r + (IF - IR) * ru;
                float Itw = -gR * r + (IF - IR) * rw;
                data += Itu * du + (Itw - gbc - Ccw * wdot) * dw;
            }
            break;
        }
        case 3:
        {
            // Nonlinear junction-capacitance branch (CJX or CCS), current positive -> negative.
            // [1..2] node indices, [3..6] C0, VJ, M, FC. FC=0 selects the CCS continuation.
            float C0 = C_LoadFloat(uint2(3, i));
            if (C0 == 0.0) break;
            float v = C_LoadX(uint2(1, i)) - C_LoadX(uint2(2, i));
            float VJ = C_LoadFloat(uint2(4, i));
            float M = C_LoadFloat(uint2(5, i));
            float FC = C_LoadFloat(uint2(6, i));
            uint positiveIndex = C_LoadUInt32(uint2(1, i));
            uint negativeIndex = C_LoadUInt32(uint2(2, i));
            float direction = (j == positiveIndex ? 1.0 : 0.0) - (j == negativeIndex ? 1.0 : 0.0);
            if (direction == 0.0) break;
            float vdot = C_LoadXdot(uint2(1, i)) - C_LoadXdot(uint2(2, i));
            float dCdv;
            if (v < FC * VJ)
                dCdv = C0 * M / VJ * pow(1.0 - v / VJ, -M - 1.0);
            else
                dCdv = C0 * M / VJ * pow(1.0 - FC, -M - 1.0);
            data += direction * dCdv * vdot;
            break;
        }
    }
    return data;
}

float dfidxdotj(uint i, uint j)
{
    float data = 0;
    data = _B[uint2(i, j)].r;
    
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
                    float Is = C_LoadFloat(uint2(1, i));
                    float nVtinv = C_LoadFloat(uint2(2, i));
                    float v = C_LoadX(uint2(4, i)) - C_LoadX(uint2(3, i));
                    float vdot = C_LoadXdot(uint2(4, i)) - C_LoadXdot(uint2(3, i));
                    float TT = 2e-9;
                    float Cjo = 2e-12;
                    float Vj = 0.6;
                    float m = 0.5;
                    float Fc = 0.5;
            
                    float Cj = Cjo * ((v < Fc * Vj) ?
                        pow(1 - v / Vj, -m) :
                        pow(1 - Fc, -m - 1.0) * (1 - Fc * (1.0 + m) + m * v / Vj)
                    );
                    data += Cj + TT * Is * nVtinv * exp(v * nVtinv);
                }
                break;
            }
        case 2:
        {
            // NPN GP core, ROHM Figure 2 topology (CBE, intrinsic CBC, RB/RC/RE).
            // [1] 0=Ic / 1=Ib, [2..4] C'/B'/E', [5..27] packed MNAGen parameters.
            bool baseRow = C_LoadUInt32(uint2(1, i)) != 0u;
            float u = C_LoadX(uint2(3, i)) - C_LoadX(uint2(4, i));
            float w = C_LoadX(uint2(3, i)) - C_LoadX(uint2(2, i));
            uint cIndex = C_LoadUInt32(uint2(2, i));
            uint bIndex = C_LoadUInt32(uint2(3, i));
            uint eIndex = C_LoadUInt32(uint2(4, i));
            float du = (j == bIndex ? 1.0 : 0.0) - (j == eIndex ? 1.0 : 0.0);
            float dw = (j == bIndex ? 1.0 : 0.0) - (j == cIndex ? 1.0 : 0.0);
            if (du == 0.0 && dw == 0.0) break;
            float IS = C_LoadFloat(uint2(5, i));
            float invNfVt = C_LoadFloat(uint2(7, i));
            float invNrVt = C_LoadFloat(uint2(9, i));
            float invVAF = C_LoadFloat(uint2(14, i));
            float invVAR = C_LoadFloat(uint2(15, i));
            float invIKF = C_LoadFloat(uint2(16, i));
            float invIKR = C_LoadFloat(uint2(17, i));
            float CJE = C_LoadFloat(uint2(18, i));
            float VJE = C_LoadFloat(uint2(19, i));
            float MJE = C_LoadFloat(uint2(20, i));
            float CJC = C_LoadFloat(uint2(21, i));
            float VJC = C_LoadFloat(uint2(22, i));
            float MJC = C_LoadFloat(uint2(23, i));
            float FC = C_LoadFloat(uint2(24, i));
            float TF = C_LoadFloat(uint2(25, i));
            float TR = C_LoadFloat(uint2(26, i));
            float NK = C_LoadFloat(uint2(27, i));
            float expF = exp(u * invNfVt);
            float expR = exp(w * invNrVt);
            float IF = IS * (expF - 1.0);
            float IR = IS * (expR - 1.0);
            float gF = IS * invNfVt * expF;
            float gR = IS * invNrVt * expR;

            // r = 1/qb = a * h(z), z = IF/IKF + IR/IKR.
            // NK=0.5 gives the standard GP square root. All derivatives follow this same r.
            float a = 1.0 - w * invVAF - u * invVAR;
            float z = IF * invIKF + IR * invIKR;
            float s = 1.0 + 4.0 * z;
            float d = pow(s, NK);
            float dp = 4.0 * NK * pow(s, NK - 1.0);
            float invOnePlusD = 1.0 / (1.0 + d);
            float h = 2.0 * invOnePlusD;
            float hp = -2.0 * dp * invOnePlusD * invOnePlusD;
            float zu = gF * invIKF;
            float zw = gR * invIKR;
            float r = a * h;
            float ru = -invVAR * h + a * hp * zu;
            float rw = -invVAF * h + a * hp * zw;

            // CJC here is already multiplied by XCJC; the remainder is a case-3 CJX row.
            float2 junctionCap = float2(0.0, 0.0);
            if (CJE != 0.0)
            {
                if (u < FC * VJE)
                {
                    float oneMinusV = 1.0 - u / VJE;
                    junctionCap.x = CJE * pow(oneMinusV, -MJE);
                }
                else
                {
                    float scale = CJE * pow(1.0 - FC, -MJE - 1.0);
                    junctionCap.x = scale * (1.0 - FC * (1.0 + MJE) + MJE * u / VJE);
                }
            }
            if (CJC != 0.0)
            {
                if (w < FC * VJC)
                {
                    float oneMinusV = 1.0 - w / VJC;
                    junctionCap.y = CJC * pow(oneMinusV, -MJC);
                }
                else
                {
                    float scale = CJC * pow(1.0 - FC, -MJC - 1.0);
                    junctionCap.y = scale * (1.0 - FC * (1.0 + MJC) + MJC * w / VJC);
                }
            }
            // Charge control: QBE = QJE + TF*IF*r, QBC = QJC + TR*IR.
            // QBE depends on both u and w, so retain the cross-capacitance Cx.
            float Ce = junctionCap.x + TF * (gF * r + IF * ru);
            float Cx = TF * IF * rw;
            float Cc = junctionCap.y + TR * gR;
            data += baseRow ? Ce * du + (Cx + Cc) * dw : -Cc * dw;
            break;
        }
        case 3:
        {
            // Nonlinear junction-capacitance branch (CJX or CCS), current positive -> negative.
            // [1..2] node indices, [3..6] C0, VJ, M, FC. FC=0 selects the CCS continuation.
            float C0 = C_LoadFloat(uint2(3, i));
            if (C0 == 0.0) break;
            float v = C_LoadX(uint2(1, i)) - C_LoadX(uint2(2, i));
            float VJ = C_LoadFloat(uint2(4, i));
            float M = C_LoadFloat(uint2(5, i));
            float FC = C_LoadFloat(uint2(6, i));
            uint positiveIndex = C_LoadUInt32(uint2(1, i));
            uint negativeIndex = C_LoadUInt32(uint2(2, i));
            float direction = (j == positiveIndex ? 1.0 : 0.0) - (j == negativeIndex ? 1.0 : 0.0);
            if (direction == 0.0) break;
            float capacitance;
            if (v < FC * VJ)
                capacitance = C0 * pow(1.0 - v / VJ, -M);
            else
                capacitance = C0 * pow(1.0 - FC, -M - 1.0)
                            * (1.0 - FC * (1.0 + M) + M * v / VJ);
            data += capacitance * direction;
            break;
        }
    }
    
    return data;
}

#endif
