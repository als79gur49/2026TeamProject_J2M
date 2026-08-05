Shader "UI/TerminalIris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 1
        _ClosedOvershootPixels ("Closed Overshoot Pixels", Float) = 0
        _OuterColor ("Outer Color", Color) = (0, 0, 0, 1)
        _OuterOpacity ("Outer Opacity", Range(0, 1)) = 1
        _EdgeAntiAliasScale ("Edge Anti-Alias Scale", Float) = 0.82
        _MinimumAAPixels ("Minimum AA Pixels", Float) = 0.82
        _ArtisticFeatherHalfWidthPixels ("Artistic Feather Half Width Pixels", Float) = 0
        _RimWidthPixels ("Rim Width Pixels", Float) = 0
        _RimSoftnessPixels ("Rim Softness Pixels", Float) = 0.85
        _RimFadeOutPixels ("Rim Fade Out Pixels", Float) = 3
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0)
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
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
            Name "TerminalIris"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _RimColor;
            half4 _OuterColor;
            float4 _ClipRect;
            float2 _Center;
            float _Radius;
            float _ClosedOvershootPixels;
            float _OuterOpacity;
            float _EdgeAntiAliasScale;
            float _MinimumAAPixels;
            float _ArtisticFeatherHalfWidthPixels;
            float _RimWidthPixels;
            float _RimSoftnessPixels;
            float _RimFadeOutPixels;

            v2f vert(appdata_t v)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = v.vertex;
                output.vertex = UnityObjectToClipPos(v.vertex);
                output.texcoord = v.texcoord;
                output.color = v.color;
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                float pixelUnit = rcp(max(1.0, _ScreenParams.y));
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 delta = input.texcoord - _Center;
                delta.x *= aspect;
                float effectiveRadius =
                    _Radius - max(0.0, _ClosedOvershootPixels) * pixelUnit;
                float signedDistance = length(delta) - effectiveRadius;

                float2 distanceDerivative = float2(
                    ddx(signedDistance),
                    ddy(signedDistance));
                float derivativeWidth =
                    length(distanceDerivative) * _EdgeAntiAliasScale;
                float aaWidth = max(
                    derivativeWidth,
                    _MinimumAAPixels * pixelUnit);
                float artisticFeather =
                    _ArtisticFeatherHalfWidthPixels * pixelUnit;
                float edgeWidth = aaWidth + artisticFeather;
                float outside = smoothstep(-edgeWidth, edgeWidth, signedDistance);

                float halfRimWidth = 0.5 * _RimWidthPixels * pixelUnit;
                float rimSoftness = max(
                    aaWidth,
                    _RimSoftnessPixels * pixelUnit);
                float rimDistance = abs(signedDistance);
                float rim = 1.0 - smoothstep(
                    max(0.0, halfRimWidth - rimSoftness),
                    halfRimWidth + rimSoftness,
                    rimDistance);
                float effectiveRadiusPixels = max(0.0, effectiveRadius / pixelUnit);
                float rimFadeStartPixels =
                    0.5 * _RimWidthPixels +
                    _MinimumAAPixels +
                    _ArtisticFeatherHalfWidthPixels +
                    _RimSoftnessPixels;
                float rimFadeEndPixels =
                    rimFadeStartPixels + max(0.001, _RimFadeOutPixels);
                rim *= smoothstep(
                    rimFadeStartPixels,
                    rimFadeEndPixels,
                    effectiveRadiusPixels);
                rim *= step(0.0001, _RimWidthPixels);

                half outerAlpha =
                    (half)(outside * _OuterOpacity * _OuterColor.a);
                half rimAlpha = (half)(rim * _RimColor.a);
                half combinedAlpha =
                    rimAlpha + outerAlpha * (1.0h - rimAlpha);
                half3 premultipliedRgb =
                    _RimColor.rgb * rimAlpha +
                    _OuterColor.rgb * outerAlpha * (1.0h - rimAlpha);
                half3 combinedRgb = combinedAlpha > 0.0001h
                    ? premultipliedRgb / combinedAlpha
                    : half3(0, 0, 0);
                half4 color = half4(combinedRgb, combinedAlpha);
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
