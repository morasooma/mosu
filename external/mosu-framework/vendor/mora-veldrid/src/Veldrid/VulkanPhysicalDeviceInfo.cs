namespace Veldrid
{
    /// <summary>
    /// A physical device enumerated by the Vulkan instance used for rendering.
    /// The identifier is independent of the enumeration order except for identical devices.
    /// </summary>
    public readonly struct VulkanPhysicalDeviceInfo
    {
        public string Id { get; }
        public string Name { get; }

        public VulkanPhysicalDeviceInfo(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
