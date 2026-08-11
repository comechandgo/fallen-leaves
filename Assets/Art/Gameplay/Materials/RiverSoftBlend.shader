Shader "FallenLeaves/RiverSoftBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EndFade ("End Fade", Range(0.001, 0.25)) = 0.035
        _SideFade ("Side Fade", Range(0.001, 0.25)) = 0.06
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment RiverFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _EndFade;
            float _SideFade;

            fixed4 RiverFrag(v2f IN) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(IN.texcoord) * IN.color;
                float leftFade = smoothstep(0.0, _EndFade, IN.texcoord.x);
                float rightFade = smoothstep(0.0, _EndFade, 1.0 - IN.texcoord.x);
                float bottomFade = smoothstep(0.0, _SideFade, IN.texcoord.y);
                float topFade = smoothstep(0.0, _SideFade, 1.0 - IN.texcoord.y);
                color.a *= leftFade * rightFade * bottomFade * topFade;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
