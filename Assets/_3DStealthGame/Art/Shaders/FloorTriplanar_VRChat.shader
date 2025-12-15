Shader "Custom/FloorTriplanar_VRChat"
{
    Properties
    {
        [NoScaleOffset] _MainTexture ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Tiling ("Tiling", Float) = 1
        _Blend ("Blend", Range(0.1, 20)) = 8
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        [NoScaleOffset] _OcclusionMap ("AO", 2D) = "white" {}
        _OcclusionStrength ("AO Strength", Range(0, 1)) = 1
        [NoScaleOffset] _MetallicGlossMap ("Metallic Smooth", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 1
        _Glossiness ("Smoothness", Range(0, 1)) = 1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
        }
        LOD 200
        
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        
        sampler2D _MainTexture;
        sampler2D _BumpMap;
        sampler2D _OcclusionMap;
        sampler2D _MetallicGlossMap;
        
        half4 _Color;
        half _Tiling;
        half _Blend;
        half _BumpScale;
        half _OcclusionStrength;
        half _Metallic;
        half _Glossiness;
        
        struct Input
        {
            float3 worldPos;
            float3 vertexNormal;
        };
        
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Pass object-space normal transformed to world space
            o.vertexNormal = UnityObjectToWorldNormal(v.normal);
        }
        
        // Calculate triplanar blend weights
        float3 GetTriplanarBlend(float3 normal, float blend)
        {
            float3 b = pow(abs(normal), blend);
            return b / (b.x + b.y + b.z + 0.0001);
        }
        
        // Triplanar texture sampling
        float4 TriplanarTex(sampler2D tex, float3 pos, float3 blend)
        {
            float4 cx = tex2D(tex, pos.zy);
            float4 cy = tex2D(tex, pos.xz);
            float4 cz = tex2D(tex, pos.xy);
            return cx * blend.x + cy * blend.y + cz * blend.z;
        }
        
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Scale world position by tiling
            float3 worldPos = IN.worldPos * _Tiling;
            float3 normal = normalize(IN.vertexNormal);
            
            // Get blend weights
            float3 blend = GetTriplanarBlend(normal, _Blend);
            
            // Sample albedo
            half4 albedo = TriplanarTex(_MainTexture, worldPos, blend);
            
            // Sample metallic/smoothness
            half4 metalGloss = TriplanarTex(_MetallicGlossMap, worldPos, blend);
            
            // Sample occlusion
            half4 occlusion = TriplanarTex(_OcclusionMap, worldPos, blend);
            
            // Sample normal maps from each projection
            half3 nx = UnpackScaleNormal(tex2D(_BumpMap, worldPos.zy), _BumpScale);
            half3 ny = UnpackScaleNormal(tex2D(_BumpMap, worldPos.xz), _BumpScale);
            half3 nz = UnpackScaleNormal(tex2D(_BumpMap, worldPos.xy), _BumpScale);
            
            // Blend the tangent-space normals
            half3 tangentNormal = normalize(nx * blend.x + ny * blend.y + nz * blend.z);
            
            // Output
            o.Albedo = albedo.rgb * _Color.rgb;
            o.Normal = tangentNormal;
            o.Metallic = metalGloss.r * _Metallic;
            o.Smoothness = metalGloss.a * _Glossiness;
            o.Occlusion = lerp(1, occlusion.r, _OcclusionStrength);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    
    FallBack "Standard"
}
