Shader "TraceCurve/Mask UI" {
    Properties {
        _MainTex ("Main", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
    }

    SubShader {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZWrite Off
        ZTest Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            uniform sampler2D _MainTex;
            uniform sampler2D _MaskTex;
            uniform float4 _MainTex_ST;
            uniform float4 _MaskTex_ST;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            struct app2vert
            {
                float4 position: POSITION;
                half4 color: COLOR;
                float2 texcoord: TEXCOORD0;
            };

            struct vert2frag
            {
                float4 position: POSITION;
                half4 color: COLOR;
                float2 texcoord: TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            vert2frag vert(app2vert input)
            {
                vert2frag output;
                output.worldPosition = input.position;
                output.position = UnityObjectToClipPos(input.position);
        #ifdef UNITY_HALF_TEXEL_OFFSET
                output.position.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1);
        #endif
				output.color = input.color;
                output.texcoord = input.texcoord;
                return output;
            }

            float4 frag(vert2frag input) : COLOR
            {
                float4 main_color = tex2D(_MainTex, input.texcoord) + _TextureSampleAdd;
                main_color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
        #ifdef UNITY_UI_ALPHACLIP
                clip (main_color.a - 0.001);
        #endif
                float4 mask_color = tex2D(_MaskTex, input.texcoord);
                float4 value = float4(input.color.r * main_color.r, input.color.g * main_color.g, input.color.b * main_color.b, input.color.a * main_color.a * mask_color.r);
                return value;
            }
            ENDCG
        }
    }
}