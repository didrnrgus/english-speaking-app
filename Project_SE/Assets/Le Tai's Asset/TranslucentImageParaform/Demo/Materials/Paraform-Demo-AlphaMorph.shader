// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

Shader "LeTai/Paraform/Demo/Alpha Morph"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        _WidthMorph("Width Morph", Range(0, 1)) = 1
        _PositionMorph("Position Morph", Range(0, 1)) = 1

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest[unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../../Shaders/formSpriteAlphaMorph.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct VertexInput
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4    _Color;
            float4    _ClipRect;
            float2    _SourceSize;
            float4    _SpriteUVRect;
            float2    _SpriteSize;
            float     _WidthMorph;
            float     _PositionMorph;

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.worldPosition = input.vertex;
                output.vertex        = UnityObjectToClipPos(input.vertex);
                output.uv            = input.uv;
                output.color         = input.color * _Color;
                return output;
            }

            half4 frag(VertexOutput input) : SV_Target
            {
                float2 localUV  = (input.uv - _SpriteUVRect.xy) / _SpriteUVRect.zw;
                float  distance = sampleFormSpriteDistance(_MainTex,
                                                                 input.uv,
                                                                 _DistanceRange);
                float alpha = formSpriteMorphAlpha(_MainTex,
                                                            localUV,
                                                            _SourceSize,
                                                            _SpriteUVRect,
                                                            _SpriteSize,
                                                            _DistanceRange,
                                                            distance,
                                                            _WidthMorph,
                                                            _PositionMorph);
                half4 color = half4(input.color.rgb, input.color.a * alpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - .001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}