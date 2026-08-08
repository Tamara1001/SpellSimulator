Shader "Custom/Binarize"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold ("Luminance Threshold", Range(0,1)) = 0.5
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

            sampler2D _MainTex;
            float _Threshold;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Calcular luminancia real perceptual
                float lum = col.r * 0.299 + col.g * 0.587 + col.b * 0.114;
                
                // Asumimos que el usuario dibuja con tinta oscura sobre fondo claro:
                // Si la luminancia es MENOR al threshold (es tinta), invertimos a Blanco (1.0).
                // Si la luminancia es MAYOR al threshold (es fondo), invertimos a Negro (0.0).
                float bw = lum < _Threshold ? 1.0 : 0.0;
                
                return fixed4(bw, bw, bw, 1.0);
            }
            ENDCG
        }
    }
}
