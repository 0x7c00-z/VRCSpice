#ifndef VRCSPICE_OSCILLOSCOPE_TRIGGER_COMMON_INCLUDED
#define VRCSPICE_OSCILLOSCOPE_TRIGGER_COMMON_INCLUDED

Texture2D<uint> _SolverDump;
int _Row1;
int _Row2;
uint _Stride;

#define OSC_RAW_CAPACITY 1000u
#define OSC_PRE_CAPACITY 100u
#define OSC_CAPTURE_CAPACITY 200u
#define OSC_POST_CAPACITY 100u
#define OSC_AUTO_FRAMES 120u

#define OSC_DISABLED 0u
#define OSC_PREPARING 1u
#define OSC_WAITING 2u
#define OSC_CAPTURING 3u
#define OSC_HOLD 4u

#define OSC_MODE_SINGLE 0u
#define OSC_MODE_NORMAL 1u
#define OSC_MODE_AUTO 2u

#define OSC_FLAG_RESET_PRE 1u
#define OSC_FLAG_CLEAR_DISPLAY 2u
#define OSC_FLAG_TRIGGER_STARTED 4u
#define OSC_FLAG_CAPTURE_COMPLETE 8u
#define OSC_FLAG_CLEAR_CAPTURE 16u
#define OSC_FLAG_TRIGGER_CHANGED 32u
#define OSC_FLAG_OVERRUN 64u

#define OSC_STATE_SEQUENCE 0u
#define OSC_STATE_GENERATION 1u
#define OSC_STATE_SAMPLING_REVISION 2u
#define OSC_STATE_TRIGGER_REVISION 3u
#define OSC_STATE_ACQUISITION 4u
#define OSC_STATE_PRE_COUNT 5u
#define OSC_STATE_READY_COUNT 6u
#define OSC_STATE_PREVIOUS_VALUE 7u
#define OSC_STATE_PREVIOUS_VALID 8u
#define OSC_STATE_POST_COUNT 9u
#define OSC_STATE_AUTO_WAIT 10u
#define OSC_STATE_CANDIDATE_START 11u
#define OSC_STATE_CANDIDATE_COUNT 12u
#define OSC_STATE_TRIGGER_ORDINAL 13u
#define OSC_STATE_APPEND_START 14u
#define OSC_STATE_APPEND_COUNT 15u
#define OSC_STATE_FLAGS 16u
#define OSC_STATE_OLD_POST_COUNT 17u
#define OSC_STATE_OLD_PRE_COUNT 18u
#define OSC_STATE_MODE 19u
#define OSC_STATE_ENABLED 20u
#define OSC_STATE_HAS_TRIGGER_DISPLAY 21u

uint OscSolverLoad(uint x, uint y)
{
    return _SolverDump.Load(int3(x, y, 0));
}

uint OscFirstAligned(uint value, uint stride)
{
    uint remainder = value % stride;
    return remainder == 0u ? value : value + stride - remainder;
}

uint OscCandidateCount(uint start, uint finish, uint stride)
{
    if(finish < start) return 0u;
    uint first = OscFirstAligned(start, stride);
    uint last = finish - finish % stride;
    return last < first ? 0u : (last - first) / stride + 1u;
}

uint OscCandidateSequence(uint firstSequence, uint ordinal, uint stride)
{
    return firstSequence + ordinal * stride;
}

uint OscChannelValue(int row, uint dataN, uint sourceY, out uint valid)
{
    if(row == -2)
    {
        valid = 1u;
        return asuint(0.0);
    }
    if(row >= 0 && (uint)row < dataN)
    {
        uint result = OscSolverLoad((uint)row, sourceY);
        valid = isfinite(asfloat(result)) ? 1u : 0u;
        return valid != 0u ? result : asuint(0.0);
    }
    valid = 0u;
    return asuint(0.0);
}

void OscLoadCandidate(uint firstSequence, uint ordinal, uint stride, uint latestSequence,
    out uint timeValue, out uint value1, out uint value2, out uint validBits)
{
    uint sampleSequence = OscCandidateSequence(firstSequence, ordinal, stride);
    uint sourceY = latestSequence - sampleSequence + 1u;
    uint dataN = OscSolverLoad(1u, 0u);
    uint valid1;
    uint valid2;
    value1 = OscChannelValue(_Row1, dataN, sourceY, valid1);
    value2 = OscChannelValue(_Row2, dataN, sourceY, valid2);
    timeValue = OscSolverLoad(dataN, sourceY);
    validBits = valid1 | (valid2 << 1u);
}

uint OscSelectedValid(uint validBits, uint source)
{
    return (validBits >> min(source, 1u)) & 1u;
}

float OscSelectedValue(uint value1, uint value2, uint source)
{
    return asfloat(source == 0u ? value1 : value2);
}

#endif
