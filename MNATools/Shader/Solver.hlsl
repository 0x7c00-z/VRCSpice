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
#define MAX_NORM_ERR 1.0
#define STEP_ORDER round(_MainTex[OFFSET_STEP_ORDER].r)

float get_data(uint index, uint row)
{
    //get data from output
    float data = _MainTex[OFFSET_OUT_BUFFER + uint2(row, index)].r; //get current data to be processed
    return data;
}

float get_data_i_data_im1_timestep(uint index)
{
    //get the timestep between datai and datai-1
    float data = _MainTex[OFFSET_OUT_BUFFER + uint2(_DATA_N, index)].r; //get current data to be processed
    return data;
}

float push_to_output(uint2 pixel)
{
    //push the current NR vector to the OFFSET_OUT_BUFFER + uint2(0, 0), and OFFSET_OUT_BUFFER + uint2(N, M) to OFFSET_OUT_BUFFER + uint2(N, M+1)
    float data = _MainTex[pixel].r; //get current data to be processed
    if (!any(pixel - OFFSET_STEPS_N))
    {
        data = asfloat(asuint(data) + 1);
    }
    if ((int) pixel.y > (int) OFFSET_OUT_BUFFER.y)
    {
        //slide and push
        
        data = _MainTex[pixel - uint2(0, 1)].r;
    }
    else if ((int) pixel.y == (int) OFFSET_OUT_BUFFER.y)
    {
        //push NR vector to output buffer
        data = _MainTex[OFFSET_NR_VECTOR + uint2(pixel.x, 0)].r;
        if (pixel.x == _DATA_N)
        {
            //save the delta time next to the last pixel of the output buffer
            data = _MainTex[OFFSET_TIME_STEP].r;
        }
    }
    return data;
}

float generate_yacobi_matrix_and_vector(uint2 pixel)
{
    //1st order gear method
    //solve $$A\mathbf{x}+B\mathbf{\dot{x}}+\mathbf{I_s}\odot\left[\exp\left(C\mathbf{x}\right)-1\right]-\mathbf{b_{rhs}} = 0$$
    //like (A+\frac{B}{\Delta t})\mathbf{x}^{n+1} + \mathbf{I_s}\odot\left[\exp\left(C\mathbf{x}^{n+1}\right)-1\right]-\mathbf{b_{rhs}} - \frac{B}{\Delta t}\mathbf{x}^n =0
    float data = _MainTex[pixel].r; //get current data to be processed
    //generate yacobi matrix here and store it in the texture
    if (pixel.y == 0)
    {
        return data;
    }
    else if (OFFSET_YACOBI_MATRIX.y <= pixel.y && pixel.y < OFFSET_YACOBI_MATRIX.y + _DATA_N)
    {
        //generate yacobi matrix
        pixel -= OFFSET_YACOBI_MATRIX;
        data = dfidxj(pixel.x, pixel.y) + dfidxdotj(pixel.x, pixel.y) / _MainTex[OFFSET_TIME_STEP].r;
    }
    else if (pixel.y == OFFSET_INVERSE_VECTOR.y)
    {
        //generate vector
        data = f(pixel.x);
    }
    return data;
}

float update_nr_vector(uint2 pixel)
{
    //update the vector by adding the solved vector to the current vector, and store it in the OFFSET_NR_VECTOR + uint2(0, 0) to OFFSET_NR_VECTOR + uint2(N-1, 0)
    float data = _MainTex[pixel].r; //get current data to be processed
    if (pixel.y == OFFSET_NR_VECTOR.y)
    {
        data -= max(-1.0, min(1.0, _MainTex[OFFSET_INVERSE_VECTOR + uint2(pixel.x, 0)].r));
    }
    return data;
}

float lu_decomposition(uint2 pixel)
{
    
//pivotting function to get the correct value from the texture after pivotting, this is used in the lu decomposition process.
//this replace LOOP_COUNTER with the max_index to get the correct value from the texture after pivotting
#define PIVOTTING(x) ((x == k) ? max_index : ((x == max_index) ? k : x))
    
    //perform lu decomposition on the yacobi matrix, and store the inverse of the diagonal of the yacobi matrix in the OFFSET_INVERSE_VECTOR + uint2(0, 0) to OFFSET_INVERSE_VECTOR + uint2(N-1, 0)
    float data = _MainTex[pixel].r; //get current data to be processed
    uint k = LOOP_I; //get current loop counter
    uint max_index = k;
    float max_value = abs(_MainTex[OFFSET_YACOBI_MATRIX + uint2(k, k)].r);
    if (pixel.y == 0)
    {
        return data;
    }
    if ((OFFSET_YACOBI_MATRIX.y <= pixel.y && pixel.y < OFFSET_YACOBI_MATRIX.y + _DATA_N) || pixel.y == OFFSET_INVERSE_VECTOR.y)//if yabobi matrix or inverse vector
    {
        [loop]
        for (uint i = k + 1; i < _DATA_N; i++)
        {
            //look for the maximum value in the current column to avoid numerical instability
            if (max_value < abs(_MainTex[OFFSET_YACOBI_MATRIX + uint2(i, k)].r))
            {
                max_value = abs(_MainTex[OFFSET_YACOBI_MATRIX + uint2(i, k)].r);
                max_index = i;
            }
        }
        
        data = _MainTex[uint2(PIVOTTING(pixel.x), pixel.y)].r;
        
        //multiply both INVERSE_VECTOR and YACOBI_MATRIX by same matrix do pivotting and lu decomposition at the same time
        if (pixel.x > k)
        {
            data -= _MainTex[uint2(PIVOTTING(k), pixel.y)] * _MainTex[OFFSET_YACOBI_MATRIX + uint2(PIVOTTING(pixel.x), k)] / _MainTex[OFFSET_YACOBI_MATRIX + uint2(PIVOTTING(k), k)].r;
        }
    }
    return data;

#undef PIVOTTING

}


float solve_vector(uint2 pixel)
{
    //perform forward and backward substitution to solve the vector, and store the solved vector in the OFFSET_INVERSE_VECTOR + uint2(0, 0) to OFFSET_INVERSE_VECTOR + uint2(N-1, 0)
    float data = _MainTex[pixel].r; //get current data to be processed
    if (pixel.y == OFFSET_INVERSE_VECTOR.y)
    {
        if (pixel.x == _DATA_N - 1 - LOOP_I)
        {
            for (uint i = pixel.x + 1; i < _DATA_N; i++)
            {
                data -= _MainTex[OFFSET_YACOBI_MATRIX + uint2(pixel.x, i)].r * _MainTex[OFFSET_INVERSE_VECTOR + uint2(i, 0)].r;
            }
            data /= _MainTex[OFFSET_YACOBI_MATRIX + uint2(pixel.x, pixel.x)].r;
        }
    }
    return data;
}

float generate_xdot_vector(uint2 pixel)
{
    //generate the xdot vector by using the solved vector and the previous vector, and store it in the OFFSET_XDOT_VECTOR + uint2(0, 0) to OFFSET_XDOT_VECTOR + uint2(N-1, 0)
    float data = _MainTex[pixel].r; //get current data to be processed
    if (pixel.y == OFFSET_XDOT_VECTOR.y)
    {
        data = _MainTex[OFFSET_COF_COR_NP1].r * _MainTex[OFFSET_NR_VECTOR + uint2(pixel.x, 0)].r + _MainTex[OFFSET_COF_COR_N].r * get_data(0, pixel.x)
            + _MainTex[OFFSET_COF_COR_NM1].r * get_data(1, pixel.x);
    }
    return data;
}

float initialize(uint2 pixel)
{
    float data = 0;
    if (!any(pixel - OFFSET_TIME_STEP))
    {
        data = _DeltaTime;
    }
    return data;
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


float generate_coefficients(uint2 pixel)
{
    float data = _MainTex[pixel].r; //get current data to be processed
    [branch]
    if (pixel.x - OFFSET_COF_PRE_N.x < 3 && pixel.y == OFFSET_COF_PRE_N.y) //if predictor coefficient
    {
        //float t_n+1 = 0;
        float t_n = -_MainTex[OFFSET_TIME_STEP].r;
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
    }
    else if (pixel.x - OFFSET_COF_COR_NP1.x < 3 && pixel.y == OFFSET_COF_COR_NP1.y)//if corrector coefficient
    {
        float t_n = -_MainTex[OFFSET_TIME_STEP].r;
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
    }
    return data;
}

float generate_predictor_vector(uint2 pixel)
{
    float data = _MainTex[pixel].r; //get current data to be processed
    if (pixel.y == OFFSET_PREDICTOR_VECTOR.y || pixel.y == OFFSET_NR_VECTOR.y){
        switch (STEP_ORDER)
        {
            case 0:
                data = get_data(0, pixel.x) * _MainTex[OFFSET_COF_PRE_N + uint2(0, 0)].r;
                break;
            case 1:
                data = get_data(0, pixel.x) * _MainTex[OFFSET_COF_PRE_N + uint2(0, 0)].r + get_data(1, pixel.x) * _MainTex[OFFSET_COF_PRE_NM1].r;
                break;
            case 2:
                data = get_data(0, pixel.x) * _MainTex[OFFSET_COF_PRE_N + uint2(0, 0)].r + get_data(1, pixel.x) * _MainTex[OFFSET_COF_PRE_NM1].r + get_data(2, pixel.x) * _MainTex[OFFSET_COF_PRE_NM2].r;
                break;
        }
    }
    return data;
}


float calc_normalized_error()
{
    float normalized_error = 0;
    [loop]
    for (uint i = 0; i < _DATA_N; i++)
    {
        normalized_error = max(normalized_error,
                            (abs(_MainTex[OFFSET_NR_VECTOR + uint2(i, 0)].r) - _MainTex[OFFSET_PREDICTOR_VECTOR + uint2(i, 0)].r)
                            );
    }
    normalized_error = normalized_error * _MainTex[OFFSET_TIME_STEP].r / (_MainTex[OFFSET_TIME_STEP].r + get_data_i_data_im1_timestep(1) + ((STEP_ORDER == 1) ? 0 : get_data_i_data_im1_timestep(2)));
    return normalized_error;
}

float determine_time_step_and_order(uint2 pixel)
{
    float data = _MainTex[pixel].r; //get current data to be processed
    if (!any(pixel - OFFSET_STEP_ORDER))
    {
        //カウンタもstateも普通に動いたのにこれは2^23をたさないと動かない？？？？←これも動いてない??←普通にfloatで一旦やりましょう
        uint order = min(2, asuint(_MainTex[OFFSET_STEPS_N].r));
        //order = 0; //DEBUG
        return order;

    }
    else if (!any(pixel - OFFSET_TIME_STEP))
    {
        /*float err = calc_normalized_error();
        if (isnan(err) || err == 0)
        {
            data = data * 1.5;
        }
        else
        {
            data = data * max(1.1, pow(err, -1.0 / (STEP_ORDER + 1.0)));
        }*/

    }
    return data;
}


uint2 uv2texel(float2 uv)
{
    return uv * _MainTex_TexelSize.zw;
}



float process(uint2 uv)
{
    switch (STATE)
    {
        case STATE_DETERMINE_TIME_STEP_AND_ORDER:
            return determine_time_step_and_order(uv);
        case STATE_GENERATE_COEFFICIENTS:
            return generate_coefficients(uv);
        case STATE_GENERATE_PREDICTOR_VECTOR:
            return generate_predictor_vector(uv);
        case STATE_INITIALIZE:
            return initialize(uv);
        case STATE_GENERATE_XDOT_VECTOR:
            return generate_xdot_vector(uv);
        case STATE_GENERATE_MATRIX_and_VECTOR:
            return generate_yacobi_matrix_and_vector(uv);
        case STATE_LU_DECOMPOSITION:
            return lu_decomposition(uv);
        case STATE_SOLVE_VECTOR:
            return solve_vector(uv);
        case STATE_UPDATE_NR_VECTOR:
            return update_nr_vector(uv);
        case STATE_PUSH_TO_OUTPUT:
            return push_to_output(uv);
        default:
            return 0;
    }
}


float flowControl(uint2 pixel)
{
    //control the flow of the solver by using the OFFSET_SOLVER_STATE and OFFSET_LOOP_COUNTER in the texture, and return 0 for all pixels except the control pixels
    uint data = asuint(_MainTex[pixel].r); //get current data to be processed
    
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
                invvec_len_squared += pow(abs(_MainTex[OFFSET_INVERSE_VECTOR + uint2(i, 0)].r), 2.0);
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
    return asfloat(data);
}