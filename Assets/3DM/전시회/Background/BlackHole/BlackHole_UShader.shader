Shader "Custom/URP/BlackHoleSphereAnimated"
{
    Properties
    {
        [Header(Event Horizon)]
        _HorizonRadius
        (
            "Horizon Radius",
            Range(0.05, 0.8)
        ) = 0.36

        [HDR] _HorizonColor
        (
            "Horizon Color",
            Color
        ) = (0, 0, 0, 1)


        [Header(Animated Gravitational Lens)]
        _LensRadius
        (
            "Lens Radius",
            Range(0.2, 1.0)
        ) = 0.96

        _LensStrength
        (
            "Lens Strength",
            Range(-0.08, 0.08)
        ) = 0.018

        _LensRotationSpeed
        (
            "Lens Rotation Speed",
            Range(-4, 4)
        ) = 0.45

        _SwirlStrength
        (
            "Lens Swirl Strength",
            Range(0, 2)
        ) = 0.55

        _RippleFrequency
        (
            "Ripple Frequency",
            Range(1, 30)
        ) = 9

        _RippleSpeed
        (
            "Ripple Speed",
            Range(-10, 10)
        ) = 1.8

        _RippleStrength
        (
            "Ripple Strength",
            Range(0, 1)
        ) = 0.35

        _LensNoiseScale
        (
            "Lens Noise Scale",
            Range(1, 20)
        ) = 5

        _LensNoiseSpeed
        (
            "Lens Noise Speed",
            Range(0, 5)
        ) = 0.45

        _LensNoiseStrength
        (
            "Lens Noise Strength",
            Range(0, 1)
        ) = 0.35


        [Header(Photon Ring)]
        _PhotonOffset
        (
            "Photon Ring Offset",
            Range(0, 0.2)
        ) = 0.045

        _PhotonWidth
        (
            "Photon Ring Width",
            Range(0.002, 0.1)
        ) = 0.018

        [HDR] _PhotonColor
        (
            "Photon Ring Color",
            Color
        ) = (1, 0.38, 0.035, 1)

        _PhotonEmission
        (
            "Photon Emission",
            Range(0, 30)
        ) = 7


        [Header(Accretion Disk)]
        _DiskRadius
        (
            "Disk Radius",
            Range(0.1, 1.0)
        ) = 0.68

        _DiskWidth
        (
            "Disk Width",
            Range(0.005, 0.3)
        ) = 0.10

        _DiskFeather
        (
            "Disk Edge Feather",
            Range(0.001, 0.15)
        ) = 0.025

        _DiskFlatten
        (
            "Disk Flatten",
            Range(0.02, 1.0)
        ) = 0.18

        _DiskAngle
        (
            "Disk Screen Angle",
            Range(-180, 180)
        ) = 8

        _DiskSpeed
        (
            "Disk Rotation Speed",
            Range(-4, 4)
        ) = 0.45

        _StreakCount
        (
            "Disk Streak Count",
            Range(1, 100)
        ) = 32

        _DiskNoiseScale
        (
            "Disk Noise Scale",
            Range(1, 30)
        ) = 9

        _Doppler
        (
            "Doppler Brightness",
            Range(0, 1.5)
        ) = 0.55

        _DiskEmission
        (
            "Disk Emission",
            Range(0, 30)
        ) = 5

        [HDR] _DiskInnerColor
        (
            "Disk Inner Color",
            Color
        ) = (1, 0.9, 0.42, 1)

        [HDR] _DiskOuterColor
        (
            "Disk Outer Color",
            Color
        ) = (1, 0.025, 0.002, 1)


        [Header(Sphere Edge)]
        _EdgeFade
        (
            "Sphere Edge Fade",
            Range(0.001, 0.2)
        ) = 0.055
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BlackHoleSphere"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite Off
            ZTest LEqual

            Blend One Zero

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"


            static const float BH_TWO_PI = 6.28318530718;


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;

                UNITY_VERTEX_OUTPUT_STEREO
            };


            CBUFFER_START(UnityPerMaterial)

                float _HorizonRadius;
                half4 _HorizonColor;

                float _LensRadius;
                float _LensStrength;
                float _LensRotationSpeed;
                float _SwirlStrength;

                float _RippleFrequency;
                float _RippleSpeed;
                float _RippleStrength;

                float _LensNoiseScale;
                float _LensNoiseSpeed;
                float _LensNoiseStrength;

                float _PhotonOffset;
                float _PhotonWidth;
                half4 _PhotonColor;
                float _PhotonEmission;

                float _DiskRadius;
                float _DiskWidth;
                float _DiskFeather;
                float _DiskFlatten;
                float _DiskAngle;
                float _DiskSpeed;
                float _StreakCount;
                float _DiskNoiseScale;
                float _Doppler;
                float _DiskEmission;

                half4 _DiskInnerColor;
                half4 _DiskOuterColor;

                float _EdgeFade;

            CBUFFER_END


            float2 Rotate2D(float2 position, float angle)
            {
                float sineValue = sin(angle);
                float cosineValue = cos(angle);

                return float2
                (
                    cosineValue * position.x -
                    sineValue * position.y,

                    sineValue * position.x +
                    cosineValue * position.y
                );
            }


            float Hash21(float2 position)
            {
                position = frac(
                    position *
                    float2(123.34, 456.21)
                );

                position += dot(
                    position,
                    position + 45.32
                );

                return frac(
                    position.x * position.y
                );
            }


            float ValueNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 localPosition = frac(position);

                localPosition =
                    localPosition *
                    localPosition *
                    (3.0 - 2.0 * localPosition);

                float value00 =
                    Hash21(cell);

                float value10 =
                    Hash21(
                        cell + float2(1, 0)
                    );

                float value01 =
                    Hash21(
                        cell + float2(0, 1)
                    );

                float value11 =
                    Hash21(
                        cell + float2(1, 1)
                    );

                float bottomValue =
                    lerp(
                        value00,
                        value10,
                        localPosition.x
                    );

                float topValue =
                    lerp(
                        value01,
                        value11,
                        localPosition.x
                    );

                return lerp(
                    bottomValue,
                    topValue,
                    localPosition.y
                );
            }


            float FBM(float2 position)
            {
                float result = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int index = 0; index < 4; index++)
                {
                    result +=
                        ValueNoise(position) *
                        amplitude;

                    position =
                        Rotate2D(
                            position * 2.03,
                            0.51
                        );

                    amplitude *= 0.5;
                }

                return result;
            }


            float RingMask
            (
                float radius,
                float targetRadius,
                float width
            )
            {
                float distanceFromRing =
                    abs(radius - targetRadius);

                return 1.0 -
                    smoothstep(
                        width,
                        width * 2.5,
                        distanceFromRing
                    );
            }


            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );

                output.positionCS =
                    positionInputs.positionCS;

                output.screenPos =
                    ComputeScreenPos(
                        positionInputs.positionCS
                    );

                output.normalWS =
                    TransformObjectToWorldNormal(
                        input.normalOS
                    );

                return output;
            }


            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalVS =
                    normalize(
                        TransformWorldToViewDir(
                            normalize(input.normalWS),
                            true
                        )
                    );

                float2 spherePosition =
                    normalVS.xy;

                float sphereRadius =
                    saturate(
                        length(spherePosition)
                    );

                float2 radialDirection =
                    spherePosition /
                    max(sphereRadius, 0.0001);

                float2 tangentDirection =
                    float2
                    (
                        -radialDirection.y,
                        radialDirection.x
                    );


                float sphereEdgeFade =
                    1.0 -
                    smoothstep(
                        1.0 - _EdgeFade,
                        1.0,
                        sphereRadius
                    );


                float2 screenUV =
                    input.screenPos.xy /
                    max(
                        input.screenPos.w,
                        0.00001
                    );

                float screenAspect =
                    _ScreenParams.x /
                    max(_ScreenParams.y, 1.0);


                half3 originalSceneColor =
                    SampleSceneColor(screenUV);


                float currentTime =
                    _Time.y;

                float lensInnerMask =
                    smoothstep(
                        _HorizonRadius - 0.01,
                        _HorizonRadius + 0.065,
                        sphereRadius
                    );

                float lensOuterMask =
                    1.0 -
                    smoothstep(
                        _LensRadius - 0.12,
                        _LensRadius,
                        sphereRadius
                    );

                float lensMask =
                    lensInnerMask *
                    lensOuterMask *
                    sphereEdgeFade;


                float lensRange =
                    max(
                        _LensRadius -
                        _HorizonRadius,
                        0.001
                    );

                float lensProfile =
                    1.0 -
                    saturate
                    (
                        (
                            sphereRadius -
                            _HorizonRadius
                        ) / lensRange
                    );

                lensProfile *= lensProfile;


                float2 rotatingLensPosition =
                    Rotate2D
                    (
                        spherePosition,
                        currentTime *
                        _LensRotationSpeed
                    );

                float2 lensNoisePosition =
                    rotatingLensPosition *
                    _LensNoiseScale;

                lensNoisePosition +=
                    float2
                    (
                        currentTime * _LensNoiseSpeed,
                        -currentTime *
                        _LensNoiseSpeed * 0.73
                    );

                float lensNoise =
                    FBM(lensNoisePosition);


                float radialRipple =
                    sin
                    (
                        sphereRadius *
                        _RippleFrequency *
                        BH_TWO_PI -

                        currentTime *
                        _RippleSpeed *
                        BH_TWO_PI +

                        lensNoise * 3.0
                    );


                float swirlWave =
                    sin
                    (
                        sphereRadius * 14.0 -

                        currentTime *
                        _LensRotationSpeed *
                        BH_TWO_PI +

                        lensNoise * 6.0
                    );

                float swirlAmount =
                    swirlWave *
                    _SwirlStrength;


                float2 distortionDirection =
                    normalize
                    (
                        radialDirection +
                        tangentDirection *
                        swirlAmount
                    );


                distortionDirection.x /=
                    screenAspect;


                float noiseMultiplier =
                    1.0 +
                    (
                        lensNoise * 2.0 - 1.0
                    ) *
                    _LensNoiseStrength;

                float rippleMultiplier =
                    1.0 +
                    radialRipple *
                    _RippleStrength;


                float distortionAmount =
                    _LensStrength *
                    lensProfile *
                    lensMask *
                    noiseMultiplier *
                    rippleMultiplier;

                distortionAmount =
                    clamp(
                        distortionAmount,
                        -0.12,
                        0.12
                    );


                float2 distortedScreenUV =
                    screenUV +
                    distortionDirection *
                    distortionAmount;

                distortedScreenUV =
                    saturate(
                        distortedScreenUV
                    );


                half3 distortedSceneColor =
                    SampleSceneColor(
                        distortedScreenUV
                    );


                half3 finalColor =
                    lerp
                    (
                        originalSceneColor,
                        distortedSceneColor,
                        lensMask
                    );


                float photonRadius =
                    _HorizonRadius +
                    _PhotonOffset;

                float photonMask =
                    RingMask
                    (
                        sphereRadius,
                        photonRadius,
                        _PhotonWidth
                    );

                photonMask *=
                    sphereEdgeFade;


                float photonFlow =
                    0.72 +
                    0.28 *
                    sin
                    (
                        atan2(
                            spherePosition.y,
                            spherePosition.x
                        ) * 8.0 -

                        currentTime *
                        _LensRotationSpeed *
                        5.0 +

                        lensNoise * 4.0
                    );

                half3 photonEmission =
                    _PhotonColor.rgb *
                    photonMask *
                    photonFlow *
                    _PhotonEmission;


                float diskAngleRadians =
                    _DiskAngle *
                    0.01745329252;

                float2 diskPosition =
                    Rotate2D
                    (
                        spherePosition,
                        diskAngleRadians
                    );

                float2 ellipsePosition =
                    float2
                    (
                        diskPosition.x,

                        diskPosition.y /
                        max(_DiskFlatten, 0.001)
                    );

                float ellipseRadius =
                    length(ellipsePosition);

                float distanceFromDisk =
                    abs(
                        ellipseRadius -
                        _DiskRadius
                    );

                float diskMask =
                    1.0 -
                    smoothstep
                    (
                        _DiskWidth,
                        _DiskWidth +
                        _DiskFeather,
                        distanceFromDisk
                    );

                diskMask *=
                    sphereEdgeFade;


                float diskPolarAngle =
                    atan2
                    (
                        ellipsePosition.y,
                        ellipsePosition.x
                    );

                float2 rotatingDiskPosition =
                    Rotate2D
                    (
                        diskPosition,
                        currentTime *
                        _DiskSpeed
                    );

                float diskNoise =
                    FBM
                    (
                        rotatingDiskPosition *
                        _DiskNoiseScale +

                        float2
                        (
                            currentTime *
                            _DiskSpeed * 0.4,

                            -currentTime *
                            _DiskSpeed * 0.27
                        )
                    );


                float streakPattern =
                    0.5 +
                    0.5 *
                    sin
                    (
                        diskPolarAngle *
                        _StreakCount -

                        currentTime *
                        _DiskSpeed *
                        _StreakCount +

                        ellipseRadius * 22.0 +

                        diskNoise * 5.0
                    );


                float flowBrightness =
                    lerp
                    (
                        0.25,
                        1.8,
                        saturate
                        (
                            streakPattern * 0.65 +
                            diskNoise * 0.55
                        )
                    );


                float diskHeat =
                    1.0 -
                    saturate
                    (
                        (
                            ellipseRadius -
                            (
                                _DiskRadius -
                                _DiskWidth
                            )
                        ) /
                        max(
                            _DiskWidth * 2.0,
                            0.001
                        )
                    );


                half3 diskColor =
                    lerp
                    (
                        _DiskOuterColor.rgb,
                        _DiskInnerColor.rgb,
                        diskHeat
                    );


                float dopplerBrightness =
                    1.0 +
                    (
                        diskPosition.x /
                        max(_DiskRadius, 0.001)
                    ) *
                    _Doppler;

                dopplerBrightness =
                    max(
                        dopplerBrightness,
                        0.08
                    );


                half3 diskEmission =
                    diskColor *
                    diskMask *
                    flowBrightness *
                    dopplerBrightness *
                    _DiskEmission;


                float frontDiskMask =
                    1.0 -
                    smoothstep(
                        -0.015,
                        0.015,
                        diskPosition.y
                    );

                float backDiskMask =
                    1.0 -
                    frontDiskMask;


                float horizonMask =
                    1.0 -
                    smoothstep
                    (
                        _HorizonRadius - 0.012,
                        _HorizonRadius + 0.012,
                        sphereRadius
                    );


                finalColor +=
                    diskEmission *
                    backDiskMask;

                finalColor +=
                    photonEmission;

                finalColor =
                    lerp
                    (
                        finalColor,
                        _HorizonColor.rgb,
                        horizonMask
                    );

                finalColor +=
                    diskEmission *
                    frontDiskMask;


                finalColor =
                    lerp
                    (
                        originalSceneColor,
                        finalColor,
                        sphereEdgeFade
                    );


                return half4(
                    finalColor,
                    1.0
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}
