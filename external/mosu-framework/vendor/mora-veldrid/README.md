# Mora Veldrid

Vendored Veldrid core, derived from https://github.com/ppy/veldrid at
`a405fe8484fa5384dfbd1faff58c74f717f46651` (NuGet `4.9.69-ga405fe8484`).
The original MIT license is retained in LICENSE, alongside the copyright
headers in upstream-derived files.

The framework references this project directly. Regular Mosu builds include the
patched backend without manual DLL copies or modifications of the NuGet cache.
Metal/OpenGL bindings remain pinned to the original NuGet release. SPIRV references
the same package identity and resolves to this project; retain `ppy.Veldrid` assembly
name and assembly version `4.9.0.0` for compatibility.

Changes to the Vulkan backend:
- correct queue-family indices during logical-device creation;
- no obsolete device layers;
- debug markers require their instance extension dependency;
- optimal images use Undefined initial and tracked layouts;
- no clearing of swapchain images before acquisition;
- framebuffers retain their color/depth images until framebuffer destruction, including
  GPU fence retirement, so resizing cannot free attachments still used by submitted commands.

Fork renderer configuration identifier: `Mora_Vulkan = 1000`, UI label
`Вулкан Мора (Experimental)`. Original upstream enum values are preserved for
existing configurations; Windows/Linux offer Mora_Vulkan in their platform lists.
Both old and new Vulkan choices currently use the patched Veldrid core. This is
not a separate loaded upstream Vulkan implementation.

Upstream updates must be intentionally merged into this source directory and
validated; changing a NuGet version alone cannot overwrite these patches.
Review upstream differences against the recorded base, not against a moving branch.

Completed fences are retired as an ordered prefix; resource retirement order
and synchronization are unchanged. The optional in-memory Vulkan pipeline
cache (`MORA_VULKAN_PIPELINE_CACHE=1`) is off by default.
