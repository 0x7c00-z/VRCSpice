#ifndef FONT_ASSET_INCLUDED
#define FONT_ASSET_INCLUDED

//このへんの関数群はジオメトリシェーダ想定。
//フラグメントではfontfragのみ実行。

#define DECIMAL_POINT '.'
#define DECIMAL_MINUS '-'

struct fontv2f
{
    float2 uv : TEXCOORD0;
    uint charcode : TEXCOORD1;
    float4 vertex : SV_POSITION;
};

Texture2D _FontTex;
float4 _FontColor;

fixed4 readpixbyind(uint ind)
{
    uint w, h;
    _FontTex.GetDimensions(w, h);
                //w = _MainTex_TexelSize.z;
    return _FontTex[uint2(ind % w, ind / w)];
}

float getAspect(uint charcode)
{
    float4 data = readpixbyind(charcode);
    uint2 charSize = data.yx * 255;
    
    return (float) charSize.x / (float) charSize.y;

}


//six vertex par char
//returns character width
float putChar(float2 LeftDownPos, float lineheight, in float space, uint charcode, inout TriangleStream<fontv2f> stream)
{
    fontv2f o;
    o.charcode = charcode;
    float2 size = lineheight;
    size.x = lineheight * getAspect(o.charcode);
    LeftDownPos.x = -LeftDownPos.x;
    
    o.uv = 1; //(1, 1)
    size.x = -size.x;
    o.vertex = UnityObjectToClipPos(float4(LeftDownPos + o.uv * size, 0, 1));
    stream.Append(o);
    o.uv.y = 0; //(1, 0)
    o.vertex = UnityObjectToClipPos(float4(LeftDownPos + o.uv * size, 0, 1));
    stream.Append(o);
    o.uv.x = 0; //(0, 0)
    o.vertex = UnityObjectToClipPos(float4(LeftDownPos + o.uv * size, 0, 1));
    stream.Append(o);
    stream.RestartStrip();
    o.uv.y = 1; //(0, 1)
    o.vertex = UnityObjectToClipPos(float4(LeftDownPos + o.uv * size, 0, 1));
    stream.Append(o);
    o.uv.x = 1; //(1, 1)
    o.vertex = UnityObjectToClipPos(float4(LeftDownPos + o.uv * size, 0, 1));
    stream.Append(o);
    o.uv = 0; //(0, 0)
    o.vertex = UnityObjectToClipPos(float4(LeftDownPos + o.uv * size, 0, 1));
    stream.Append(o);
    stream.RestartStrip();
    
    return -size.x + lineheight * space;
}

//returns digits width
float putFloatChar(in float2 LeftDownPos, in float lineheight, in float space, in float value, in uint digits, inout TriangleStream<fontv2f> stream)
{
    float2 cursur = 0;
    int expo;
    uint char;
    if (value < 0)
    {
        if (!digits--)
        {
            return cursur.x;
        }
        cursur.x += putChar(LeftDownPos + cursur, lineheight, space, DECIMAL_MINUS, stream);
        value = -value;
    }
    expo = max(log10(value), -1);
    
    while (digits > 0)
    {
        if (expo == -1)
        {
            if (!digits--)
            {
                break;
            }
            cursur.x += putChar(LeftDownPos + cursur, lineheight, space, DECIMAL_POINT, stream);
        }
        
        if (!digits--)
        {
            break;
        }
        char = (uint) (value / pow(10.0f, (float)expo)) % 10;
        cursur.x += putChar(LeftDownPos + cursur, lineheight, space, '0' + char, stream);
        value = value % pow(10.0f, (float)expo);
        expo--;
        if (value < 0)
        {
            value = 0;
        }
        if (value == 0)
        {
            break;
        }

    }
    return cursur.x;
}

///Fragment shader
fixed4 fontfrag(fontv2f i) : SV_Target
{
    
    
    // 1. テーブルから文字情報を読み出す
    float4 data = readpixbyind(i.charcode); // i.indは33などの文字コード [cite: 4, 6]
                
    // 2. 文字の縦横サイズ (0.0~1.0を0~255に戻す)
    uint charH = data.r * 255;
    uint charW = data.g * 255;
                
    // 3. UVから「何ビット目か」を計算(うごいてない)
    uint px = (i.uv.x * (float) charW);
    uint py = (i.uv.y * (float) charH);

                //DEBUG
                //return fixed4(px%2, py%2, 0, 1);

    uint bit = px + py * charW;
            
    // 4. データ開始アドレス (b=上位, a=下位)
    uint pointer = (uint) (data.b * 255.1) * 256 + (uint) (data.a * 255.1);
                
    // 5. 対象のテクセル(32bit分)を読み出す
    fixed4 pix = readpixbyind(pointer + bit / 32);
                
    // 6. 4バイトをuint(32bit)に結合してビット抽出
    uint4 b4 = uint4((float4) pix * 255.1);
    uint combined = b4.r | (b4.g << 8) | (b4.b << 16) | (b4.a << 24);
                
    uint val = (combined >> (bit % 32)) & 1;
            
    if (!val)
    {
        discard;
    }
    return _FontColor;
}

#endif