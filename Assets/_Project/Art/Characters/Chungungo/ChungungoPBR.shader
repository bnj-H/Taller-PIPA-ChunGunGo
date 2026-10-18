Shader "Chon-gu/Chungungo PBR"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0,2)) = 1
        _MetallicRoughnessMap ("Metallic (B) / Roughness (G)", 2D) = "white" {}
        _MetallicScale ("Metallic Scale", Range(0,1)) = 1
        _SmoothnessScale ("Smoothness Scale", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _MetallicRoughnessMap;
        fixed4 _Color;
        half _BumpScale, _MetallicScale, _SmoothnessScale;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float2 uv_MetallicRoughnessMap; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            fixed4 mr = tex2D(_MetallicRoughnessMap, IN.uv_MetallicRoughnessMap);
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
            o.Metallic = mr.b * _MetallicScale;
            o.Smoothness = (1.0h - mr.g) * _SmoothnessScale;
        }
        ENDCG
    }
    FallBack "Standard"
}
