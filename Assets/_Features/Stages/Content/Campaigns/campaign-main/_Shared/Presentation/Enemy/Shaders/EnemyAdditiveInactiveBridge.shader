Shader "Game/Enemy/AdditiveInactiveBridge"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        _BaseMap("Base Map", 2D) = "white" {}
        PA_SSS_Texture2D_("Polygon Arsenal Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,0.5)
        _Color("Color", Color) = (1,1,1,0.5)
        _TintColor("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)

        _InactiveBlend("Inactive Blend", Range(0.0, 1.0)) = 0.0
        _InactiveNoiseReveal("Inactive Noise Reveal", Range(0.0, 1.0)) = 0.0
        _DesaturateStrength("Desaturate Strength", Range(0.0, 1.0)) = 0.85
        _InactiveTint("Inactive Tint", Color) = (0.62, 0.64, 0.68, 1.0)
        _EmissionSuppression("Emission Suppression", Range(0.0, 1.0)) = 0.85
        _InactiveNoiseMap("Inactive Noise Map", 2D) = "white" {}
        _InactiveNoiseStrength("Inactive Noise Strength", Range(0.0, 1.0)) = 0.0
        _InactiveNoiseScale("Inactive Noise Scale", Float) = 1.0
        _InactiveNoiseEdgeWidth("Inactive Noise Edge Width", Range(0.0001, 1.0)) = 0.08
        _InactiveNoiseThreshold("Inactive Noise Threshold", Range(0.0, 1.0)) = 0.5

        _Cull("__cull", Float) = 0.0
        _SrcBlend("__src", Float) = 5.0
        _DstBlend("__dst", Float) = 1.0
        _SrcBlendAlpha("__srcA", Float) = 1.0
        _DstBlendAlpha("__dstA", Float) = 1.0
        _ZWrite("__zw", Float) = 0.0
        _BlendOp("__blendOp", Float) = 0.0
        _Surface("__surface", Float) = 1.0
        _Blend("__blend", Float) = 2.0
        _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            BlendOp[_BlendOp]
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_InactiveNoiseMap); SAMPLER(sampler_InactiveNoiseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseMap_ST;
            float4 PA_SSS_Texture2D__ST;
            half4 _BaseColor;
            half4 _Color;
            half4 _TintColor;
            half4 _EmissionColor;
            half4 _InactiveTint;
            half _InactiveBlend;
            half _InactiveNoiseReveal;
            half _DesaturateStrength;
            half _EmissionSuppression;
            half _InactiveNoiseStrength;
            half _InactiveNoiseScale;
            half _InactiveNoiseEdgeWidth;
            half _InactiveNoiseThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half EnemyInactiveNoiseCoverage(float2 uv)
            {
                half reveal = saturate(_InactiveNoiseReveal);
                if (reveal <= half(0.0001))
                {
                    return half(0.0);
                }

                half noise = SAMPLE_TEXTURE2D(
                    _InactiveNoiseMap,
                    sampler_InactiveNoiseMap,
                    uv * max(_InactiveNoiseScale, half(0.0001))).r;
                half edge = max(_InactiveNoiseEdgeWidth, half(0.0001));
                half progress = lerp(-edge, half(1.0) + edge, reveal);
                half revealMask = smoothstep(noise - edge, noise + edge, progress);
                half strength = saturate(_InactiveNoiseStrength);
                half noiseMask = max(reveal * half(0.65), revealMask);
                return lerp(reveal, noiseMask, strength);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half inactiveBlend = saturate(_InactiveBlend);
                half suppressionMask = inactiveBlend * EnemyInactiveNoiseCoverage(input.uv);
                half emissionScale = half(1.0) - (_EmissionSuppression * suppressionMask);
                half4 color = tex * _BaseColor;
                color.rgb = (color.rgb + _EmissionColor.rgb) * emissionScale;
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
