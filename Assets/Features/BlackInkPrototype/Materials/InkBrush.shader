Shader "InkRunner/Black Ink Brush"
{
    Properties
    {
        _InkColor ("Ink color", Color) = (0.012, 0.013, 0.016, 1)
        _EdgeRoughness ("Brush edge roughness", Range(0, 1)) = 0.65
        [HideInInspector] _Thickness ("World thickness", Float) = 1
        [HideInInspector] _TextureScale ("World texture scale", Float) = 1
        [HideInInspector] _StrokeLength ("Stroke arc length in UV units", Float) = 1
        [HideInInspector] _ArcOffset ("Stable reverse-stroke texture offset", Float) = 0
        [HideInInspector] _NoiseSeed ("Stroke texture seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Black Ink Unlit"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex InkVertex
            #pragma fragment InkFragment
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                float _Thickness;
                float _TextureScale;
                float _StrokeLength;
                float _ArcOffset;
                float _NoiseSeed;
                float _EdgeRoughness;
            CBUFFER_END

            float InkHash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float InkNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(InkHash(cell), InkHash(cell + float2(1, 0)), f.x),
                    lerp(InkHash(cell + float2(0, 1)), InkHash(cell + 1.0), f.x), f.y);
            }

            Varyings InkVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 InkFragment(Varyings input) : SV_Target
            {
                float arc = input.uv.x;
                float depth = input.uv.y;
                float grainX = arc + _ArcOffset + _NoiseSeed;
                float roughness = saturate(_EdgeRoughness);

                // Opaque throughout the physical platform. The procedural wash
                // changes pigment color very slightly, never cutting holes in it.
                float paper = InkNoise(float2(grainX * 25.0, depth * 27.0));
                float brushGrain = InkNoise(float2(grainX * 2.4, depth * 92.0));
                float pigment = 0.87 + paper * 0.13 + brushGrain * 0.16;
                half3 ink = _InkColor.rgb * pigment;

                // At depth 1 the collider ends. Extra visual fringe has an
                // irregular solid root and short, softly bleeding brush fibers.
                float edgeNoise = InkNoise(float2(grainX * 13.0, 3.4));
                float fineEdge = InkNoise(float2(grainX * 69.0, 7.1));
                float fringeLength = lerp(0.035, 0.075 + edgeNoise * 0.12 + fineEdge * 0.035, roughness);
                float lowerAlpha = 1.0 - smoothstep(1.0, 1.0 + fringeLength, depth);
                float lowerFibers = InkNoise(float2(grainX * 111.0, depth * 6.0));
                float fringe = saturate((depth - 1.0) / max(fringeLength, 0.001));
                lowerAlpha *= lerp(1.0, lerp(0.28, 1.0, lowerFibers), fringe * roughness);

                // Mesh has a small extension at each end. Different transverse
                // bristle lengths break the silhouette without rounded tube caps.
                float outsideArc = max(-arc, arc - _StrokeLength);
                float tipDistance = max(0.0, outsideArc) * _TextureScale / max(_Thickness, 0.001);
                float strand = floor(depth * 49.0);
                float strandLength = lerp(0.028, 0.174,
                    InkHash(float2(strand + _NoiseSeed * 17.0, outsideArc > -arc ? 9.3 : 2.8)));
                float tipAlpha = 1.0 - smoothstep(max(0.001, strandLength - 0.018), strandLength, tipDistance);
                float tipTaper = 1.0 - smoothstep(1.03, 1.24, depth);
                tipAlpha = lerp(tipAlpha * tipTaper, 1.0, step(outsideArc, 0.0));

                half alpha = (half)(lowerAlpha * tipAlpha * _InkColor.a);
                clip(alpha - 0.008h);
                return half4(ink, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
