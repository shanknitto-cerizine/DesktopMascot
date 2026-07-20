Shader "Hidden/DesktopMascot/StaticComplexSilhouetteDiagnostic"
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

            bool ellipse(float2 p, float2 center, float2 radius)
            {
                const float2 d = (p - center) / radius;
                return dot(d, d) <= 1.0;
            }

            bool segment(float2 p, float2 a, float2 b, float radius)
            {
                const float2 ab = b - a;
                const float t = saturate(dot(p - a, ab) / dot(ab, ab));
                return distance(p, a + t * ab) <= radius;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Graphics.Blit supplies vertically inverted UVs for this
                // D3D12 RenderTexture path. Therefore input UV y=0 is the
                // displayed top and already matches the shape's top-left
                // coordinate convention.
                const float2 p = floor(input.uv * 64.0);

                const bool head = ellipse(p, float2(29, 19), float2(13, 11));
                const bool body = ellipse(p, float2(31, 39), float2(12, 17));
                const bool longLeftEar =
                    p.y >= 2 && p.y <= 18
                    && p.x >= 10 + (p.y / 4)
                    && p.x <= 22 + (p.y / 8);
                const bool shortRightEar =
                    p.y >= 9 && p.y <= 18
                    && p.x >= 36 && p.x <= 47 - ((p.y - 9) / 2);
                const bool leftArm =
                    segment(p, float2(21, 33), float2(12, 43), 3.0);
                const bool rightArm =
                    segment(p, float2(41, 34), float2(47, 45), 3.0);
                const bool leftLeg =
                    p.x >= 20 && p.x <= 27 && p.y >= 51 && p.y <= 62;
                const bool rightLeg =
                    p.x >= 35 && p.x <= 42 && p.y >= 52 && p.y <= 60;
                const bool tail =
                    (p.x >= 42 && p.x <= 56 && p.y >= 44 && p.y <= 48)
                    || (p.x >= 53 && p.x <= 62 && p.y >= 40 && p.y <= 45);
                const bool inner = head || body || longLeftEar
                    || shortRightEar || leftArm || rightArm
                    || leftLeg || rightLeg || tail;

                const bool softHead =
                    ellipse(p, float2(29, 19), float2(15, 13));
                const bool softBody =
                    ellipse(p, float2(31, 39), float2(14, 19));
                const bool softTail =
                    segment(p, float2(43, 47), float2(61, 42), 4.5);
                const bool soft = !inner
                    && (softHead || softBody || softTail);

                const float alpha = inner ? 1.0 : (soft ? 64.0 / 255.0 : 0.0);
                const float3 straightColor = inner
                    ? float3(0.15, 0.75, 1.0)
                    : float3(1.0, 0.65, 0.15);
                return float4(straightColor * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
