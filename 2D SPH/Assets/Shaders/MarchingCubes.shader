Shader "Custom/MarchingCubes" {
    Properties {
    }

    SubShader {
        Tags {
            "Queue"="Geometry"
        }

        Pass {
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

            const float size = 1.0;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 col : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            v2f vert(appdata_full v, uint id : SV_InstanceID) {
                float3 centreWorld = positions[id];

                v2f o;
                float3 centreObj = mul(unity_WorldToObject, float4(centreWorld, 1)).xyz;
                float3 vertObj = centreObj + v.vertex * (size * 0.5);

                o.pos = UnityObjectToClipPos(vertObj);
                // Transform normal to world space
                o.normal = UnityObjectToWorldNormal(v.vertex);

                o.col = float4(0, 1, 0, 1);
                o.uv = v.texcoord;
                return o;
            }

			float4 frag (v2f i) : SV_Target
			{
                float shading = saturate(dot(_WorldSpaceLightPos0.xyz, i.normal));
                shading = shading * 0.7 + 0.3;

                return float4(1, 1, 1, 1);
			}
            ENDCG
        }
    }
}