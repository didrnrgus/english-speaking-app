Shader "Hidden/Paraform/Form-Sprite-Generate"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "PreviewType" = "Plane"
        }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        // TODO: cleanup

        #pragma target 4.5
        #pragma vertex vert
        #pragma shader_feature_fragment __ LETAI_DBG
        #include "formSpriteGenerateCommon.hlsl"
        #include "../Shaders/formSprite.hlsl"

        #include "ShaderDebugger.hlsl"

        #if defined(LETAI_DBG)
        float4 _LeTai_DbgPixelTarget;
        float4 _LeTai_DbgPixelContour;
        int    _LeTai_DbgGridRadius;
        #endif

        Texture2D _Corner;
        float     _AlphaThreshold;
        ENDHLSL

        Pass
        {
            Name "alphaPreprocess"

            HLSLPROGRAM
            #pragma fragment alphaPreprocessFrag
            #include "formSpriteGenerateContour.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Contour"

            HLSLPROGRAM
            #pragma fragment contourFrag
            #include "formSpriteGenerateContour.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ContourSmooth"

            HLSLPROGRAM
            #pragma fragment contourSmoothFrag
            #include "formSpriteGenerateContour.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "CornerDetect"

            HLSLPROGRAM
            #pragma fragment cornerDetectFrag

            #include "formSpriteGenerateCorner.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "JumpFloodSeed"

            HLSLPROGRAM
            #pragma fragment jumpFloodSeedFrag

            #include "formSpriteGenerateJumpFlood.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "JumpFlood"

            HLSLPROGRAM
            #pragma fragment jumpFloodFrag

            #include "formSpriteGenerateJumpFlood.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Resolve"

            HLSLPROGRAM
            #pragma fragment frag

            #include "formSpriteGenerateResolve.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Filter"

            HLSLPROGRAM
            #pragma fragment filterFrag

            #include "formSpriteGenerateFilter.hlsl"
            ENDHLSL
        }
    }
}
