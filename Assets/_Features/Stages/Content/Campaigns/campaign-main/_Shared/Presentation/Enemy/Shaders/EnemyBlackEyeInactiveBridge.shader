Shader "Game/Enemy/BlackEyeInactiveBridge"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _B("B", Color) = (0,0,0,1)
        _W("W", Color) = (1,1,1,1)
        _Border("Border", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        [HDR] _EmissionColor("Color", Color) = (0,0,0,1)

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

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_InactiveNoiseMap); SAMPLER(sampler_InactiveNoiseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _Color;
            half4 _B;
            half4 _W;
            half4 _EmissionColor;
            half4 _InactiveTint;
            half _Border;
            half _Smoothness;
            half _Metallic;
            half _Cutoff;
            half _Surface;
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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
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

            half3 ApplyInactive(half3 color, half inactiveBlend, half noiseCoverage)
            {
                if (inactiveBlend <= half(0.0001) || noiseCoverage <= half(0.0001))
                {
                    return color;
                }

                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                half3 grayscale = luminance.xxx;
                half3 inactiveTinted = lerp(grayscale, _InactiveTint.rgb, inactiveBlend * half(0.35));
                return lerp(color, inactiveTinted, saturate(inactiveBlend * noiseCoverage * _DesaturateStrength));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = saturate(half(1.0) - saturate(dot(normalWS, viewDirWS)));
                half borderMask = step(_Border, fresnel);

                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = albedoSample.a * _BaseColor.a;
                half inactiveBlend = saturate(_InactiveBlend);
                half noiseCoverage = EnemyInactiveNoiseCoverage(input.uv);
                half inactiveMask = inactiveBlend * noiseCoverage;
                half3 albedo = lerp(_B.rgb, _W.rgb, borderMask) * albedoSample.rgb;
                albedo = ApplyInactive(albedo, inactiveBlend, noiseCoverage);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = alpha;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = half(1.0);
                surfaceData.emission = _EmissionColor.rgb * (half(1.0) - (_EmissionSuppression * inactiveMask));
                surfaceData.clearCoatMask = half(0.0);
                surfaceData.clearCoatSmoothness = half(0.0);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = alpha;
                return color;
            }
            ENDHLSL
        }
    }
}
