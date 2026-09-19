#include "Solver_model.hlsl"
#include "Solver_variables.hlsl"
//This shader computes gear method up to 2nd order.

/*
while(true){
    while(ERROR_FEASIBLE){
        DETERMINE_TIME_STEP_AND_ORDER;
        GENERATE_COEFFICIENTS;
        GENERATE_PREDICTOR_VECTOR;
        COPY_PREDICTOR_VECTOR_TO_NR_VECTOR; //performed in the GENERATE_PREDICTOR_VECTOR function
        WHILE(CONVERGENCE){
            GEBERATE_XDOT_VECTOR;
            GENERATE_YACOBI_MATRIX_AND_VECTOR;
            FOR(_DATA_N){
                LU_DECOMPOSITION;// YACOBI_MATRIX = U; INVERSE_VECTOR = L^{-1}b;
            }
            FOR(_DATA_N){
                SOLVE_VECTOR;//INVERSE_VECTOR = U^{-1}*INVERSE_VECTOR;
            }
            UPDATE_NR_VECTOR;
        }
    }
    PUSH_TO_OUTPUT;
}

タイムステップを修正する。
↓
予測子、修正子の係数を計算する。(完成！)
↓
予測子を計算。(完成！)
↓
修正子を計算。
↓
誤差評価&タイムステップ修正

*/
#define STATE_INITIALIZE 0
#define STATE_DETERMINE_TIME_STEP_AND_ORDER 1
#define STATE_GENERATE_COEFFICIENTS 2
#define STATE_GENERATE_PREDICTOR_VECTOR 3
#define STATE_GENERATE_XDOT_VECTOR 4
#define STATE_GENERATE_MATRIX_and_VECTOR 5
#define STATE_LU_DECOMPOSITION 6
#define STATE_SOLVE_VECTOR 7
#define STATE_UPDATE_NR_VECTOR 8
#define STATE_PUSH_TO_OUTPUT 9

#define PRECISION 0.000001
#define MAX_NORM_ERR 0.1

float get_data(uint index, uint row)
{
    //get data from output
    float data = SolverLoadFloat(OFFSET_OUT_BUFFER + uint2(row, index)); //get current data to be processed
    return data;
}

float get_data_i_data_im1_timestep(uint index)
{
    //get the timestep between datai and datai-1
    float data = SolverLoadFloat(OFFSET_OUT_BUFFER + uint2(_DATA_N, index)); //get current data to be processed
    return data;
}

uint push_to_output(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    if (!any(pixel - OFFSET_STEPS_N))
    {
        result = SolverStoreUInt(SolverLoadUInt(pixel) + 1u);
    }
    if (pixel.y > OFFSET_OUT_BUFFER.y)
    {
        result = SolverLoadUInt(pixel - uint2(0, 1));
    }
    if (pixel.y == OFFSET_OUT_BUFFER.y)
    {
        // Store the accepted time step beside the solution vector.
        result = SolverLoadUInt(pixel.x == _DATA_N
            ? OFFSET_TIME_STEP : OFFSET_NR_VECTOR + uint2(pixel.x, 0));
    }
    return result;
}

uint generate_yacobi_matrix_and_vector(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    if (OFFSET_YACOBI_MATRIX.y <= pixel.y && pixel.y < OFFSET_YACOBI_MATRIX.y + _DATA_N)
    {
        pixel -= OFFSET_YACOBI_MATRIX;
        result = SolverStoreFloat(dfidxj(pixel.x, pixel.y)
            + dfidxdotj(pixel.x, pixel.y) * SolverLoadFloat(OFFSET_COF_COR_NP1));
    }
    if (pixel.y == OFFSET_INVERSE_VECTOR.y)
    {
        result = SolverStoreFloat(f(pixel.x));
    }
    return result;
}

uint update_nr_vector(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    if (pixel.y == OFFSET_NR_VECTOR.y)
    {
        float data = SolverLoadFloat(pixel);
        data -= max(-1.0, min(1.0, SolverLoadFloat(OFFSET_INVERSE_VECTOR + uint2(pixel.x, 0))));
        result = SolverStoreFloat(data);
    }
    return result;
}

uint lu_decomposition(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    
//pivotting function to get the correct value from the texture after pivotting, this is used in the lu decomposition process.
//this replace LOOP_COUNTER with the max_index to get the correct value from the texture after pivotting
#define PIVOTTING(x) ((x == k) ? max_index : ((x == max_index) ? k : x))
    
    //perform lu decomposition on the yacobi matrix, and store the inverse of the diagonal of the yacobi matrix in the OFFSET_INVERSE_VECTOR + uint2(0, 0) to OFFSET_INVERSE_VECTOR + uint2(N-1, 0)
    uint k = LOOP_I; //get current loop counter
    uint max_index = k;
    float max_value = abs(SolverLoadFloat(OFFSET_YACOBI_MATRIX + uint2(k, k)));
    if ((OFFSET_YACOBI_MATRIX.y <= pixel.y && pixel.y < OFFSET_YACOBI_MATRIX.y + _DATA_N) || pixel.y == OFFSET_INVERSE_VECTOR.y)//if yabobi matrix or inverse vector
    {
        [loop]
        for (uint i = k + 1; i < _DATA_N; i++)
        {
            //look for the maximum value in the current column to avoid numerical instability
            if (max_value < abs(SolverLoadFloat(OFFSET_YACOBI_MATRIX + uint2(i, k))))
            {
                max_value = abs(SolverLoadFloat(OFFSET_YACOBI_MATRIX + uint2(i, k)));
                max_index = i;
            }
        }
        
        float data = SolverLoadFloat(uint2(PIVOTTING(pixel.x), pixel.y));
        
        //multiply both INVERSE_VECTOR and YACOBI_MATRIX by same matrix do pivotting and lu decomposition at the same time
        if (pixel.x > k)
        {
            data -= SolverLoadFloat(uint2(PIVOTTING(k), pixel.y)) * SolverLoadFloat(OFFSET_YACOBI_MATRIX + uint2(PIVOTTING(pixel.x), k)) / SolverLoadFloat(OFFSET_YACOBI_MATRIX + uint2(PIVOTTING(k), k));
        }
        result = SolverStoreFloat(data);
    }

#undef PIVOTTING
    return result;
}


uint solve_vector(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    if (pixel.y == OFFSET_INVERSE_VECTOR.y && pixel.x == _DATA_N - 1 - LOOP_I)
    {
        float data = SolverLoadFloat(pixel);
        for (uint i = pixel.x + 1; i < _DATA_N; i++)
        {
            data -= SolverLoadFloat(OFFSET_YACOBI_MATRIX + uint2(pixel.x, i))
                * SolverLoadFloat(OFFSET_INVERSE_VECTOR + uint2(i, 0));
        }
        data /= SolverLoadFloat(OFFSET_YACOBI_MATRIX + uint2(pixel.x, pixel.x));
        result = SolverStoreFloat(data);
    }
    return result;
}

uint generate_xdot_vector(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    if (pixel.y == OFFSET_XDOT_VECTOR.y)
    {
        float data = SolverLoadFloat(OFFSET_COF_COR_NP1) * SolverLoadFloat(OFFSET_NR_VECTOR + uint2(pixel.x, 0))
            + SolverLoadFloat(OFFSET_COF_COR_N) * get_data(0, pixel.x)
            + SolverLoadFloat(OFFSET_COF_COR_NM1) * get_data(1, pixel.x);
        result = SolverStoreFloat(data);
    }
    return result;
}

uint initialize(uint2 pixel)
{
    uint result = SolverStoreUInt(0u);
    if (!any(pixel - OFFSET_TIME_STEP))
    {
        result = SolverStoreFloat(_DeltaTime);
    }
    return result;
}

float2x2 inverse_matrix(float2x2 mat)
{
    //calculate the inverse of a 2x2 matrix
    float det = mat[0][0] * mat[1][1] - mat[0][1] * mat[1][0];
    return float2x2(
        mat[1][1] / det, -mat[0][1] / det,
        -mat[1][0] / det, mat[0][0] / det
    );
}

float3x3 inverse_matrix(float3x3 mat)
{
    //calculate the inverse of a 3x3 matrix
    float det = mat[0][0] * (mat[1][1] * mat[2][2] - mat[1][2] * mat[2][1]) -
                mat[0][1] * (mat[1][0] * mat[2][2] - mat[1][2] * mat[2][0]) +
                mat[0][2] * (mat[1][0] * mat[2][1] - mat[1][1] * mat[2][0]);
    return float3x3(
        (mat[1][1] * mat[2][2] - mat[1][2] * mat[2][1]) / det,
        (mat[0][2] * mat[2][1] - mat[0][1] * mat[2][2]) / det,
        (mat[0][1] * mat[1][2] - mat[0][2] * mat[1][1]) / det,
        (mat[1][2] * mat[2][0] - mat[1][0] * mat[2][2]) / det,
        (mat[0][0] * mat[2][2] - mat[0][2] * mat[2][0]) / det,
        (mat[0][2] * mat[1][0] - mat[0][0] * mat[1][2]) / det,
        (mat[1][0] * mat[2][1] - mat[1][1] * mat[2][0]) / det,
        (mat[0][1] * mat[2][0] - mat[0][0] * mat[2][1]) / det,
        (mat[0][0] * mat[1][1] - mat[0][1] * mat[1][0]) / det
    );
}


uint generate_coefficients(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    float data = 0.0;
    [branch]
    if (pixel.x - OFFSET_COF_PRE_N.x < 3 && pixel.y == OFFSET_COF_PRE_N.y) //if predictor coefficient
    {
        //float t_n+1 = 0;
        float t_n = -SolverLoadFloat(OFFSET_TIME_STEP);
        float t_n_m1 = t_n-get_data(0, _DATA_N);
        float t_n_m2 = t_n_m1-get_data(1, _DATA_N);
        switch (STEP_ORDER)
        {
            case 0:
                data = 1;
                data = (pixel.x - OFFSET_COF_PRE_N.x > 0) ? 0.0 : data;
                break;
            case 1:
                float2x2 mat = float2x2(
                    1, t_n,
                    1, t_n_m1
                );
                mat = inverse_matrix(mat);
                data = mat[0][pixel.x - OFFSET_COF_PRE_N.x];
                data = (pixel.x - OFFSET_COF_PRE_N.x > 1) ? 0.0 : data;
                break;
            case 2:
                float3x3 mat2 = float3x3(
                    1, t_n, t_n * t_n,
                    1, t_n_m1, t_n_m1 * t_n_m1,
                    1, t_n_m2, t_n_m2 * t_n_m2
                );
                mat2 = inverse_matrix(mat2);
                data = mat2[0][pixel.x - OFFSET_COF_PRE_N.x];
                break;
        }
        result = SolverStoreFloat(data);
    }
    else if (pixel.x - OFFSET_COF_COR_NP1.x < 3 && pixel.y == OFFSET_COF_COR_NP1.y)//if corrector coefficient
    {
        float t_n = -SolverLoadFloat(OFFSET_TIME_STEP);
        float t_n_m1 = t_n - get_data(0, _DATA_N);
        switch (STEP_ORDER)
        {
            case 0:
            case 1:
                data = ((pixel.x - OFFSET_COF_COR_NP1.x) ? 1.0 : -1.0) / t_n;
                data = (pixel.x - OFFSET_COF_COR_NP1.x > 1) ? 0.0 : data;
                break;
            case 2:
                float3x3 mat2 = float3x3(
                    1, 0, 0,
                    1, t_n, t_n * t_n,
                    1, t_n_m1, t_n_m1 * t_n_m1
                );
                mat2 = inverse_matrix(mat2);
                data = mat2[1][pixel.x - OFFSET_COF_COR_NP1.x];
                break;
        }
        result = SolverStoreFloat(data);
    }
    return result;
}

uint generate_predictor_vector(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    if (pixel.y == OFFSET_PREDICTOR_VECTOR.y || pixel.y == OFFSET_NR_VECTOR.y){
        float data = 0.0;
        switch (STEP_ORDER)
        {
            case 0:
                data = get_data(0, pixel.x) * SolverLoadFloat(OFFSET_COF_PRE_N + uint2(0, 0));
                break;
            case 1:
                data = get_data(0, pixel.x) * SolverLoadFloat(OFFSET_COF_PRE_N + uint2(0, 0)) + get_data(1, pixel.x) * SolverLoadFloat(OFFSET_COF_PRE_NM1);
                break;
            case 2:
                data = get_data(0, pixel.x) * SolverLoadFloat(OFFSET_COF_PRE_N + uint2(0, 0)) + get_data(1, pixel.x) * SolverLoadFloat(OFFSET_COF_PRE_NM1) + get_data(2, pixel.x) * SolverLoadFloat(OFFSET_COF_PRE_NM2);
                break;
        }
        result = SolverStoreFloat(data);
    }
    return result;
}


float calc_normalized_error()
{
    float normalized_error = 0;
    [loop]
    for (uint i = 0; i < _DATA_N; i++)
    {
        normalized_error = max(normalized_error,
                            abs(SolverLoadFloat(OFFSET_NR_VECTOR + uint2(i, 0)) - SolverLoadFloat(OFFSET_PREDICTOR_VECTOR + uint2(i, 0)))
                            );
    }
    normalized_error = normalized_error * SolverLoadFloat(OFFSET_TIME_STEP) / (SolverLoadFloat(OFFSET_TIME_STEP) + get_data_i_data_im1_timestep(1) + ((STEP_ORDER == 1) ? 0 : get_data_i_data_im1_timestep(2)));
    return normalized_error;
}

uint determine_time_step_and_order(uint2 pixel)
{
    uint result = SolverLoadUInt(pixel);
    if (!any(pixel - OFFSET_STEP_ORDER))
    {
        result = SolverStoreUInt(min(2u, SolverLoadUInt(OFFSET_STEPS_N)));
    }
    else if (!any(pixel - OFFSET_TIME_STEP))
    {
        // TODO: Adaptive time-step adjustment. Preserve the existing time step for now.
        float err = calc_normalized_error();
        if (isnan(err) || err == 0)
        {
            result = SolverStoreFloat(SolverLoadFloat(OFFSET_TIME_STEP) * 1.5);
        }
        else
        {
            result = SolverStoreFloat(SolverLoadFloat(OFFSET_TIME_STEP) * min(1.1, pow(err / (MAX_NORM_ERR * 0.8), -1.0 / (STEP_ORDER + 1.0))));
        }

    }
    
    return result;
}


uint2 uv2texel(float2 uv)
{
    return uv * _MainTex_TexelSize.zw;
}



uint process(uint2 uv)
{
    uint result = 0u;
    switch (STATE)
    {
        case STATE_DETERMINE_TIME_STEP_AND_ORDER:
            result = determine_time_step_and_order(uv);
            break;
        case STATE_GENERATE_COEFFICIENTS:
            result = generate_coefficients(uv);
            break;
        case STATE_GENERATE_PREDICTOR_VECTOR:
            result = generate_predictor_vector(uv);
            break;
        case STATE_INITIALIZE:
            result = initialize(uv);
            break;
        case STATE_GENERATE_XDOT_VECTOR:
            result = generate_xdot_vector(uv);
            break;
        case STATE_GENERATE_MATRIX_and_VECTOR:
            result = generate_yacobi_matrix_and_vector(uv);
            break;
        case STATE_LU_DECOMPOSITION:
            result = lu_decomposition(uv);
            break;
        case STATE_SOLVE_VECTOR:
            result = solve_vector(uv);
            break;
        case STATE_UPDATE_NR_VECTOR:
            result = update_nr_vector(uv);
            break;
        case STATE_PUSH_TO_OUTPUT:
            result = push_to_output(uv);
            break;
        default:
            result = 0;
            break;
    }
    return result;
}


uint flowControl(uint2 pixel)
{
    //Update uint control fields and copy every other texel without conversion.
    uint data = SolverLoadUInt(pixel); //get current data to be processed
    
    if (!any(pixel - OFFSET_SOLVER_STATE))
    {
        //data means the previous instruction already done
        //control the flow of the solver
        if (data >= STATE_PUSH_TO_OUTPUT)
        {
            data = STATE_DETERMINE_TIME_STEP_AND_ORDER; //Infinite loop
        }
        else if (data == STATE_LU_DECOMPOSITION || data == STATE_SOLVE_VECTOR)
        {
            if (LOOP_I == _DATA_N - 1)
            {
                data += 1; // Next state
            }
        }
        else if (data == STATE_UPDATE_NR_VECTOR)
        {
            float invvec_len_squared = 0;
            
            [loop]
            for (uint i = 0; i < _DATA_N; i++)
            {
                invvec_len_squared += pow(abs(SolverLoadFloat(OFFSET_INVERSE_VECTOR + uint2(i, 0))), 2.0);
            }
            
            if (invvec_len_squared < PRECISION)
            {
                //NR iteration converged
                if (STEP_ORDER == 0)
                {
                    data = STATE_UPDATE_NR_VECTOR + 1; //Next state
                }
                else
                {
                    //TODO:Evaluate the error and determine use this answer or not
                    if (calc_normalized_error() > MAX_NORM_ERR)
                    {
                        data = STATE_DETERMINE_TIME_STEP_AND_ORDER;
                    }
                    else
                    {
                        data = STATE_UPDATE_NR_VECTOR + 1;
                    }
                }
            }
            else
            {
                data = STATE_GENERATE_XDOT_VECTOR; //Not converged, go to next NR iteration
            }

        }
        else
        {
            data += 1; //Next state
        }
    }
    else if (!any(pixel - OFFSET_LOOP_COUNTER))
    {
        //control the loop counter for lu decomposition and solving vector
        switch (STATE)
        {
            case STATE_LU_DECOMPOSITION:
            case STATE_SOLVE_VECTOR:
                if (data >= _DATA_N - 1)
                {
                    data = 0;
                }
                else
                {
                    data += 1;
                }
                break;
            default:
                data = 0;
                break;
        }
    }
    return SolverStoreUInt(data);
}