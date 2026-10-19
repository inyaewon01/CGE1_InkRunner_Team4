Shader "InkRunner/BlackInkPrototype/InkGaugeUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Source", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SourceMode ("0 Original, 1 Brush, 2 Red frame", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "GaugeUI"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _SourceMode;
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 texel = tex2D(_MainTex, input.texcoord) + _TextureSampleAdd;
                float redChroma = texel.r - max(texel.g, texel.b);
                float redAmount = smoothstep(0.035, 0.12, redChroma);
                if (_SourceMode > 1.5)
                {
                    // The left cap overlaps the brush in the original composite.
                    // Keep the red frame and discard the dark shaft / light bristles.
                    texel.a *= redAmount;
                }
                else if (_SourceMode > 0.5)
                {
                    // Preserve the illustration and its bow, while removing the red
                    // horizontal frame that runs behind its lower half. UVs refer to
                    // the full, untouched 2146 x 733 source, never the cropped rect.
                    float sourceY = (1.0 - input.texcoord.y) * 733.0;
                    float sourceX = input.texcoord.x * 2146.0;
                    float frameRegion = smoothstep(311.0, 319.0, sourceY);
                    frameRegion = max(frameRegion, smoothstep(359.0, 366.0, sourceX) *
                        smoothstep(286.0, 292.0, sourceY));
                    texel.a *= 1.0 - redAmount * frameRegion;
                }
                texel *= input.color;
                #ifdef UNITY_UI_CLIP_RECT
                texel.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(texel.a - 0.001);
                #endif
                return texel;
            }
            ENDCG
        }
    }
}
