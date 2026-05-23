Shader "Custom/GazeMask"
{
    Properties
    {
        _GazePos ("Gaze Position", Vector) = (0.5,0.5,0,0)
        _GazePosR ("Gaze Position Right Eye", Vector) = (0.5,0.5,0,0)
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 _GazePos;
            float2 _GazePosR;

            float _Radius;
            float _Softness;
            float _Opacity;

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);

                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv =
                    UnityStereoTransformScreenSpaceTex(i.uv);

                float2 gaze =
                    unity_StereoEyeIndex == 0
                    ? _GazePos
                    : _GazePosR;

                float aspect =
                    _ScreenParams.x / _ScreenParams.y;

                float2 uvAspect =
                    float2(uv.x * aspect, uv.y);

                float2 gazeAspect =
                    float2(gaze.x * aspect, gaze.y);

                float dist =
                    distance(uvAspect, gazeAspect);

                float alpha =
                    smoothstep(
                        _Radius,
                        _Radius + _Softness,
                        dist
                    );

                alpha *= _Opacity;

                return fixed4(0,0,0,alpha);
            }

            ENDCG
        }
    }
}