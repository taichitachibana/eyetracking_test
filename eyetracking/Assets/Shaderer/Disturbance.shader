Shader "Custom/Disturbance"
{
    Properties
    {
        _DisturbanceColor ("Disturbance Color",    Color) = (1, 1, 1, 1)
        _Brightness       ("Brightness (Flicker)", Float) = 1.0
        _EdgeSoftness     ("Edge Softness",        Float) = 0.1
        _DisturbTime      ("Time",                 Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" }
        ZWrite Off
        ZTest Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _DisturbanceColor;
            float  _Brightness;
            float  _EdgeSoftness;
            float  _DisturbTime;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 centered = i.uv - 0.5;
                float  dist     = length(centered) * 2.0;

                float inner = 1.0 - _EdgeSoftness;
                float alpha = (1.0 - smoothstep(inner, 1.0, dist)) * _Brightness;

                return fixed4(_DisturbanceColor.rgb, alpha * _DisturbanceColor.a);
            }
            ENDCG
        }
    }
}
