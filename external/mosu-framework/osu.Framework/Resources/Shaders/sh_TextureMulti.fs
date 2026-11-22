#ifndef TEXTURE_MULTI_FS
#define TEXTURE_MULTI_FS

#include "sh_Utils.h"
#include "sh_Masking.h"
#include "sh_TextureWrapping.h"

layout(location = 2) in highp vec2 v_TexCoord;
layout(location = 5) flat in int v_TextureIndex;
layout(set = 0, binding = 0) uniform lowp texture2D m_Texture0;
layout(set = 0, binding = 1) uniform lowp sampler m_Sampler0;
layout(set = 1, binding = 0) uniform lowp texture2D m_Texture1;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler1;
layout(set = 2, binding = 0) uniform lowp texture2D m_Texture2;
layout(set = 2, binding = 1) uniform lowp sampler m_Sampler2;
layout(set = 3, binding = 0) uniform lowp texture2D m_Texture3;
layout(set = 3, binding = 1) uniform lowp sampler m_Sampler3;
layout(set = 4, binding = 0) uniform lowp texture2D m_Texture4;
layout(set = 4, binding = 1) uniform lowp sampler m_Sampler4;
layout(set = 5, binding = 0) uniform lowp texture2D m_Texture5;
layout(set = 5, binding = 1) uniform lowp sampler m_Sampler5;
layout(set = 6, binding = 0) uniform lowp texture2D m_Texture6;
layout(set = 6, binding = 1) uniform lowp sampler m_Sampler6;
layout(set = 7, binding = 0) uniform lowp texture2D m_Texture7;
layout(set = 7, binding = 1) uniform lowp sampler m_Sampler7;
layout(location = 0) out vec4 o_Colour;

void main(void)
{
    // Query the original implicit LOD before the page-selection branch. All
    // eligible font pages have identical native dimensions and filtering.
    vec2 coord = wrap(v_TexCoord, v_TexRect);
    float lod = textureQueryLod(sampler2D(m_Texture0, m_Sampler0), coord).y - 0.9;
    vec4 texel;
    switch (v_TextureIndex)
    {
        case 0: texel = textureLod(sampler2D(m_Texture0, m_Sampler0), coord, lod); break;
        case 1: texel = textureLod(sampler2D(m_Texture1, m_Sampler1), coord, lod); break;
        case 2: texel = textureLod(sampler2D(m_Texture2, m_Sampler2), coord, lod); break;
        case 3: texel = textureLod(sampler2D(m_Texture3, m_Sampler3), coord, lod); break;
        case 4: texel = textureLod(sampler2D(m_Texture4, m_Sampler4), coord, lod); break;
        case 5: texel = textureLod(sampler2D(m_Texture5, m_Sampler5), coord, lod); break;
        case 6: texel = textureLod(sampler2D(m_Texture6, m_Sampler6), coord, lod); break;
        default: texel = textureLod(sampler2D(m_Texture7, m_Sampler7), coord, lod); break;
    }
    o_Colour = getRoundedColor(texel, coord);
}
#endif
