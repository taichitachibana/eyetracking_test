Shader "Custom/GazeMask"
{
    Properties
    {
        _GazePos  ("Gaze Position", Vector) = (0.5, 0.5, 0, 0)
        _Radius   ("Visible Radius", Float) = 0.25
        _Softness ("Edge Softness",  Float) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        ZWrite Off
        ZTest Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
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
                float2 uv  : TEXCOORD0;
                float4 clipPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 _GazePos;
            float  _Radius;
            float  _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos     = UnityObjectToClipPos(v.vertex);
                o.clipPos = o.pos;
                o.uv      = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // クリップ座標からスクリーンUVを計算（視線の歪みを低減）
                float2 screenUV = i.clipPos.xy / i.clipPos.w * 0.5 + 0.5;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uvA   = float2(screenUV.x * aspect, screenUV.y);
                float2 gazeA = float2(_GazePos.x  * aspect, _GazePos.y);

                float dist  = distance(uvA, gazeA);
                float alpha = smoothstep(_Radius, _Radius + _Softness, dist);

                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}