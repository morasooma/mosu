namespace Veldrid
{
    /// <summary>
    /// The graphics device is no longer usable. Rendering must stop; retrying a frame cannot recover it.
    /// </summary>
    public sealed class GraphicsDeviceLostException : VeldridException
    {
        public GraphicsDeviceLostException(string message)
            : base(message)
        {
        }
    }
}
