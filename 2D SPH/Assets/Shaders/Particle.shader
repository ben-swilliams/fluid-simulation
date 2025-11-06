Shader "Custom/Particle" {
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

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            StructuredBuffer<float3> positions;
            StructuredBuffer<float3> velocities;
            StructuredBuffer<float> properties;

            // X: Velocity, Y: Density, Z: Pressure
            float3 colourTarget;
            float3 propMaxes;

            float lowHue;
            float highHue;
            float size;
            float restDensity;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 col : TEXCOORD1;
                float3 normal : NORMAL;
            };

            v2f vert(appdata_full v, uint id : SV_InstanceID) {
                float3 centreWorld =  positions[id];
                float3 centreObj = mul(unity_WorldToObject, float4(centreWorld, 1)).xyz;
                float3 vertObj = centreObj + v.vertex * size;

                float prop;
                if (colourTarget.x != 0) {
                    prop = length(velocities[id]);
                } else {
                    prop = properties[id];
                }
                float propMax = dot(colourTarget, propMaxes);

                float propNorm;
                if (colourTarget.y == 0) propNorm = saturate(prop / propMax);
                else {
                    float minDensity = (1 - propMaxes.y) * restDensity;
                    float maxDensity = (1 + propMaxes.y) * restDensity;
                    propNorm = saturate((prop - minDensity) / (maxDensity - minDensity));
                }

                float hue = lerp(lowHue, highHue, propNorm);
                float3 rgb = hsv2rgb(float3(hue, 1.0, 1.0));

                v2f o;
                o.pos = UnityObjectToClipPos(vertObj);
                o.col = rgb;
                o.uv = v.texcoord;
                o.normal = normalize(v.vertex);
                return o;
            }

			float4 frag (v2f i) : SV_Target
			{
                float3 normal = normalize(i.normal);
				float shading = saturate(dot(_WorldSpaceLightPos0.xyz, normal));
				shading = shading * 0.7 + 0.3;
				
				return float4(i.col * shading, 1);
			}
            ENDCG
        }
    }
}