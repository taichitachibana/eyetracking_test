Shader "Hidden/Custom/GazeMask"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _GazePos ("Gaze Position (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0.25
        _Softness ("Softness", Float) = 0.08
        _Intensity ("Intensity", Float) = 0.75
        _Aspect ("Aspect", Vector) = (1,1,0,0) // X,Y のスケーリング(楕円用)
        _ShapeType ("Shape Type", Range(0,3)) = 0 // 0=circle,1=ellipse,2=rect,3=mask texture
        _MaskTex ("Mask Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off Cull Off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _GazePos;
            float _Radius;
            float _Softness;
            float _Intensity;
            float4 _Aspect;
            float _ShapeType;
            sampler2D _MaskTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 center = _GazePos.xy;

                float mask = 0.0;

                // shape selection via _ShapeType (float compare)
                if (_ShapeType < 0.5) // circle
                {
                    float2 diff = (uv - center) * float2(_Aspect.x, _Aspect.y);
                    float dist = length(diff);
                    mask = smoothstep(_Radius, _Radius + _Softness, dist);
                }
                else if (_ShapeType < 1.5) // ellipse (aspect used)
                {
                    float2 diff = uv - center;
                    diff.x *= _Aspect.x;
                    diff.y *= _Aspect.y;
                    float dist = length(diff);
                    mask = smoothstep(_Radius, _Radius + _Softness, dist);
                }
                else if (_ShapeType < 2.5) // rectangle (rounded-ish with smoothstep)
                {
                    float2 d = abs(uv - center) - float2(_Radius, _Radius);
                    float dist = max(d.x, d.y);
                    // dist may be negative inside rect -> smoothstep with softness
                    mask = smoothstep(0.0, _Softness, dist);
                }
                else // mask texture: sample mask centered at gaze (mask texture coords: center -> 0.5,0.5)
                {
                    float2 maskUV = (uv - center) + 0.5;
                    // optional: allow scaling by radius (not implemented here)
                    float4 mt = tex2D(_MaskTex, maskUV);
                    mask = mt.r; // assume red channel encodes mask (0..1)
                }

                fixed4 col = tex2D(_MainTex, uv);
                // apply darkness: mask==1 => apply full intensity; mask==0 => no change
                col.rgb = col.rgb * (1.0 - _Intensity * mask);

                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}

