Shader "Hidden/MMP_LightImageBasedExport"
{
    Properties
    {
        [HDR]_MainTex("_MainTex", Cube) = "" {}
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

            float3 vecFromSplitOctant(float _u, float _v)
            {

                float3 vec = 0;
                float px = _u*4.0f - 1.0f;
                float py = _v*2.0f - 1.0f;
                
                float sign;
                if( px > 1.0f ){
                    px -= 2.0f;
                    sign = -1.0f;
                    }else{
                    sign = 1.0f;
                }

                vec.x = ( sign * px + py ) / 2.0f;
                vec.y = ( sign * px - py ) / 2.0f;
                
                vec.z = 1.0f - abs(vec.x) - abs(vec.y);
                
                vec.z *= sign;

                const float invLen = 1.0f/length(vec);
                vec.x *= invLen;
                vec.y *= invLen;
                vec.z *= invLen;
                return vec;
            }

            half4 EncodeRGBE(float3 rgb){
                float maxRGB  = max(rgb.x, max(rgb.g, rgb.b));
                float exp = ceil(log2(maxRGB));
                float toRgb8 = 255.0 * 1.0 / exp2(exp);
                float4 rgbe = 0;
                rgbe.r = rgb.r * toRgb8;
                rgbe.g = rgb.g * toRgb8;
                rgbe.b = rgb.b * toRgb8;
                rgbe.a = exp+128.0;
                return rgbe / 255.0;
            }


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv.y = 1.0 - o.uv.y;
                return o;
            }

            samplerCUBE _MainTex;
            float4 _MainTex_HDR;

            fixed4 frag (v2f i) : SV_Target
            {

                float2 uv = float2(i.uv.x, i.uv.y * 8.0);
                float lod = floor(uv.y);
                float3 decode = vecFromSplitOctant(uv.x, fmod(uv.y, 1.0));
                decode.x = -decode.x;
                float4 skyData = texCUBElod(_MainTex, float4(decode, lod));
                half3 skyColor = DecodeHDR(skyData, _MainTex_HDR);
                return EncodeRGBE(skyColor.rgb);

            }
            ENDCG
        }
    }
}
