#ifndef SOLVER_H_INCLUDED
#define SOLVER_H_INCLUDED

#include "Solver_model.hlsl"

bool InvalidFloat(float value) { return isnan(value) || isinf(value); }

float MatrixValue(uint row,uint column)
{
    return SolverLoadFloat(OFFSET_JACOBIAN_MATRIX+uint2(row,column));
}

float NewtonValue(uint row)
{
    return SolverLoadFloat(OFFSET_NEWTON_VECTOR+uint2(row,0));
}

uint PivotRow(uint k)
{
    uint pivot=k;
    float maximum=abs(MatrixValue(k,k));
    [loop]for(uint row=k+1u;row<_SYSTEM_N;row++)
    {
        float candidate=abs(MatrixValue(row,k));
        if(candidate>maximum){maximum=candidate;pivot=row;}
    }
    return pivot;
}

uint SwappedRow(uint row,uint k,uint pivot)
{
    return row==k?pivot:(row==pivot?k:row);
}

float BuildRemain(uint equation)
{
    uint equationStage=equation/_DATA_N;
    uint variable=equation-equationStage*_DATA_N;
    float h=_OutputDeltaTime*SolverLoadFloat(OFFSET_NORMALIZED_STEP);
    float value=0.0;
    [loop]for(uint stage=0u;stage<=_STAGE_COUNT;stage++)
    {
        value+=DCoefficient(equationStage,stage)*CQ(stage,variable);
        value+=h*PNormalizedCoefficient(equationStage,stage)*GFB(stage,variable);
    }
    return value;
}

float BuildJacobian(uint equation,uint unknown)
{
    uint equationStage=equation/_DATA_N;
    uint row=equation-equationStage*_DATA_N;
    uint stage=unknown/_DATA_N+1u;
    uint column=unknown-(stage-1u)*_DATA_N;
    float h=_OutputDeltaTime*SolverLoadFloat(OFFSET_NORMALIZED_STEP);
    return DCoefficient(equationStage,stage)*CJacobian(stage,row,column)
        +h*PNormalizedCoefficient(equationStage,stage)*AJacobian(stage,row,column);
}

uint initialize_solver(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    uint outputCount=SolverLoadUInt(OFFSET_OUTPUT_COUNT);
    if(pixel.y==OFFSET_COMMITTED_VECTOR.y && pixel.x<_DATA_N)
        result=SolverStoreFloat(outputCount>0u?get_data(0u,pixel.x):0.0);
    else if(!any(pixel-OFFSET_LOOP_COUNTER) || !any(pixel-OFFSET_NR_ITER_N) ||
        !any(pixel-OFFSET_REJECT_COUNT) || !any(pixel-OFFSET_FAILURE_FLAG)) result=0u;
    else if(!any(pixel-OFFSET_NORMALIZED_PROGRESS))result=SolverStoreFloat(0.0);
    else if(!any(pixel-OFFSET_NORMALIZED_STEP))result=SolverStoreFloat(0.1);
    else if(!any(pixel-OFFSET_CURRENT_TIME))result=SolverLoadUInt(OFFSET_LAST_OUTPUT_TIME);
    else if(!any(pixel-OFFSET_APPLIED_SETTINGS))result=_SettingsRevision;
    else if(!any(pixel-OFFSET_APPLIED_HISTORY))result=_ClearHistoryRevision;
    else if(!any(pixel-OFFSET_APPLIED_RESTART))result=_RestartRevision;
    else if(!any(pixel-OFFSET_SCHEME))result=_IntegrationScheme;
    return result;
}

uint prepare_step(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    if(pixel.y==OFFSET_STAGE_VECTOR.y && pixel.x<_SYSTEM_N)
        result=SolverLoadUInt(OFFSET_COMMITTED_VECTOR+uint2(pixel.x%_DATA_N,0));
    else if(!any(pixel-OFFSET_LOOP_COUNTER) || !any(pixel-OFFSET_NR_ITER_N) ||
        !any(pixel-OFFSET_FAILURE_FLAG))result=0u;
    return result;
}

uint build_system(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    if(pixel.x<_SYSTEM_N && pixel.y>=OFFSET_JACOBIAN_MATRIX.y &&
        pixel.y<OFFSET_JACOBIAN_MATRIX.y+_SYSTEM_N)
        result=SolverStoreFloat(BuildJacobian(pixel.x,pixel.y-OFFSET_JACOBIAN_MATRIX.y));
    else if(pixel.y==OFFSET_NEWTON_VECTOR.y && pixel.x<_SYSTEM_N)
        result=SolverStoreFloat(BuildRemain(pixel.x));
    else if(!any(pixel-OFFSET_LOOP_COUNTER))result=0u;
    return result;
}

uint lu_decomposition(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    uint k=LOOP_I;
    uint pivot=PivotRow(k);
    if(pixel.x<_SYSTEM_N && pixel.y>=OFFSET_JACOBIAN_MATRIX.y &&
        pixel.y<OFFSET_JACOBIAN_MATRIX.y+_SYSTEM_N)
    {
        uint row=pixel.x;
        uint column=pixel.y-OFFSET_JACOBIAN_MATRIX.y;
        uint sourceRow=SwappedRow(row,k,pivot);
        float value=MatrixValue(sourceRow,column);
        if(row>k)
        {
            float factor=MatrixValue(sourceRow,k)/MatrixValue(pivot,k);
            if(column==k)value=factor;
            else if(column>k)value-=factor*MatrixValue(pivot,column);
        }
        result=SolverStoreFloat(value);
    }
    else if(pixel.y==OFFSET_NEWTON_VECTOR.y && pixel.x<_SYSTEM_N)
    {
        uint row=pixel.x;
        uint sourceRow=SwappedRow(row,k,pivot);
        float value=NewtonValue(sourceRow);
        if(row>k)value-=MatrixValue(sourceRow,k)/MatrixValue(pivot,k)*NewtonValue(pivot);
        result=SolverStoreFloat(value);
    }
    return result;
}

uint solve_vector(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    uint row=_SYSTEM_N-1u-LOOP_I;
    if(pixel.y==OFFSET_NEWTON_VECTOR.y && pixel.x==row)
    {
        float value=NewtonValue(row);
        [loop]for(uint column=row+1u;column<_SYSTEM_N;column++)
            value-=MatrixValue(row,column)*NewtonValue(column);
        value/=MatrixValue(row,row);
        result=SolverStoreFloat(value);
    }
    return result;
}

uint update_newton(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    if(pixel.y==OFFSET_STAGE_VECTOR.y && pixel.x<_SYSTEM_N)
    {
        float correction=clamp(NewtonValue(pixel.x),-1.0,1.0);
        result=SolverStoreFloat(SolverLoadFloat(pixel)-correction);
    }
    else if(!any(pixel-OFFSET_NR_ITER_N))result=SolverLoadUInt(pixel)+1u;
    return result;
}

uint accept_step(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    float progress=SolverLoadFloat(OFFSET_NORMALIZED_PROGRESS);
    float step=SolverLoadFloat(OFFSET_NORMALIZED_STEP);
    float nextProgress=min(1.0,progress+step);
    if(pixel.y==OFFSET_COMMITTED_VECTOR.y && pixel.x<_DATA_N)
        result=SolverLoadUInt(OFFSET_STAGE_VECTOR+uint2((_STAGE_COUNT-1u)*_DATA_N+pixel.x,0));
    else if(!any(pixel-OFFSET_NORMALIZED_PROGRESS))result=SolverStoreFloat(nextProgress);
    else if(!any(pixel-OFFSET_NORMALIZED_STEP))result=SolverStoreFloat(min(step*2.0,max(0.0,1.0-nextProgress)));
    else if(!any(pixel-OFFSET_CURRENT_TIME))result=SolverStoreFloat(SolverLoadFloat(pixel)+_OutputDeltaTime*step);
    else if(!any(pixel-OFFSET_REJECT_COUNT) || !any(pixel-OFFSET_FAILURE_FLAG) || !any(pixel-OFFSET_NR_ITER_N))result=0u;
    return result;
}

uint reject_step(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    if(!any(pixel-OFFSET_NORMALIZED_STEP))result=SolverStoreFloat(SolverLoadFloat(pixel)*0.5);
    else if(!any(pixel-OFFSET_REJECT_COUNT))result=SolverLoadUInt(pixel)+1u;
    else if(!any(pixel-OFFSET_NR_ITER_N) || !any(pixel-OFFSET_LOOP_COUNTER))result=0u;
    return result;
}

uint push_output(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    if(pixel.y>=OFFSET_OUT_BUFFER.y && pixel.y<OFFSET_OUT_BUFFER.y+OUTPUT_BUFFER_LENGTH && pixel.x<=_DATA_N)
    {
        uint history=pixel.y-OFFSET_OUT_BUFFER.y;
        if(history==0u)
            result=pixel.x<_DATA_N?SolverLoadUInt(OFFSET_COMMITTED_VECTOR+uint2(pixel.x,0)):SolverStoreFloat(_OutputDeltaTime);
        else result=SolverLoadUInt(pixel-uint2(0,1));
    }
    else if(!any(pixel-OFFSET_NORMALIZED_PROGRESS))result=SolverStoreFloat(0.0);
    else if(!any(pixel-OFFSET_NORMALIZED_STEP))result=SolverStoreFloat(0.1);
    else if(!any(pixel-OFFSET_OUTPUT_COUNT))result=min(OUTPUT_BUFFER_LENGTH,SolverLoadUInt(pixel)+1u);
    else if(!any(pixel-OFFSET_OUTPUT_SEQUENCE))result=SolverLoadUInt(pixel)+1u;
    else if(!any(pixel-OFFSET_LAST_OUTPUT_TIME))result=SolverLoadUInt(OFFSET_CURRENT_TIME);
    return result;
}

uint settings_reset(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    uint count=SolverLoadUInt(OFFSET_OUTPUT_COUNT);
    bool clearHistory=SolverLoadUInt(OFFSET_APPLIED_HISTORY)!=_ClearHistoryRevision;
    bool restart=SolverLoadUInt(OFFSET_APPLIED_RESTART)!=_RestartRevision;
    if(pixel.y==OFFSET_COMMITTED_VECTOR.y && pixel.x<_DATA_N)
        result=restart?SolverStoreFloat(0.0):SolverStoreFloat(count>0u?get_data(0u,pixel.x):0.0);
    else if((clearHistory || restart) && pixel.y>=OFFSET_OUT_BUFFER.y &&
        pixel.y<OFFSET_OUT_BUFFER.y+OUTPUT_BUFFER_LENGTH)result=0u;
    else if(!any(pixel-OFFSET_NORMALIZED_PROGRESS))result=SolverStoreFloat(0.0);
    else if(!any(pixel-OFFSET_NORMALIZED_STEP))result=SolverStoreFloat(0.1);
    else if(!any(pixel-OFFSET_CURRENT_TIME))result=restart?SolverStoreFloat(0.0):SolverLoadUInt(OFFSET_LAST_OUTPUT_TIME);
    else if(!any(pixel-OFFSET_LOOP_COUNTER) || !any(pixel-OFFSET_NR_ITER_N) ||
        !any(pixel-OFFSET_REJECT_COUNT) || !any(pixel-OFFSET_FAILURE_FLAG))result=0u;
    else if(!any(pixel-OFFSET_APPLIED_SETTINGS))result=_SettingsRevision;
    else if(!any(pixel-OFFSET_APPLIED_HISTORY))result=_ClearHistoryRevision;
    else if(!any(pixel-OFFSET_APPLIED_RESTART))result=_RestartRevision;
    else if(!any(pixel-OFFSET_SCHEME))result=_IntegrationScheme;
    else if((clearHistory || restart) &&
        (!any(pixel-OFFSET_OUTPUT_COUNT) || !any(pixel-OFFSET_OUTPUT_SEQUENCE)))result=0u;
    else if(restart && !any(pixel-OFFSET_LAST_OUTPUT_TIME))result=0u;
    return result;
}

uint process(uint2 pixel)
{
    switch(STATE)
    {
        case STATE_INITIALIZE:return initialize_solver(pixel);
        case STATE_PREPARE_STEP:return prepare_step(pixel);
        case STATE_BUILD_SYSTEM:return build_system(pixel);
        case STATE_LU_DECOMPOSITION:return lu_decomposition(pixel);
        case STATE_SOLVE_VECTOR:return solve_vector(pixel);
        case STATE_UPDATE_NEWTON:return update_newton(pixel);
        case STATE_ACCEPT_STEP:return accept_step(pixel);
        case STATE_REJECT_STEP:return reject_step(pixel);
        case STATE_PUSH_OUTPUT:return push_output(pixel);
        case STATE_SETTINGS_RESET:return settings_reset(pixel);
        default:return SolverLoadUInt(pixel);
    }
}

uint flowControl(uint2 pixel)
{
    uint result=SolverLoadUInt(pixel);
    if(!any(pixel-OFFSET_SOLVER_STATE))
    {
        if(SolverLoadUInt(OFFSET_APPLIED_SETTINGS)!=_SettingsRevision ||
            SolverLoadUInt(OFFSET_APPLIED_HISTORY)!=_ClearHistoryRevision ||
            SolverLoadUInt(OFFSET_APPLIED_RESTART)!=_RestartRevision ||
            SolverLoadUInt(OFFSET_SCHEME)!=_IntegrationScheme)
            return STATE_SETTINGS_RESET;

        if(STATE==STATE_INITIALIZE || STATE==STATE_PREPARE_STEP || STATE==STATE_PUSH_OUTPUT || STATE==STATE_SETTINGS_RESET)
            result=STATE==STATE_INITIALIZE?STATE_PREPARE_STEP:
                (STATE==STATE_PREPARE_STEP?STATE_BUILD_SYSTEM:
                (STATE==STATE_PUSH_OUTPUT?STATE_PREPARE_STEP:STATE_PREPARE_STEP));
        else if(STATE==STATE_BUILD_SYSTEM)
        {
            bool invalid=false;
            [loop]for(uint i=0u;i<_SYSTEM_N;i++)invalid=invalid||InvalidFloat(NewtonValue(i));
            result=invalid?STATE_REJECT_STEP:STATE_LU_DECOMPOSITION;
        }
        else if(STATE==STATE_LU_DECOMPOSITION)
        {
            float pivot=MatrixValue(LOOP_I,LOOP_I);
            if(InvalidFloat(pivot)||abs(pivot)<PIVOT_MINIMUM)result=STATE_REJECT_STEP;
            else result=LOOP_I>=_SYSTEM_N-1u?STATE_SOLVE_VECTOR:STATE_LU_DECOMPOSITION;
        }
        else if(STATE==STATE_SOLVE_VECTOR)
            result=LOOP_I>=_SYSTEM_N-1u?STATE_UPDATE_NEWTON:STATE_SOLVE_VECTOR;
        else if(STATE==STATE_UPDATE_NEWTON)
        {
            float norm=0.0;bool invalid=false;
            [loop]for(uint i=0u;i<_SYSTEM_N;i++)
            {float d=NewtonValue(i);invalid=invalid||InvalidFloat(d);norm+=d*d;}
            if(invalid)result=STATE_REJECT_STEP;
            else if(norm<NEWTON_PRECISION)result=STATE_ACCEPT_STEP;
            else result=SolverLoadUInt(OFFSET_NR_ITER_N)>=max(1u,_MaxNewtonIterations)?STATE_REJECT_STEP:STATE_BUILD_SYSTEM;
        }
        else if(STATE==STATE_ACCEPT_STEP)
            result=SolverLoadFloat(OFFSET_NORMALIZED_PROGRESS)>=1.0-1e-6?STATE_PUSH_OUTPUT:STATE_PREPARE_STEP;
        else if(STATE==STATE_REJECT_STEP)
            result=SolverLoadUInt(OFFSET_REJECT_COUNT)>=MAX_REJECT_COUNT?STATE_SOLVER_ERROR:STATE_PREPARE_STEP;
        else result=STATE_SOLVER_ERROR;
    }
    else if(!any(pixel-OFFSET_LOOP_COUNTER))
    {
        if(STATE==STATE_LU_DECOMPOSITION || STATE==STATE_SOLVE_VECTOR)
            result=SolverLoadUInt(pixel)>=_SYSTEM_N-1u?0u:SolverLoadUInt(pixel)+1u;
        else result=0u;
    }
    else if(!any(pixel-OFFSET_FAILURE_FLAG))
    {
        if(STATE==STATE_BUILD_SYSTEM)
        {
            bool invalid=false;
            [loop]for(uint i=0u;i<_SYSTEM_N;i++)invalid=invalid||InvalidFloat(NewtonValue(i));
            if(invalid)result=FAILURE_NONFINITE;
        }
        else if(STATE==STATE_LU_DECOMPOSITION)
        {
            float pivot=MatrixValue(LOOP_I,LOOP_I);
            if(InvalidFloat(pivot)||abs(pivot)<PIVOT_MINIMUM)result=FAILURE_SINGULAR;
        }
        else if(STATE==STATE_UPDATE_NEWTON)
        {
            bool invalid=false;
            [loop]for(uint i=0u;i<_SYSTEM_N;i++)invalid=invalid||InvalidFloat(NewtonValue(i));
            if(invalid)result=FAILURE_NONFINITE;
            else if(SolverLoadUInt(OFFSET_NR_ITER_N)>=max(1u,_MaxNewtonIterations))result=FAILURE_NEWTON;
        }
    }
    return result;
}

uint2 uv2texel(float2 uv)
{
    return (uint2)(uv*_MainTex_TexelSize.zw);
}

#endif
