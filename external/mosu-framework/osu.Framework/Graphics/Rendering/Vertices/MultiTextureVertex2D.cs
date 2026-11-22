using System;
using System.Runtime.InteropServices;
using osuTK.Graphics.ES30;

namespace osu.Framework.Graphics.Rendering.Vertices
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MultiTextureVertex2D : IEquatable<MultiTextureVertex2D>, IVertex
    {
        public TexturedVertex2D Vertex;

        [VertexMember(1, VertexAttribPointerType.Int)]
        public int TextureIndex;

        public readonly bool Equals(MultiTextureVertex2D other) => Vertex.Equals(other.Vertex) && TextureIndex == other.TextureIndex;
    }
}
