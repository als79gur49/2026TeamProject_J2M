Shader "Hidden/TerminalIrisBeforeComparison"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 1
        _Feather ("Feather", Float) = 0.01
        _OuterColor ("Outer Color", Color) = (0, 0, 0, 1)
        _OuterOpacity ("Outer Opacity", Range(0, 1)) = 1
        _RimWidth ("Rim Width", Float) = 0
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0)
        _AspectRatio ("Aspect Ratio", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use UI Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
            Name "TerminalIrisBeforeComparison"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
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
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _RimColor;
            fixed4 _OuterColor;
            float4 _ClipRect;
            float2 _Center;
            float _Radius;
            float _Feather;
            float _OuterOpacity;
            float _RimWidth;
            float _AspectRatio;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 delta = input.texcoord - _Center;
                delta.x *= max(0.0001, _AspectRatio);
                float distanceFromCenter = length(delta);
                float feather = max(0.0001, _Feather);
                float outside = smoothstep(
                    _Radius - feather,
                    _Radius + feather,
                    distanceFromCenter);
                float rim = 1.0 - smoothstep(
                    max(0.0, _Radius - _RimWidth),
                    _Radius + feather,
                    distanceFromCenter);
                rim *= smoothstep(
                    max(0.0, _Radius - _RimWidth - feather),
                    max(0.0, _Radius - _RimWidth),
                    distanceFromCenter);
                float fullyClosed = 1.0 - step(0.0001, _Radius);
                outside = lerp(outside, 1.0, fullyClosed);
                rim *= 1.0 - fullyClosed;

                fixed outerAlpha = outside * _OuterOpacity * _OuterColor.a;
                fixed rimAlpha = rim * _RimColor.a;
                fixed combinedAlpha = saturate(outerAlpha + rimAlpha);
                fixed3 combinedRgb = combinedAlpha > 0.0001
                    ? (_OuterColor.rgb * outerAlpha + _RimColor.rgb * rimAlpha) /
                      combinedAlpha
                    : fixed3(0, 0, 0);
                fixed4 color = fixed4(combinedRgb, combinedAlpha);
                color *= input.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
