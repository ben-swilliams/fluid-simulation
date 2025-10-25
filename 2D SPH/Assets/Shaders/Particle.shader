Shader "Custom/Particle" {
    Properties {
    }

    SubShader {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            StructuredBuffer<float2> positions;
            StructuredBuffer<float2> velocities;
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
            };

            float3 hsv2rgb(float3 c) {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert(appdata_full v, uint id : SV_InstanceID) {
                float3 centreWorld = float3(positions[id], 0);
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
                return o;
            }

            float4 frag(v2f i) : SV_Target {
                float2 p = (i.uv - 0.5) * 2;
                float distSq = dot(p, p);

                float w = fwidth(distSq) * 0.5;
                float alpha = 1.0 - smoothstep(1.0 - w, 1.0 + w, distSq);

                return float4(i.col, alpha);
            }
            ENDCG
        }
    }
}