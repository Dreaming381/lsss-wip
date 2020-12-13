Shader "Hidden/SpaceSky"
{
    HLSLINCLUDE

    #pragma editor_sync_compilation
    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "SpaceSky.hlsl"

    ENDHLSL

    SubShader
    {
        // For cubemap
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vert_SpaceSky
                #pragma fragment Frag_SpaceSky_Cubemap
            ENDHLSL
        }

        // For fullscreen Sky
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vert_SpaceSky
                #pragma fragment Frag_SpaceSky_Screen
            ENDHLSL
        }

        //For fullscreen cached sky
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vert_SpaceSky
                #pragma fragment Frag_SpaceSky_ScreenCached
            ENDHLSL
        }

        //For cubemap that caches sky
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vert_SpaceSky
                #pragma fragment Frag_SpaceSky_CubemapCached
            ENDHLSL
        }
    }
    Fallback Off
}
