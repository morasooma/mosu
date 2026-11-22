// Mosu experiment (MOSU_PATH_BLIT_DISCARD=1): identical to sh_Path.fs except fully transparent
// framebuffer texels are discarded before the gradient fetch and blend, skipping the backbuffer
// read-modify-write for the empty majority of a curved path's bounding-box blit.
// The blit pass performs no depth writes, so discard cannot cost early-Z here.

#ifndef PATH_DISCARD_FS
#define PATH_DISCARD_FS

#include "sh_Utils.h"
#include "sh_Masking.h"
#include "sh_TextureWrapping.h"

layout(location = 2) in mediump vec2 v_TexCoord;

// FrameBuffer texture
layout(set = 0, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 0, binding = 1) uniform lowp sampler m_Sampler;

// Path texture
layout(set = 1, binding = 0) uniform lowp texture2D m_Texture1;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler1;

layout(std140, set = 2, binding = 0) uniform m_PathTextureParameters
{
	highp vec4 TexRect1;
};

layout(location = 0) out vec4 o_Colour;

void main(void)
{
    // Mirrors sh_Path.fs (PR 6776): the prepass writes the raw distance into the red channel
    // (R32F framebuffer by default; 8-bit red when MOSU_PATH_R32F=0), no encode/decode.
    highp float dstFromEdge = texture(sampler2D(m_Texture, m_Sampler), v_TexCoord).r;

    if (dstFromEdge <= 0.0)
        discard;

    lowp vec4 pathCol = texture(sampler2D(m_Texture1, m_Sampler1), TexRect1.xy + vec2(dstFromEdge, 0.0) * TexRect1.zw, -0.9);
    o_Colour = getRoundedColor(pathCol, v_TexCoord);
}

#endif
