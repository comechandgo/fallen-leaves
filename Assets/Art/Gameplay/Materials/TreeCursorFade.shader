Shader "FallenLeaves/TreeCursorFade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _InnerRadius ("Inner Radius (Pixels)", Range(0, 256)) = 45
        _OuterRadius ("Outer Radius (Pixels)", Range(1, 384)) = 75
        _MinimumOpacity ("Minimum Opacity", Range(0, 1)) = 0.25
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
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
            #pragma vertex TreeVert
            #pragma fragment TreeFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnityCG.cginc"
            #include "UnitySprites.cginc"

            struct TreeV2F
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _InnerRadius;
            float _OuterRadius;
            float _MinimumOpacity;
            float4 _TreeCursorScreenPosition;
            float _TreeCursorFadeEnabled;

            TreeV2F TreeVert(appdata_t IN)
            {
                TreeV2F OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                IN.vertex = UnityFlipSprite(IN.vertex, _Flip);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                OUT.screenPosition = ComputeScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 TreeFrag(TreeV2F IN) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(IN.texcoord) * IN.color;

                float2 screenUv = IN.screenPosition.xy / max(IN.screenPosition.w, 0.0001);
                float2 pixelPosition = screenUv * _ScreenParams.xy;
                float cursorDistance = distance(pixelPosition, _TreeCursorScreenPosition.xy);
                float radiusRange = max(_OuterRadius - _InnerRadius, 0.0001);
                float outsideBlend = smoothstep(0.0, 1.0, saturate((cursorDistance - _InnerRadius) / radiusRange));
                float opacity = lerp(_MinimumOpacity, 1.0, outsideBlend);
                color.a *= lerp(1.0, opacity, saturate(_TreeCursorFadeEnabled));

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
