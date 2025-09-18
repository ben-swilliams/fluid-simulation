Shader "Custom/GPUInstancing2D"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float4x4> _PositionBuffer;
            #endif
            
            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    unity_ObjectToWorld = _PositionBuffer[unity_InstanceID];
                    unity_WorldToObject = unity_ObjectToWorld;

                    unity_WorldToObject._11_12_13_14 = unity_ObjectToWorld._11_21_31_41;
                    unity_WorldToObject._21_22_23_24 = unity_ObjectToWorld._12_22_32_42;
                    unity_WorldToObject._31_32_33_34 = unity_ObjectToWorld._13_23_33_43;
                    unity_WorldToObject._41_42_43_44 = unity_ObjectToWorld._14_24_34_44;
                #endif
            }
            
            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                
                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                return col;
            }
            ENDCG
        }
    }
}