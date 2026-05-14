Shader "Custom/GazeMask"
{
    Properties
    {
        _GazePos ("Gaze Position (Screen UV)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Visible Radius", Float) = 0.3
        _Softness ("Edge Softness", Float) = 0.1
        _Opacity ("Mask Opacity", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            float2 _GazePos;
            float  _Radius;
            float  _Softness;
            float  _Opacity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // クリップ座標 → UV(0-1)
                o.uv = o.pos.xy * 0.5 + 0.5;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // アスペクト比補正
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uv = i.uv;
                float2 gaze = _GazePos;
                uv.x   *= aspect;
                gaze.x *= aspect;

                float dist = distance(uv, gaze);
                float alpha = smoothstep(_Radius, _Radius + _Softness, dist);
                alpha *= _Opacity;

                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}