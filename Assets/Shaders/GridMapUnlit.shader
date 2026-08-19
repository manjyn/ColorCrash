Shader "Custom/GridMapUnlit"
{
    Properties
    {
        [NoScaleOffset] _DataTex ("Data Texture (Point Filter)", 2D) = "white" {}
        [NoScaleOffset] _TileArray ("Tile Patterns (2D Array)", 2DArray) = "white" {}
        _GridSize ("Grid Size (Width, Height)", Vector) = (24, 38, 0, 0)
        
        [Space(10)]
        _ColorNeutral ("Neutral Color", Color) = (0.5, 0.5, 0.5, 1)
        _ColorBlue ("Blue Team Color", Color) = (0, 0, 1, 1)
        _ColorRed ("Red Team Color", Color) = (1, 0, 0, 1)
        _ColorObstacle ("Obstacle Color", Color) = (0.15, 0.15, 0.15, 1)

        [Space(10)]
        [Header(Blue Boundary Spotlight)]
        _BlueBoundaryColor ("Blue Boundary Color", Color) = (0.2, 0.7, 1.0, 1.0)
        _BlueBoundaryWidth ("Blue Boundary Width", Range(0.01, 0.4)) = 0.12
        _BlueBoundaryIntensity ("Blue Boundary Intensity", Range(0.0, 5.0)) = 0.0
        _BlueBoundaryPulseSpeed ("Blue Boundary Pulse Speed", Range(0.0, 10.0)) = 2.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            
            // Unlit 패스
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 2D Array 텍스처 사용을 위해 타겟 3.5 이상 요구
            #pragma target 3.5 
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_DataTex);
            SAMPLER(sampler_DataTex); // Point 필터링에 맞춰 샘플링

            TEXTURE2D_ARRAY(_TileArray);
            SAMPLER(sampler_TileArray); // 타일 패턴은 Inspector의 Repeat로 설정

            CBUFFER_START(UnityPerMaterial)
                float4 _GridSize;
                half4 _ColorNeutral;
                half4 _ColorBlue;
                half4 _ColorRed;
                half4 _ColorObstacle;
                half4 _BlueBoundaryColor;
                half _BlueBoundaryWidth;
                half _BlueBoundaryIntensity;
                half _BlueBoundaryPulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 오브젝트 공간에서 클립 공간으로 정점 변환
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Data 텍스처 샘플링
                half4 dataColor = SAMPLE_TEXTURE2D(_DataTex, sampler_DataTex, input.uv);

                // 2. 값 디코딩 (0.0 ~ 1.0 범위를 0 ~ 255 정수로 복원)
                // Color32의 R(팀), G(무늬)
                float teamID = round(dataColor.r * 255.0);
                float patternID = round(dataColor.g * 255.0);

                // 3. Tile Array 텍스처 샘플링
                // 타일 무늬가 각 타일 칸마다 반복되도록 GridSize를 UV에 곱함
                float2 tileUV = input.uv * _GridSize.xy; 
                // URP의 SAMPLE_TEXTURE2D_ARRAY 매크로는 4개의 인자: (텍스처, 샘플러, uv, 인덱스)
                half4 patternColor = SAMPLE_TEXTURE2D_ARRAY(_TileArray, sampler_TileArray, tileUV, patternID);

                // 4. 색상 혼합 1=Blue, 2=Red, 3=Obstacle
                half isBlue     = step(abs(teamID - 1.0), 0.1); 
                half isRed      = step(abs(teamID - 2.0), 0.1);  
                half isObstacle = step(abs(teamID - 3.0), 0.1);
                half isNeutral  = 1.0 - (isBlue + isRed + isObstacle); // 1, 2, 3이 아니면 중립(0)으로 간주

                // 마스크 최종 틴트 컬러 결정
                half4 tintColor = (_ColorNeutral * isNeutral) + (_ColorBlue * isBlue) + (_ColorRed * isRed) + (_ColorObstacle * isObstacle);

                half4 finalColor = patternColor * tintColor;

                // 5. Blue 팀 외곽 경계 스포트라이트/글로우 연산
                if (isBlue > 0.5 && _BlueBoundaryIntensity > 0.001)
                {
                    float2 texelSize = 1.0 / _GridSize.xy;

                    // 4방향 이웃 타일의 UV
                    float2 uvRight = input.uv + float2(texelSize.x, 0.0);
                    float2 uvLeft  = input.uv - float2(texelSize.x, 0.0);
                    float2 uvUp    = input.uv + float2(0.0, texelSize.y);
                    float2 uvDown  = input.uv - float2(0.0, texelSize.y);

                    // 이웃 타일 샘플링 및 Blue 팀 여부 확인 (1.0 = Blue)
                    half nRight = (input.uv.x + texelSize.x < 1.0) ? step(abs(round(SAMPLE_TEXTURE2D(_DataTex, sampler_DataTex, uvRight).r * 255.0) - 1.0), 0.1) : 0.0;
                    half nLeft  = (input.uv.x - texelSize.x >= 0.0) ? step(abs(round(SAMPLE_TEXTURE2D(_DataTex, sampler_DataTex, uvLeft).r * 255.0) - 1.0), 0.1) : 0.0;
                    half nUp    = (input.uv.y + texelSize.y < 1.0) ? step(abs(round(SAMPLE_TEXTURE2D(_DataTex, sampler_DataTex, uvUp).r * 255.0) - 1.0), 0.1) : 0.0;
                    half nDown  = (input.uv.y - texelSize.y >= 0.0) ? step(abs(round(SAMPLE_TEXTURE2D(_DataTex, sampler_DataTex, uvDown).r * 255.0) - 1.0), 0.1) : 0.0;

                    // 타일 내부 local UV (0.0 ~ 1.0)
                    float2 cellUV = frac(input.uv * _GridSize.xy);

                    // 각 방향의 경계와의 거리 기반 글로우 계산 (이웃이 Blue가 아닌 경계면만 빛남)
                    half edgeRight = (1.0 - nRight) * smoothstep(_BlueBoundaryWidth, 0.0, 1.0 - cellUV.x);
                    half edgeLeft  = (1.0 - nLeft)  * smoothstep(_BlueBoundaryWidth, 0.0, cellUV.x);
                    half edgeUp    = (1.0 - nUp)    * smoothstep(_BlueBoundaryWidth, 0.0, 1.0 - cellUV.y);
                    half edgeDown  = (1.0 - nDown)  * smoothstep(_BlueBoundaryWidth, 0.0, cellUV.y);

                    // 4개 경계면 조화 합성
                    half borderGlow = saturate(edgeRight + edgeLeft + edgeUp + edgeDown);

                    // 시간 기반 부드러운 Pulse 애니메이션 (0.7 ~ 1.0 범위)
                    half pulse = 0.85 + 0.15 * sin(_Time.y * _BlueBoundaryPulseSpeed);

                    // 최종 스포트라이트 글로우 합성
                    half3 spotlightGlow = _BlueBoundaryColor.rgb * borderGlow * _BlueBoundaryIntensity * pulse;

                    finalColor.rgb += spotlightGlow;
                }

                // 6. 최종 픽셀 컬러 반환
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    // URP에서 지원하지 않는 렌더 파이프라인(Legacy 등)으로 fallback되는 현상 방지
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

