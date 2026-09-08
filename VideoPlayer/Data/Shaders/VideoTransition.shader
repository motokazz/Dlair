Shader "Custom/VideoTransition"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Texture A (Player A)", 2D) = "black" {}
        [NoScaleOffset] _SubTex ("Texture B (Player B)", 2D) = "black" {}
        _RuleTex ("Rule Texture (Grayscale)", 2D) = "white" {}
        _Transition ("Transition (0 to 1)", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _RuleAmount ("Rule Amount (0=Fade, 1=Wipe)", Range(0, 1)) = 0
        _GlobalAlpha ("Global Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _SubTex;
            sampler2D _RuleTex;
            float _Transition;
            float _Smoothness;
            float _RuleAmount;
            float _GlobalAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
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

                fixed4 finalColor = lerp(colB, colA, mask);
                finalColor.a *= _GlobalAlpha;

                return finalColor;
            }
            ENDCG
        }
    }
}
