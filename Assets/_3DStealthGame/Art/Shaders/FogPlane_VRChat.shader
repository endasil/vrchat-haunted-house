Shader "Custom/FogPlane_VRChat"
{
    Properties
    {
        _MaxDistance ("Max Distance", Float) = 1
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "ForwardBase" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float _MaxDistance;
            float4 _Color;
            float _ScrollSpeed;
            
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            
            // Simple hash function for noise
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            
            // Value noise
            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f); // smoothstep
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            
            // Layered noise (3 octaves)
            float simpleNoise(float2 uv, float scale)
            {
                float result = 0.0;
                float freq = 1.0;
                float amp = 0.5;
                
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    result += valueNoise(uv * scale * freq) * amp;
                    freq *= 2.0;
                    amp *= 0.5;
                }
                
                return result;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screenPos = ComputeScreenPos(o.pos);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                // Animated noise layer 1 (scrolls negative direction)
                float time1 = _Time.y * (-_ScrollSpeed);
                float2 uv1 = i.uv * 4.0 + time1;
                float noise1 = simpleNoise(uv1, 80.0);
                noise1 = lerp(0.7, 1.0, noise1); // Remap 0-1 to 0.7-1.0
                
                // Animated noise layer 2 (scrolls positive direction)
                float time2 = _Time.y * _ScrollSpeed;
                float2 uv2 = i.uv + time2;
                float noise2 = simpleNoise(uv2, 50.0);
                noise2 = lerp(0.2, 1.0, noise2); // Remap 0-1 to 0.2-1.0
                
                // Combine noise layers
                float combinedNoise = noise1 * noise2;
                
                // Apply color
                float4 col = combinedNoise * _Color;
                
                // Depth-based alpha calculation
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                
                // Sample depth texture
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth);
                
                // Calculate view direction correction for proper depth
                float3 camForward = -UNITY_MATRIX_V[2].xyz;
                float viewDot = dot(i.viewDir, camForward);
                float3 correctedViewDir = i.viewDir / max(viewDot, 0.0001);
                
                // Reconstruct world position of depth sample
                float3 depthWorldPos = _WorldSpaceCameraPos + correctedViewDir * sceneDepth;
                
                // Distance from fog plane to depth point
                float dist = length(depthWorldPos - i.worldPos);
                
                // Smoothstep for smooth falloff
                float alpha = smoothstep(0.0, _MaxDistance, dist);
                
                col.a = alpha;
                
                // Apply Unity fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Transparent/Diffuse"
}
