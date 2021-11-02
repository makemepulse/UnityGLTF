Shader "Hidden/EXT_LightImageBasedExport"
{
    Properties
    {
        [HDR]_MainTex ("Texture", Cube) = "white" {}
    }
    SubShader
    {

        Cull Off ZWrite Off ZTest Always

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

            static const float MaxRange = 255.0; 

            half4 EncodeRGBD(float3 rgb)
            {
                half maxRGB  = max(rgb.x, max(rgb.g, rgb.b));
                half D       = max(MaxRange / maxRGB, 1);
                D            = saturate(floor(D) / 255.0);
                return half4(rgb.rgb * (D * (255.0 / MaxRange)), D);
            }


            float3 DecodeRGBD(half4 rgbd)
            {
                return rgbd.rgb * ((MaxRange / 255.0) / rgbd.a);
            }


            float4 multQuat(float4 q1, float4 q2) {
                return float4(
                q1.w * q2.x + q1.x * q2.w + q1.z * q2.y - q1.y * q2.z,
                q1.w * q2.y + q1.y * q2.w + q1.x * q2.z - q1.z * q2.x,
                q1.w * q2.z + q1.z * q2.w + q1.y * q2.x - q1.x * q2.y,
                q1.w * q2.w - q1.x * q2.x - q1.y * q2.y - q1.z * q2.z
                );
            }

            float3 rotateVector( float4 quat, float3 vec ) {
                // https://twistedpairdevelopment.wordpress.com/2013/02/11/rotating-a-vector-by-a-quaternion-in-glsl/
                float4 qv = multQuat( quat, float4(vec, 0.0) );
                return multQuat( qv, float4(-quat.x, -quat.y, -quat.z, quat.w) ).xyz;
            }


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            samplerCUBE _MainTex;
            float4 _MainTex_HDR;
            half4 _Tint;
            half _Exposure;
            float4 _Rotation;
            float _MipLevel;

            float4 frag (v2f i) : SV_Target
            {

                float2 uv = i.uv.yx;
                uv.y = 1.0 - uv.y;
                float3 s = normalize(float3(1.0, uv * 2.0 - float2(1.0, 1.0)));
                s = rotateVector(_Rotation, s);
                s.x = -s.x;

                half4 skyData = texCUBElod(_MainTex, float4(s, _MipLevel));
                half3 skyColor = DecodeHDR (skyData, _MainTex_HDR);
                
                fixed4 c = 0;
                c.rgb = GammaToLinearSpace(skyColor);
                c.rgb = skyColor;
                return EncodeRGBD(c);

            }

            ENDCG
        }
    }
}
