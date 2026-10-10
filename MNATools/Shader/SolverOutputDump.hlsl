#ifndef SOLVER_OUTPUT_DUMP_INCLUDED
#define SOLVER_OUTPUT_DUMP_INCLUDED

Texture2D<uint> _MainTex;
uint _DATA_N;
float _OutputDeltaTime;

#define OUTPUT_DUMP_FORMAT_X       0u
#define OUTPUT_DUMP_DATA_N_X       1u
#define OUTPUT_DUMP_COUNT_X        2u
#define OUTPUT_DUMP_SEQUENCE_X     3u
#define OUTPUT_DUMP_DELTA_TIME_X   4u
#define OUTPUT_DUMP_LAST_TIME_X    5u
#define OUTPUT_DUMP_GENERATION_X   6u
#define OUTPUT_DUMP_DATA_Y         1u
#define OUTPUT_DUMP_CAPACITY       1000u

uint OutputDumpLoadUInt(uint x, uint y)
{
    return _MainTex.Load(int3(x, y, 0));
}

float OutputDumpLoadFloat(uint x, uint y)
{
    return asfloat(OutputDumpLoadUInt(x, y));
}

uint OutputDumpCount()
{
    return min(OUTPUT_DUMP_CAPACITY, OutputDumpLoadUInt(OUTPUT_DUMP_COUNT_X, 0u));
}

float get_data(uint historyIndex, uint variable)
{
    if(historyIndex >= OutputDumpCount() || variable >= _DATA_N) return 0.0;
    return OutputDumpLoadFloat(variable, OUTPUT_DUMP_DATA_Y + historyIndex);
}

float get_output_time(uint historyIndex)
{
    if(historyIndex >= OutputDumpCount()) return 0.0;
    return OutputDumpLoadFloat(_DATA_N, OUTPUT_DUMP_DATA_Y + historyIndex);
}

float get_data_i_data_im1_timestep(uint historyIndex)
{
    return _OutputDeltaTime;
}

#endif
