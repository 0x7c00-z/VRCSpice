#ifndef SOLVER_MODEL_H_INCLUDED
#define SOLVER_MODEL_H_INCLUDED

#include "Solver_variables.hlsl"

Texture2D _A;
Texture2D _B;
Texture2D<float2> _C;
Texture2D _Is;
Texture2D _rhs;
Texture2D _TimeEvolution;

uint C_LoadUInt32(uint2 pixel)
{
    uint2 parts = (uint2)round(_C.Load(int3(pixel, 0)) * 65535.0);
    return parts.x | (parts.y << 16);
}

float C_LoadFloat(uint2 pixel) { return asfloat(C_LoadUInt32(pixel)); }

float XAt(uint stage, uint index)
{
    if (index == 0xffffffffu || index >= _DATA_N) return 0.0;
    if (stage == 0u) return SolverLoadFloat(OFFSET_COMMITTED_VECTOR + uint2(index, 0));
    return SolverLoadFloat(OFFSET_STAGE_VECTOR + uint2((stage - 1u) * _DATA_N + index, 0));
}

float JunctionCapacitance(float c0, float vj, float m, float fc, float v)
{
    if (c0 == 0.0 || vj == 0.0) return 0.0;
    if (v < fc * vj) return c0 * pow(max(1e-20, 1.0 - v / vj), -m);
    return c0 * pow(max(1e-20, 1.0 - fc), -m - 1.0)
        * (1.0 - fc * (1.0 + m) + m * v / vj);
}

// Integral of JunctionCapacitance with Q(0)=0.  Keeping Q explicit is what
// permits D*CQ + P*GFB collocation for voltage-dependent capacitances.
float JunctionCharge(float c0, float vj, float m, float fc, float v)
{
    if (c0 == 0.0 || vj == 0.0) return 0.0;
    float vf = fc * vj;
    float oneMinusFc = max(1e-20, 1.0 - fc);
    if (v < vf)
    {
        float oneMinusV = max(1e-20, 1.0 - v / vj);
        if (abs(1.0 - m) < 1e-6) return -c0 * vj * log(oneMinusV);
        return c0 * vj * (1.0 - pow(oneMinusV, 1.0 - m)) / (1.0 - m);
    }
    float qf;
    if (abs(1.0 - m) < 1e-6) qf = -c0 * vj * log(oneMinusFc);
    else qf = c0 * vj * (1.0 - pow(oneMinusFc, 1.0 - m)) / (1.0 - m);
    float cf = c0 * pow(oneMinusFc, -m);
    float slope = c0 * m / vj * pow(oneMinusFc, -m - 1.0);
    float dv = v - vf;
    return qf + cf * dv + 0.5 * slope * dv * dv;
}

void BjtTerms(uint stage, uint row,
    out float u, out float w, out float IF, out float IR,
    out float gF, out float gR, out float r, out float ru, out float rw,
    out float Ibe, out float Ibc, out float gbe, out float gbc,
    out float qbe, out float qbc, out float ce, out float cx, out float cc)
{
    uint cIndex = C_LoadUInt32(uint2(2, row));
    uint bIndex = C_LoadUInt32(uint2(3, row));
    uint eIndex = C_LoadUInt32(uint2(4, row));
    u = XAt(stage, bIndex) - XAt(stage, eIndex);
    w = XAt(stage, bIndex) - XAt(stage, cIndex);

    float IS = C_LoadFloat(uint2(5, row));
    float invBF = C_LoadFloat(uint2(6, row));
    float invNfVt = C_LoadFloat(uint2(7, row));
    float invBR = C_LoadFloat(uint2(8, row));
    float invNrVt = C_LoadFloat(uint2(9, row));
    float ISE = C_LoadFloat(uint2(10, row));
    float invNeVt = C_LoadFloat(uint2(11, row));
    float ISC = C_LoadFloat(uint2(12, row));
    float invNcVt = C_LoadFloat(uint2(13, row));
    float invVAF = C_LoadFloat(uint2(14, row));
    float invVAR = C_LoadFloat(uint2(15, row));
    float invIKF = C_LoadFloat(uint2(16, row));
    float invIKR = C_LoadFloat(uint2(17, row));
    float CJE = C_LoadFloat(uint2(18, row));
    float VJE = C_LoadFloat(uint2(19, row));
    float MJE = C_LoadFloat(uint2(20, row));
    float CJC = C_LoadFloat(uint2(21, row));
    float VJC = C_LoadFloat(uint2(22, row));
    float MJC = C_LoadFloat(uint2(23, row));
    float FC = C_LoadFloat(uint2(24, row));
    float TF = C_LoadFloat(uint2(25, row));
    float TR = C_LoadFloat(uint2(26, row));
    float NK = C_LoadFloat(uint2(27, row));

    float expF = exp(u * invNfVt);
    float expR = exp(w * invNrVt);
    IF = IS * (expF - 1.0);
    IR = IS * (expR - 1.0);
    gF = IS * invNfVt * expF;
    gR = IS * invNrVt * expR;

    float a = 1.0 - w * invVAF - u * invVAR;
    float z = IF * invIKF + IR * invIKR;
    float s = max(1e-20, 1.0 + 4.0 * z);
    float d = pow(s, NK);
    float dp = 4.0 * NK * pow(s, NK - 1.0);
    float invOnePlusD = 1.0 / (1.0 + d);
    float h = 2.0 * invOnePlusD;
    float hp = -2.0 * dp * invOnePlusD * invOnePlusD;
    float zu = gF * invIKF;
    float zw = gR * invIKR;
    r = a * h;
    ru = -invVAR * h + a * hp * zu;
    rw = -invVAF * h + a * hp * zw;

    Ibe = IF * invBF + ISE * (exp(u * invNeVt) - 1.0);
    Ibc = IR * invBR + ISC * (exp(w * invNcVt) - 1.0);
    gbe = gF * invBF + ISE * invNeVt * exp(u * invNeVt);
    gbc = gR * invBR + ISC * invNcVt * exp(w * invNcVt);

    float qje = JunctionCharge(CJE, VJE, MJE, FC, u);
    float qjc = JunctionCharge(CJC, VJC, MJC, FC, w);
    float cje = JunctionCapacitance(CJE, VJE, MJE, FC, u);
    float cjc = JunctionCapacitance(CJC, VJC, MJC, FC, w);
    qbe = qje + TF * IF * r;
    qbc = qjc + TR * IR;
    ce = cje + TF * (gF * r + IF * ru);
    cx = TF * IF * rw;
    cc = cjc + TR * gR;
}

float NonlinearCharge(uint stage, uint row)
{
    uint kind = C_LoadUInt32(uint2(0, row));
    if (kind == 1u)
    {
        float isat = C_LoadFloat(uint2(1, row));
        float invNVt = C_LoadFloat(uint2(2, row));
        float v = XAt(stage, C_LoadUInt32(uint2(4, row))) - XAt(stage, C_LoadUInt32(uint2(3, row)));
        float tt = C_LoadFloat(uint2(5, row));
        float id = isat * (exp(v * invNVt) - 1.0);
        return JunctionCharge(C_LoadFloat(uint2(6, row)), C_LoadFloat(uint2(7, row)),
            C_LoadFloat(uint2(8, row)), C_LoadFloat(uint2(9, row)), v) + tt * id;
    }
    if (kind == 2u)
    {
        float u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc;
        BjtTerms(stage,row,u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc);
        return C_LoadUInt32(uint2(1,row)) != 0u ? qbe + qbc : -qbc;
    }
    if (kind == 3u)
    {
        float v = XAt(stage, C_LoadUInt32(uint2(1,row))) - XAt(stage, C_LoadUInt32(uint2(2,row)));
        return JunctionCharge(C_LoadFloat(uint2(3,row)), C_LoadFloat(uint2(4,row)),
            C_LoadFloat(uint2(5,row)), C_LoadFloat(uint2(6,row)), v);
    }
    return 0.0;
}

float NonlinearCurrent(uint stage, uint row)
{
    uint kind = C_LoadUInt32(uint2(0, row));
    if (kind == 1u)
    {
        float v = XAt(stage, C_LoadUInt32(uint2(4,row))) - XAt(stage, C_LoadUInt32(uint2(3,row)));
        return C_LoadFloat(uint2(1,row)) * (exp(v * C_LoadFloat(uint2(2,row))) - 1.0);
    }
    if (kind == 2u)
    {
        float u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc;
        BjtTerms(stage,row,u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc);
        return C_LoadUInt32(uint2(1,row)) != 0u ? Ibe + Ibc : (IF - IR) * r - Ibc;
    }
    return 0.0;
}

float NonlinearChargeDerivative(uint stage, uint row, uint column)
{
    uint kind = C_LoadUInt32(uint2(0,row));
    if (kind == 1u)
    {
        uint cathode = C_LoadUInt32(uint2(3,row));
        uint anode = C_LoadUInt32(uint2(4,row));
        float direction = (column == anode ? 1.0 : 0.0) - (column == cathode ? 1.0 : 0.0);
        if (direction == 0.0) return 0.0;
        float v = XAt(stage,anode) - XAt(stage,cathode);
        float isat = C_LoadFloat(uint2(1,row));
        float invNVt = C_LoadFloat(uint2(2,row));
        float cap = JunctionCapacitance(C_LoadFloat(uint2(6,row)), C_LoadFloat(uint2(7,row)),
            C_LoadFloat(uint2(8,row)), C_LoadFloat(uint2(9,row)), v);
        return direction * (cap + C_LoadFloat(uint2(5,row)) * isat * invNVt * exp(v * invNVt));
    }
    if (kind == 2u)
    {
        uint cIndex=C_LoadUInt32(uint2(2,row)), bIndex=C_LoadUInt32(uint2(3,row)), eIndex=C_LoadUInt32(uint2(4,row));
        float du=(column==bIndex?1.0:0.0)-(column==eIndex?1.0:0.0);
        float dw=(column==bIndex?1.0:0.0)-(column==cIndex?1.0:0.0);
        if (du==0.0 && dw==0.0) return 0.0;
        float u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc;
        BjtTerms(stage,row,u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc);
        return C_LoadUInt32(uint2(1,row)) != 0u ? ce*du + (cx+cc)*dw : -cc*dw;
    }
    if (kind == 3u)
    {
        uint positive=C_LoadUInt32(uint2(1,row)), negative=C_LoadUInt32(uint2(2,row));
        float direction=(column==positive?1.0:0.0)-(column==negative?1.0:0.0);
        if(direction==0.0)return 0.0;
        float v=XAt(stage,positive)-XAt(stage,negative);
        return direction*JunctionCapacitance(C_LoadFloat(uint2(3,row)),C_LoadFloat(uint2(4,row)),
            C_LoadFloat(uint2(5,row)),C_LoadFloat(uint2(6,row)),v);
    }
    return 0.0;
}

float NonlinearCurrentDerivative(uint stage, uint row, uint column)
{
    uint kind=C_LoadUInt32(uint2(0,row));
    if(kind==1u)
    {
        uint cathode=C_LoadUInt32(uint2(3,row)),anode=C_LoadUInt32(uint2(4,row));
        float direction=(column==anode?1.0:0.0)-(column==cathode?1.0:0.0);
        if(direction==0.0)return 0.0;
        float v=XAt(stage,anode)-XAt(stage,cathode);
        return direction*C_LoadFloat(uint2(1,row))*C_LoadFloat(uint2(2,row))*exp(v*C_LoadFloat(uint2(2,row)));
    }
    if(kind==2u)
    {
        uint cIndex=C_LoadUInt32(uint2(2,row)),bIndex=C_LoadUInt32(uint2(3,row)),eIndex=C_LoadUInt32(uint2(4,row));
        float du=(column==bIndex?1.0:0.0)-(column==eIndex?1.0:0.0);
        float dw=(column==bIndex?1.0:0.0)-(column==cIndex?1.0:0.0);
        if(du==0.0&&dw==0.0)return 0.0;
        float u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc;
        BjtTerms(stage,row,u,w,IF,IR,gF,gR,r,ru,rw,Ibe,Ibc,gbe,gbc,qbe,qbc,ce,cx,cc);
        if(C_LoadUInt32(uint2(1,row))!=0u)return gbe*du+gbc*dw;
        float itu=gF*r+(IF-IR)*ru;
        float itw=-gR*r+(IF-IR)*rw;
        return itu*du+(itw-gbc)*dw;
    }
    return 0.0;
}

float CQ(uint stage, uint row)
{
    float value=NonlinearCharge(stage,row);
    [loop]for(uint column=0u;column<_DATA_N;column++)
        value+=_B[uint2(row,column)].r*XAt(stage,column);
    return value;
}

float GFB(uint stage, uint row)
{
    float value=-_rhs[uint2(row,0)].r+NonlinearCurrent(stage,row);
    [loop]for(uint column=0u;column<_DATA_N;column++)
        value+=_A[uint2(row,column)].r*XAt(stage,column);
    return value;
}

float CJacobian(uint stage,uint row,uint column)
{
    return _B[uint2(row,column)].r+NonlinearChargeDerivative(stage,row,column);
}

float AJacobian(uint stage,uint row,uint column)
{
    return _A[uint2(row,column)].r+NonlinearCurrentDerivative(stage,row,column);
}

float DCoefficient(uint equationStage,uint timeStage)
{
    uint row=_IntegrationScheme==1u?0u:3u+equationStage;
    return _TimeEvolution.Load(int3(timeStage,row,0)).r;
}

float PNormalizedCoefficient(uint equationStage,uint timeStage)
{
    uint row=_IntegrationScheme==1u?1u:6u+equationStage;
    return _TimeEvolution.Load(int3(timeStage,row,0)).r;
}

float StageTimeCoefficient(uint timeStage)
{
    uint row=_IntegrationScheme==1u?2u:9u;
    return _TimeEvolution.Load(int3(timeStage,row,0)).r;
}

#endif
