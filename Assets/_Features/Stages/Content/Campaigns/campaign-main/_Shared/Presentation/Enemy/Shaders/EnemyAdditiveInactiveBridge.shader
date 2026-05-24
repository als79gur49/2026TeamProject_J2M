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
        _DesaturateStrength("Desaturate Strength", Range(0.0, 1.0)) = 0.85
        _InactiveTint("Inactive Tint", Color) = (0.62, 0.64, 0.68, 1.0)
        _EmissionSuppression("Emission Suppression", Range(0.0, 1.0)) = 0.85

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
            half _DesaturateStrength;
            half _EmissionSuppression;
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

            half3 ApplyInactive(half3 color, half inactiveBlend)
            {
                if (inactiveBlend <= half(0.0001))
                {
                    return color;
                }

                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                half3 grayscale = luminance.xxx;
                half3 inactiveTinted = lerp(grayscale, _InactiveTint.rgb, inactiveBlend * half(0.35));
                return lerp(color, inactiveTinted, saturate(inactiveBlend * _DesaturateStrength));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half inactiveBlend = saturate(_InactiveBlend);
                half emissionScale = half(1.0) - (_EmissionSuppression * inactiveBlend);
                half4 color = tex * _BaseColor;
                color.rgb = ApplyInactive(color.rgb, inactiveBlend);
                color.rgb = (color.rgb + _EmissionColor.rgb) * emissionScale;
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
