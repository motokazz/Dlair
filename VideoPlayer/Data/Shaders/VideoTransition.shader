Shader "Custom/VideoTransitionUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture A (Player A)", 2D) = "white" {}
        [NoScaleOffset] _SubTex ("Texture B (Player B)", 2D) = "white" {}
        [NoScaleOffset] _RuleTex ("Rule Texture (Grayscale)", 2D) = "white" {}
        _Transition ("Transition (0 to 1)", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _RuleAmount ("Rule Amount (0=Fade, 1=Wipe)", Range(0, 1)) = 0
        _GlobalAlpha ("Global Alpha", Range(0, 1)) = 1

        // --- UI（Image/Mask）で必須となるプロパティ ---
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        // --- UIマスク対応用のステンシル設定 ---
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
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR; // UIからのカラー情報を受け取る
                float2 uv       : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR; // フラグメントシェーダーへ渡す
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _SubTex;
            sampler2D _RuleTex;
            float _Transition;
            float _Smoothness;
            float _RuleAmount;
            float _GlobalAlpha;
            fixed4 _Color;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color; // UIの頂点カラー（透過度含む）を適用
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 colA = tex2D(_MainTex, i.uv);
                fixed4 colB = tex2D(_SubTex, i.uv);
                
                float fadeMask = 1.0 - saturate(_Transition);
                float rule = tex2D(_RuleTex, i.uv).r;
                float t = _Transition * (1.0 + _Smoothness * 2.0) - _Smoothness;
                float ruleMask = smoothstep(t - _Smoothness, t + _Smoothness, rule);
                float mask = lerp(fadeMask, ruleMask, saturate(_RuleAmount));

                // 2つのテクスチャを遷移
                fixed4 finalColor = lerp(colB, colA, mask);
                
                // UIコンポーネント（ImageやCanvasGroup）のアルファを乗算
                finalColor *= i.color;
                
                // グローバルアルファを適用
                finalColor.a *= _GlobalAlpha;

                return finalColor;
            }
            ENDCG
        }
    }
}