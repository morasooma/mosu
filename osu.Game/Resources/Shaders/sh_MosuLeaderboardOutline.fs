#ifndef MOSU_LEADERBOARD_OUTLINE_FS
#define MOSU_LEADERBOARD_OUTLINE_FS

#define HIGH_PRECISION_VERTEX
#include "sh_Utils.h"
#include "sh_Masking.h"

layout(location = 2) in highp vec2 v_TexCoord;

layout(std140, set = 0, binding = 0) uniform m_OutlineParameters
{
    highp vec2 g_Size;
    highp float g_Radius;
    highp float g_Thickness;
};

layout(location = 0) out vec4 o_Colour;

void main(void)
{
    // The Box uses the renderer's atlas white pixel, so normalise its UV range.
    highp vec2 uv = (v_TexCoord - v_TexRect.xy) / (v_TexRect.zw - v_TexRect.xy);
    highp float radius = clamp(g_Radius, 0.0, min(g_Size.x, g_Size.y) * 0.5);
    highp vec2 q = abs((uv - 0.5) * g_Size) - g_Size * 0.5 + radius;
    highp float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
    // Both sides of the stroke receive pixel coverage, including under shear,
    // rotation and fractional scale. No one-pixel mask-border colour branches.
    highp float pixel = max(fwidth(distance), 0.0001);
    highp float thickness = max(g_Thickness, pixel);
    highp float outer = smoothstep(0.0, pixel, -distance);
    highp float inner = smoothstep(thickness, thickness + pixel, -distance);
    o_Colour = getRoundedColor(vec4(1.0, 1.0, 1.0, outer - inner), v_TexCoord);
}

#endif
