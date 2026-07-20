Shader "Hidden/DesktopMascot/ProductionSizedAnimatedSilhouetteDiagnostic"
{
    Properties { _Phase ("Phase", Int) = 0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            int _Phase;
            struct A { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.vertex=UnityObjectToClipPos(i.vertex); o.uv=i.uv; return o; }
            bool ellipse(float2 p,float2 c,float2 r){float2 d=(p-c)/r;return dot(d,d)<=1;}
            bool segment(float2 p,float2 a,float2 b,float r)
            {float2 ab=b-a;float t=saturate(dot(p-a,ab)/dot(ab,ab));return distance(p,a+t*ab)<=r;}
            float4 frag(V i) : SV_Target
            {
                // Graphics.Blit already supplies the validated displayed
                // orientation on this D3D12 path. Do not flip y here.
                float2 p=floor(i.uv*64.0);
                bool squash=_Phase==3;
                bool head=ellipse(p,squash?float2(29,23):float2(29,19),squash?float2(13,6):float2(13,11));
                bool body=ellipse(p,squash?float2(31,43):float2(31,39),squash?float2(12,10):float2(12,17));
                bool leftEar=p.y>=2&&p.y<=18&&p.x>=10+(p.y/4)&&p.x<=22+(p.y/8);
                bool rightEar=p.y>=9&&p.y<=18&&p.x>=36&&p.x<=47-((p.y-9)/2);
                bool leftArm=_Phase==1
                    ? segment(p,float2(21,33),float2(10,22),3)
                    : segment(p,float2(21,33),float2(12,43),3);
                bool rightArm=segment(p,float2(41,34),float2(47,45),3);
                bool leftLeg=squash
                    ? (p.x>=15&&p.x<=22&&p.y>=52&&p.y<=62)
                    : (p.x>=20&&p.x<=27&&p.y>=51&&p.y<=62);
                bool rightLeg=squash
                    ? (p.x>=40&&p.x<=47&&p.y>=52&&p.y<=62)
                    : (p.x>=35&&p.x<=42&&p.y>=52&&p.y<=60);
                bool tail=_Phase==2
                    ? segment(p,float2(43,45),float2(61,28),3.5)
                    : ((p.x>=42&&p.x<=56&&p.y>=44&&p.y<=48)
                        ||(p.x>=53&&p.x<=62&&p.y>=40&&p.y<=45));
                bool inner=head||body||leftEar||rightEar||leftArm||rightArm||leftLeg||rightLeg||tail;
                float3 colors[4]={float3(.15,.75,1),float3(.15,1,.35),float3(1,.8,.1),float3(1,.15,.75)};
                return float4(colors[clamp(_Phase,0,3)],inner?1:0);
            }
            ENDHLSL
        }
    }
}
