Shader "Custom/MarchingCubes"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            StructuredBuffer<float3> positions;

            Texture3D<float> DensityTex;
            SamplerState samplerDensityTex;

            float3 containerSize;
            float size;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 col : TEXCOORD1;
                float3 normal : NORMAL;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata_full v, uint id : SV_InstanceID)
            {
                v2f o;

                float3 centre = positions[id];
                float3 worldPos = centre + v.vertex * size * 0.5;
            
                o.pos = UnityWorldToClipPos(worldPos);
                o.uv = v.texcoord;
                o.col = float3(1, 1, 1);
                o.normal = normalize(v.vertex);
                o.worldPos = centre;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 texCoord = (i.worldPos + containerSize / 2) / containerSize;
                float density = DensityTex.Sample(samplerDensityTex, texCoord);
                
                float restDensity = 5.0;
                float allowance = 0.01;

                float diff = restDensity * allowance;
                float min = restDensity - diff;
                float max = restDensity + diff;

                float normDensity = (density - min) / (min + max);
                return float4(normDensity, 0, 0, 1);
            }
            ENDCG
        }
    }
}
