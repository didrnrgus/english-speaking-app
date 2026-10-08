Shader "LeTai/TranslucentImage/Paraform/Demo/Grid"
{
    Properties
    {
        _CellSize ("Cell Size", Float) = 1
        [PowerSlider(2)] _LineThickness ("Line Thickness", Range(0, 1)) = 0.02
        _Subgrid ("Subgrid Divisions", Integer) = 4
        _SubgridWidth ("Subgrid Width", Range(0, 1)) = 0.5
        _SubgridOpacity ("Subgrid Opacity", Range(0, 1)) = 0.5
        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 1)
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float  _CellSize;
            float  _LineThickness;
            int    _Subgrid;
            float  _SubgridWidth;
            float  _SubgridOpacity;
            float4 _BackgroundColor;
            float4 _LineColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 gridPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                float2 worldScale = float2(
                    length(mul(unity_ObjectToWorld, float4(1, 0, 0, 0)).xyz),
                    length(mul(unity_ObjectToWorld, float4(0, 1, 0, 0)).xyz)
                );
                o.gridPos = v.vertex.xy * worldScale;
                return o;
            }

            float gridSdf(float2 position, float cellSize)
            {
                float2 cell = abs(frac(position / cellSize) - 0.5);
                return cellSize * (0.5 - max(cell.x, cell.y));
            }

            float gridCoverage(float2 position, float cellSize, float lineThickness)
            {
                float gridDistance = gridSdf(position, cellSize) - lineThickness;
                float pixelWidth   = fwidth(gridDistance);
                float lineWidth    = max(lineThickness, pixelWidth);
                float lineCoverage = lineThickness / lineWidth;
                return lineCoverage * (1.0 - smoothstep(0.0, pixelWidth, gridDistance));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float cellSize      = _CellSize;
                float majorCoverage = gridCoverage(i.gridPos, cellSize, _LineThickness);

                float subgrid          = _Subgrid;
                float subgridCellSize  = cellSize / subgrid;
                float subgridThickness = _LineThickness * _SubgridWidth;
                float subgridCoverage  = gridCoverage(i.gridPos, subgridCellSize, subgridThickness) * _SubgridOpacity;
                float lineCoverage     = max(majorCoverage, subgridCoverage);

                return lerp(_BackgroundColor, _LineColor, lineCoverage);
            }
            ENDCG
        }
    }
}
