#ifndef SHADER_DEBUGGER_INCLUDED
#define SHADER_DEBUGGER_INCLUDED

#if defined(LETAI_DBG)

#define DBG_TAG_BOX    1
#define DBG_TAG_LINE   2
#define DBG_TAG_ARROW  3
#define DBG_TAG_CIRCLE 4
#define DBG_TAG_QUADRATIC  5
#define DBG_TAG_VALUE  6

struct DbgRec
{
    uint   tag;
    uint   color;
    float4 data0;
    float4 data1;
    uint2  pixel;
};

RWStructuredBuffer<DbgRec> _LeTai_DbgBuf : register(u1);
RWStructuredBuffer<uint>   _LeTai_DbgCount : register(u2);
uint                       _LeTai_DbgCapacity;
int                        _LeTai_DbgPixelRadius;
int                        _LeTai_DbgTraceMode;
int                        _LeTai_DbgEnabled;
int                        _LeTai_DbgDrawEnabled;

static bool  _dbgOn     = false;
static bool  _dbgCenter = false;
static uint2 _dbgPixel  = uint2(0, 0);
static uint  _dbgMark   = 0;

void dbgInitAt(int2 p, int2 sel)
{
    int2 d     = p - sel;
    _dbgOn     = _LeTai_DbgEnabled != 0 && (_LeTai_DbgPixelRadius < 0 || all(abs(d) <= _LeTai_DbgPixelRadius));
    _dbgCenter = all(d == 0);
    _dbgPixel  = uint2(p);
    _dbgMark   = 0;
}

void dbgEmit(uint tag, uint col, float4 d0, float4 d1)
{
    if (!_dbgOn) return;
    if (tag != DBG_TAG_VALUE && _LeTai_DbgDrawEnabled == 0) return;
    if (_LeTai_DbgTraceMode != 0 && (tag != DBG_TAG_VALUE || _dbgMark == 0)) return;
    uint idx;
    InterlockedAdd(_LeTai_DbgCount[0], 1, idx);
    if (idx >= _LeTai_DbgCapacity) return;
    DbgRec r;
    r.tag              = tag;
    r.color            = col;
    r.data0            = d0;
    r.data1            = d1;
    r.pixel            = _dbgPixel;
    _LeTai_DbgBuf[idx] = r;
}

bool dbgNoFilterBegin()
{
    bool previous = _dbgOn;
    _dbgOn        = _LeTai_DbgEnabled != 0;
    return previous;
}

void dbgNoFilterEnd(bool previous)
{
    _dbgOn = previous;
}

uint DbgCol(float4 rgba)
{
    uint4 rgba8 = uint4(saturate(rgba) * 255.0 + 0.5);
    return rgba8.r | (rgba8.g << 8) | (rgba8.b << 16) | (rgba8.a << 24);
}

static const float4 TAB10_BLUE   = float4(.0137, .1845, .4564, 1);
static const float4 TAB10_ORANGE = float4(1, .2122, .0044, 1);
static const float4 TAB10_GREEN  = float4(.0252, .3515, .0252, 1);
static const float4 TAB10_RED    = float4(.6724, .0203, .0212, 1);
static const float4 TAB10_PURPLE = float4(.2961, .1356, .5089, 1);
static const float4 TAB10_BROWN  = float4(.2623, .0931, .0704, 1);
static const float4 TAB10_PINK   = float4(.7682, .1845, .5395, 1);
static const float4 TAB10_GRAY   = float4(.2122, .2122, .2122, 1);
static const float4 TAB10_OLIVE  = float4(.5029, .5089, .0160, 1);
static const float4 TAB10_CYAN   = float4(.0086, .5149, .6240, 1);

static const uint DBG_COL_BLUE   = DbgCol(TAB10_BLUE);
static const uint DBG_COL_ORANGE = DbgCol(TAB10_ORANGE);
static const uint DBG_COL_GREEN  = DbgCol(TAB10_GREEN);
static const uint DBG_COL_RED    = DbgCol(TAB10_RED);
static const uint DBG_COL_PURPLE = DbgCol(TAB10_PURPLE);
static const uint DBG_COL_BROWN  = DbgCol(TAB10_BROWN);
static const uint DBG_COL_PINK   = DbgCol(TAB10_PINK);
static const uint DBG_COL_GRAY   = DbgCol(TAB10_GRAY);
static const uint DBG_COL_OLIVE  = DbgCol(TAB10_OLIVE);
static const uint DBG_COL_CYAN   = DbgCol(TAB10_CYAN);

uint DbgFade(uint col, float opacity)
{
    uint alpha = (uint)round(((col >> 24) & 0xFFu) * saturate(opacity));
    return (col & 0x00FFFFFFu) | (alpha << 24);
}

uint DbgHeat(float w)
{
    return DbgCol(lerp(TAB10_BLUE, TAB10_ORANGE, saturate(w)));
}

float4 dbgF4(float x)
{
    return float4(x, 0, 0, 0);
}

float4 dbgF4(float2 x)
{
    return float4(x, 0, 0);
}

float4 dbgF4(float3 x)
{
    return float4(x, 0);
}

float4 dbgF4(float4 x)
{
    return x;
}

#define DBG_NO_FILTER_BEGIN()           dbgNoFilterBegin()
#define DBG_NO_FILTER_END(previous)     dbgNoFilterEnd(previous)

#define DBG_INIT(pos, sel)              dbgInitAt(int2(pos), int2(sel))
#define DBG_MARK(n)                     (_dbgMark = (n))
#define DBG_BOX(min, max, col)           dbgEmit(DBG_TAG_BOX,       col, float4((min).xy, (max).xy), float4(0, 0, 0, 0))
#define DBG_LINE(a, b, col)             dbgEmit(DBG_TAG_LINE,      col, float4((a).xy, (b).xy), float4(0, 0, 0, 0))
#define DBG_ARROW(a, b, col)            dbgEmit(DBG_TAG_ARROW,     col, float4((a).xy, (b).xy), float4(0, 0, 0, 0))
#define DBG_CIRCLE(center, radius, col) dbgEmit(DBG_TAG_CIRCLE,    col, float4((center).xy, (radius), 0), float4(0, 0, 0, 0))
#define DBG_QUADRATIC(curve, col)       dbgEmit(DBG_TAG_QUADRATIC, col, float4((curve).A, (curve).B), float4((curve).C, (curve).tMin, (curve).tMax))
#define DBG_VALUE(id, x)                dbgEmit(DBG_TAG_VALUE,     0, dbgF4(x), float4((float)(id), 0, 0, 0))
#define DBG_TRACE(id, x)                { DBG_MARK(1); DBG_VALUE(id, x); DBG_MARK(0); }

#define DBG_CODE(x) x

#else

#define DBG_NO_FILTER_BEGIN() false
#define DBG_NO_FILTER_END(previous)

#define DBG_INIT(pos, sel)
#define DBG_MARK(n)
#define DBG_BOX(min, max, col)
#define DBG_LINE(a, b, col)
#define DBG_ARROW(a, b, col)
#define DBG_CIRCLE(center, radius, col)
#define DBG_QUADRATIC(curve, col)
#define DBG_VALUE(id, x)
#define DBG_TRACE(id, x)
#define DBG_CODE(x)

#endif

#endif
