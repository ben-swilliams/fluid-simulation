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
            float size;
            float maxSpeed;

            float4 slowColour;
            float4 fastColour;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 vel : TEXCOORD1;
            };

            v2f vert (appdata_full v, uint id : SV_InstanceID) {
                float3 centreWorld = float3(positions[id], 0);
                float3 centreObj = mul(unity_WorldToObject, float4(centreWorld, 1)).xyz;
                float3 vertObj = centreObj + v.vertex * size;

                v2f o;

                o.pos = UnityObjectToClipPos(vertObj);
                o.vel = velocities[id];
                o.uv = v.texcoord;

                return o;
            }

            float4 frag (v2f i) : SV_Target {
                // UV -> -1..1 unit circle space
                float2 p = (i.uv - 0.5) * 2;

                float distSq = dot(p, p);
                
                // AA boundary - 0.5 makes it slightly sharper
                float w = fwidth(distSq) * 0.5;
                
                // Anti-alias the edge of the circle
                float alpha = 1 - smoothstep(1 - w, 1 + w, distSq);

                float speed = length(i.vel);
                float normSpeed = saturate(speed / maxSpeed);

                float4 colour = lerp(slowColour, fastColour, normSpeed);
                colour.a = alpha;

                return colour;
            }
            ENDCG
        }
    }
}