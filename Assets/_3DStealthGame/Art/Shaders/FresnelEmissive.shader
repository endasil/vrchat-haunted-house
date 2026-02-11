Shader "Legacy/GhostShader"
{
    Properties
    {
        _MainTex("Base Texture", 2D) = "white" {}
        _OpacityMap("Opacity Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _EmissionMap("Emission Map", 2D) = "white" {}
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _FresnelPower("Fresnel Power", Float) = 2.0
        _FresnelColor("Fresnel Color", Color) = (0.4245283, 0.4245283, 0.4245283, 0)
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        
        LOD 200
        
        // Forward Base Pass
        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            sampler2D _MainTex;
            sampler2D _OpacityMap;
            sampler2D _NormalMap;
            sampler2D _EmissionMap;
            sampler2D _OcclusionMap;
            
            float4 _MainTex_ST;
            float _FresnelPower;
            float4 _FresnelColor;
            float _Smoothness;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldTangent : TEXCOORD3;
                float3 worldBinormal : TEXCOORD4;
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBinormal = cross(o.worldNormal, o.worldTangent) * v.tangent.w;
                
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Sample textures
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                fixed4 opacityMap = tex2D(_OpacityMap, i.uv);
                fixed3 normalTex = UnpackNormal(tex2D(_NormalMap, i.uv));
                fixed4 emission = tex2D(_EmissionMap, i.uv);
                fixed occlusion = tex2D(_OcclusionMap, i.uv).r;
                
                // Transform normal from tangent to world space
                float3x3 tangentToWorld = float3x3(
                    i.worldTangent,
                    i.worldBinormal,
                    i.worldNormal
                );
                float3 worldNormal = normalize(mul(normalTex, tangentToWorld));
                
                // Fresnel effect
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = pow(1.0 - saturate(dot(worldNormal, viewDir)), _FresnelPower);
                
                // Add Fresnel color
                float4 fresnelAdd = float4(fresnel, fresnel, fresnel, fresnel) + _FresnelColor;
                
                // One minus opacity
                float4 oneMinusOpacity = 1.0 - opacityMap;
                
                // Multiply Fresnel with inverted opacity
                float4 fresnelMask = fresnelAdd * oneMinusOpacity;
                
                // Final base color
                fixed4 finalBase = baseColor + fresnelMask;
                
                // Final emission
                fixed4 finalEmission = emission + fresnelMask;
                
                // Alpha calculation
                float alpha = (fresnelMask + opacityMap).x;
                
                // Lighting calculations (softer, more like URP)
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0, dot(worldNormal, lightDir));
                
                // Shadow attenuation
                fixed shadow = SHADOW_ATTENUATION(i);
                
                // Softer lighting mix for transparent ghost effect
                fixed3 lighting = lerp(0.4, 1.0, NdotL * shadow); // Much softer lighting
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * 0.5 + 0.5; // Brighter ambient
                
                // Apply lighting to base color (keep it bright)
                fixed3 litColor = finalBase.rgb * lighting * _LightColor0.rgb;
                litColor = litColor + ambient * finalBase.rgb * 0.5;
                
                // Add emission (this makes it glow/bright)
                fixed3 finalColor = litColor + finalEmission.rgb;
                
                // Apply occlusion softly
                finalColor *= lerp(1.0, occlusion, 0.5);
                
                fixed4 col = fixed4(finalColor, alpha);
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
        
        // Forward Add Pass (for additional lights)
        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode" = "ForwardAdd" }
            
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            sampler2D _MainTex;
            sampler2D _OpacityMap;
            sampler2D _NormalMap;
            sampler2D _OcclusionMap;
            
            float4 _MainTex_ST;
            float _FresnelPower;
            float4 _FresnelColor;
            float _Smoothness;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldTangent : TEXCOORD3;
                float3 worldBinormal : TEXCOORD4;
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBinormal = cross(o.worldNormal, o.worldTangent) * v.tangent.w;
                
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Sample textures
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                fixed4 opacityMap = tex2D(_OpacityMap, i.uv);
                fixed3 normalTex = UnpackNormal(tex2D(_NormalMap, i.uv));
                fixed occlusion = tex2D(_OcclusionMap, i.uv).r;
                
                // Transform normal
                float3x3 tangentToWorld = float3x3(
                    i.worldTangent,
                    i.worldBinormal,
                    i.worldNormal
                );
                float3 worldNormal = normalize(mul(normalTex, tangentToWorld));
                
                // Fresnel
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = pow(1.0 - saturate(dot(worldNormal, viewDir)), _FresnelPower);
                float4 fresnelAdd = float4(fresnel, fresnel, fresnel, fresnel) + _FresnelColor;
                float4 fresnelMask = fresnelAdd * (1.0 - opacityMap);
                fixed4 finalBase = baseColor + fresnelMask;
                
                // Light direction (handles point/spot lights)
                #ifndef USING_DIRECTIONAL_LIGHT
                    float3 lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                #else
                    float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                #endif
                
                float NdotL = max(0, dot(worldNormal, lightDir));
                
                // Attenuation and shadow
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                
                // Softer lighting for additional lights
                fixed3 lighting = lerp(0.0, 1.0, NdotL * atten);
                fixed3 diffuse = finalBase.rgb * _LightColor0.rgb * lighting;
                
                fixed3 finalColor = diffuse * lerp(1.0, occlusion, 0.5);
                float alpha = (fresnelMask + opacityMap).x;
                
                fixed4 col = fixed4(finalColor, alpha);
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
        
        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            
            sampler2D _OpacityMap;
            float4 _MainTex_ST;
            float _FresnelPower;
            float4 _FresnelColor;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Alpha calculation for transparency shadows
                fixed4 opacityMap = tex2D(_OpacityMap, i.uv);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = pow(1.0 - saturate(dot(i.worldNormal, viewDir)), _FresnelPower);
                float4 fresnelAdd = float4(fresnel, fresnel, fresnel, fresnel) + _FresnelColor;
                float4 fresnelMask = fresnelAdd * (1.0 - opacityMap);
                float alpha = (fresnelMask + opacityMap).x;
                
                clip(alpha - 0.01);
                
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    
    FallBack "Legacy Shaders/Transparent/VertexLit"
}
