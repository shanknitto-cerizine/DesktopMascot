Shader "Hidden/DesktopMascot/AnimatedAlphaMaskDiagnostic"
{
    Properties
    {
        _Phase ("Phase", Int) = 0
    }

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

            int _Phase;

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
                const bool left = input.uv.x < 0.5;
                // Graphics.Blit supplies vertically inverted UVs for this
                // D3D12 RenderTexture path. Use the displayed orientation so
                // shader quadrants and top-left-origin HRGN coordinates agree.
                const bool top = input.uv.y < 0.5;
                bool opaque = false;
                if (_Phase == 0) opaque = left && top;
                if (_Phase == 1) opaque = !left && top;
                if (_Phase == 2) opaque = !left && !top;
                if (_Phase == 3) opaque = left && !top;
                const float alpha = opaque ? 1.0 : 0.0;
                const float3 color = float3(0.2, 0.8, 1.0) * alpha;
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
