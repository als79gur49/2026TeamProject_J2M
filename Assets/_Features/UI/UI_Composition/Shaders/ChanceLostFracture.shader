Shader "UI/ChanceLostFracture"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _IconUvRect ("Sprite UV Bounds", Vector) = (0,0,1,1)
        _CrackReveal ("Crack Reveal", Range(0,1)) = 0
        _Decay ("Corrosion Progress", Range(0,1)) = 0
        _Detach ("Fragment Detach", Range(0,1)) = 0
        _DecayColor ("Corrosion Color", Color) = (0.38,0.09,0.03,1)
        _BurnColor ("Corrosion Edge", Color) = (0.95,0.48,0.16,1)
        _EdgeColor ("Fracture Edge", Color) = (0.74,0.62,0.51,1)
        _Fragment ("Fragment Index", Range(0,9)) = 0
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _TextureSampleAdd, _ClipRect, _IconUvRect, _DecayColor, _BurnColor, _EdgeColor;
            float _CrackReveal, _Decay, _Detach, _Fragment;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = v.vertex;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            float cross2(float2 a, float2 b)
            {
                return a.x*b.y-a.y*b.x;
            }
            float hash(float2 p)
            {
                return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);
            }
            float valueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 blend = frac(p);
                blend = blend*blend*(3-2*blend);
                return lerp(
                    lerp(hash(cell),hash(cell+float2(1,0)),blend.x),
                    lerp(hash(cell+float2(0,1)),hash(cell+float2(1,1)),blend.x),
                    blend.y);
            }
            float facet(float2 p, float2 a, float2 b, float2 c, float2 d)
            {
                float winding = sign(cross2(b-a,c-a));
                float inside = min(min(winding*cross2(b-a,p-a), winding*cross2(c-b,p-b)),
                                   min(winding*cross2(d-c,p-c), winding*cross2(a-d,p-d)));
                float pixel = max(fwidth(inside), 0.00001);
                return smoothstep(-pixel,pixel,inside);
            }
            float segment(float2 p, float2 a, float2 b)
            {
                float2 d = b-a;
                return length(p-a-d*saturate(dot(p-a,d)/dot(d,d)));
            }
            float shardMask(float2 p, float index)
            {
                if (index < 1.5) return facet(p,float2(.19,.69),float2(.30,.67),float2(.32,.55),float2(.22,.55));
                if (index < 2.5) return facet(p,float2(.71,.52),float2(.79,.51),float2(.81,.41),float2(.72,.39));
                if (index < 3.5) return facet(p,float2(.30,.29),float2(.42,.29),float2(.41,.18),float2(.34,.17));
                if (index < 4.5) return facet(p,float2(.67,.68),float2(.76,.67),float2(.79,.58),float2(.69,.55));
                if (index < 5.5) return facet(p,float2(.20,.42),float2(.31,.43),float2(.33,.33),float2(.23,.32));
                if (index < 6.5) return facet(p,float2(.58,.31),float2(.70,.29),float2(.68,.20),float2(.59,.19));
                if (index < 7.5) return facet(p,float2(.64,.59),float2(.72,.58),float2(.73,.51),float2(.65,.49));
                if (index < 8.5) return facet(p,float2(.29,.49),float2(.38,.49),float2(.39,.40),float2(.30,.39));
                return facet(p,float2(.55,.28),float2(.63,.27),float2(.61,.18),float2(.55,.18));
            }
            half4 frag(v2f i):SV_Target
            {
                float2 uv = (i.uv-_IconUvRect.xy)/max(_IconUvRect.zw-_IconUvRect.xy, 0.0001);
                half4 c = (tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
                if (_Fragment > 0.5)
                {
                    // Each authored full-size image samples the same sprite as the intact icon.
                    c.a *= shardMask(uv,_Fragment);
                }
                else
                {
                    float2 p = uv;
                    // Keep the authored crack sharp while matching pieces detach from the helmet.
                    float d = segment(p,float2(0.37,0.87),float2(0.46,0.66));
                    d = min(d,segment(p,float2(0.46,0.66),float2(0.38,0.43)));
                    d = min(d,segment(p,float2(0.38,0.43),float2(0.48,0.14)));
                    d = min(d,segment(p,float2(0.46,0.66),float2(0.70,0.51)));
                    d = min(d,segment(p,float2(0.38,0.43),float2(0.22,0.29)));
                    float reveal = smoothstep(0,0.08,_CrackReveal-(0.9-uv.y));
                    float crackMask = (1-smoothstep(0.003,0.006,d))*reveal;
                    float rim = (1-smoothstep(0.009,0.014,d))*reveal;
                    float fresh = saturate(1-_CrackReveal)*2;
                    c.rgb = lerp(c.rgb,_EdgeColor.rgb,rim*saturate(fresh)*0.35);
                    // Restore the earlier broad, noisy rust spread without covering the dark insignia.
                    float corrosionNoise = lerp(valueNoise(p*9),valueNoise(p*28),0.35);
                    float corrosionThreshold = 0.48*corrosionNoise+0.07*p.y;
                    float corrosionFront = _Decay-corrosionThreshold;
                    float corrosion = smoothstep(-0.025,0.04,corrosionFront);
                    float lightSurface = smoothstep(0.40,0.55,dot(c.rgb,float3(0.299,0.587,0.114)));
                    c.rgb = lerp(c.rgb,_DecayColor.rgb,corrosion*lightSurface*0.80);
                    c.rgb *= lerp(float3(1,1,1),float3(0.97,0.83,0.75),corrosion*lightSurface);
                    float edge = 1-smoothstep(0.005,0.05,abs(corrosionFront));
                    float speckles = smoothstep(0.72,0.84,valueNoise(p*23))*corrosion;
                    c.rgb = lerp(c.rgb,_BurnColor.rgb,(edge*0.65+speckles*0.48)*lightSurface);

                    c.rgb = lerp(c.rgb,float3(0.065,0.055,0.05),crackMask);
                    float detached = 0;
                    detached = max(detached,shardMask(p,1)*smoothstep(.043,.09,_Detach));
                    detached = max(detached,shardMask(p,2)*smoothstep(.163,.21,_Detach));
                    detached = max(detached,shardMask(p,3)*smoothstep(.217,.264,_Detach));
                    detached = max(detached,shardMask(p,4)*smoothstep(.272,.319,_Detach));
                    detached = max(detached,shardMask(p,5)*smoothstep(.337,.384,_Detach));
                    detached = max(detached,shardMask(p,6)*smoothstep(.402,.449,_Detach));
                    detached = max(detached,shardMask(p,7)*smoothstep(.467,.514,_Detach));
                    detached = max(detached,shardMask(p,8)*smoothstep(.533,.58,_Detach));
                    detached = max(detached,shardMask(p,9)*smoothstep(.587,.634,_Detach));
                    c.a *= 1-detached;
                }
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
