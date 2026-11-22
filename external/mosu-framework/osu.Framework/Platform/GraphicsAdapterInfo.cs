namespace osu.Framework.Platform
{
    /// <summary>
    /// A selectable adapter for the active graphics backend. IDs are backend-specific,
    /// not enumeration indices; an empty ID means the existing automatic behaviour.
    /// </summary>
    public sealed record GraphicsAdapterInfo(string Id, string Name);
}
