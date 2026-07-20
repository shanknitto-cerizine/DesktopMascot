Shader "Hidden/DesktopMascot/AlphaMaskDiagnostic"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                bool left = input.uv.x < 0.5;
                bool top = input.uv.y >= 0.5;
                float alphaByte = top
                    ? (left ? 0.0 : 64.0)
                    : (left ? 128.0 : 255.0);
                if (abs(input.uv.x - 0.5) < 0.125
                    && abs(input.uv.y - 0.5) < 0.125)
                {
                    alphaByte = 192.0;
                }
                const float alpha = alphaByte / 255.0;
                const float3 baseColor = float3(
                    left ? 1.0 : 0.15,
                    top ? 0.25 : 0.85,
                    left ? 0.25 : 1.0);
                return float4(baseColor * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
