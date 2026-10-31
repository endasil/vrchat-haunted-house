Shader "Haunted/DirtyMirror"
{
    Properties
    {
        _MainTex("Glass Albedo", 2D) = "white" {}
        _CrackMap("Crack Map", 2D) = "white" {}
        _ReflectionTint("Reflection Tint", Color) = (0.6, 0.65, 0.6, 1)
        _HazeColor("Haze Color", Color) = (0.55, 0.6, 0.55, 1)
        _HazeAmount("Haze Amount", Range(0, 1)) = 0.25
        _GrimeAmount("Grime Amount", Range(0, 1)) = 0.5
        _GrimeScale("Grime Scale", Float) = 5
        _EdgeWidth("Edge Grime Width", Range(0.01, 0.5)) = 0.2
        _DesilverColor("Desilvered Color", Color) = (0.08, 0.07, 0.05, 1)
        _DesilverAmount("Desilvered Amount", Range(0, 1)) = 0.5
        _CrackColor("Crack Color", Color) = (0.9, 0.95, 0.9, 1)
        _CrackStrength("Crack Strength", Range(0, 4)) = 2
        _Distortion("Distortion", Range(0, 0.05)) = 0.01
        _BlurAmount("Blur Amount", Range(0, 0.02)) = 0.008
        _FilmGlow("Film Glow", Range(0, 0.2)) = 0.01
        _AmbientAmount("Ambient Light Amount", Range(0, 1)) = 0.15
        [HideInInspector] _ReflectionTex0("", 2D) = "white" {}
        [HideInInspector] _ReflectionTex1("", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _CrackMap;
            sampler2D _ReflectionTex0;
            sampler2D _ReflectionTex1;

            half4 _ReflectionTint;
            half4 _HazeColor;
            half _HazeAmount;
            half _GrimeAmount;
            float _GrimeScale;
            half _EdgeWidth;
            half4 _DesilverColor;
            half _DesilverAmount;
            half4 _CrackColor;
            half _CrackStrength;
            half _Distortion;
            half _BlurAmount;
            half _FilmGlow;
            half _AmbientAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 refl : TEXCOORD1;
                half3 ambient : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                float4 pos : SV_POSITION;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 blend = f * f * (3.0 - 2.0 * f);
                float bottom = lerp(hash(cell), hash(cell + float2(1, 0)), blend.x);
                float top = lerp(hash(cell + float2(0, 1)), hash(cell + float2(1, 1)), blend.x);
                return lerp(bottom, top, blend.y);
            }

            float fbm(float2 p)
            {
                return valueNoise(p) * 0.5 + valueNoise(p * 2.0) * 0.3 + valueNoise(p * 4.0) * 0.2;
            }

            half3 SampleReflection(float2 screenUV)
            {
                return unity_StereoEyeIndex == 0
                    ? tex2D(_ReflectionTex0, screenUV).rgb
                    : tex2D(_ReflectionTex1, screenUV).rgb;
            }

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.refl = ComputeNonStereoScreenPos(o.pos);
                o.ambient = ShadeSH9(float4(UnityObjectToWorldNormal(v.normal), 1.0));
                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half3 albedo = tex2D(_MainTex, i.uv).rgb;
                half crack = saturate((1.0 - tex2D(_CrackMap, i.uv).r) * _CrackStrength);
                float grime = fbm(i.uv * _GrimeScale);

                float2 edgeDistances = min(i.uv, 1.0 - i.uv);
                half edge = 1.0 - saturate(min(edgeDistances.x, edgeDistances.y) / _EdgeWidth);

                float streaks = valueNoise(i.uv * float2(3.0, 25.0) + grime);
                half dirt = saturate((grime + streaks * 0.5 - 0.4) * _GrimeAmount * 2.0 + edge * _GrimeAmount);

                float2 screenUV = i.refl.xy / i.refl.w;
                screenUV += (grime - 0.5 + crack * 0.5) * _Distortion;
                float blur = dirt * _BlurAmount;
                half3 reflection = SampleReflection(screenUV)
                    + SampleReflection(screenUV + float2(blur, 0))
                    + SampleReflection(screenUV - float2(blur, 0))
                    + SampleReflection(screenUV + float2(0, blur))
                    + SampleReflection(screenUV - float2(0, blur));
                half3 color = reflection * 0.2 * _ReflectionTint.rgb;

                half luminance = dot(color, half3(0.3, 0.59, 0.11));
                half3 film = luminance * _HazeColor.rgb + _HazeColor.rgb * (i.ambient * _AmbientAmount + _FilmGlow);
                color = lerp(color, film, saturate(_HazeAmount + dirt * 0.5));

                half desilver = smoothstep(0.6, 0.75, grime * 0.7 + edge * 0.6) * _DesilverAmount;
                color = lerp(color, _DesilverColor.rgb * (i.ambient * _AmbientAmount + _FilmGlow), desilver);

                color = lerp(color, color * 0.6 + _CrackColor.rgb * (i.ambient * _AmbientAmount + _FilmGlow), crack * 0.7);

                half hole = saturate((albedo.r - albedo.b) * 8.0);
                color = lerp(color, albedo * (i.ambient * _AmbientAmount + _FilmGlow), hole);

                half4 result = half4(color, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
}
