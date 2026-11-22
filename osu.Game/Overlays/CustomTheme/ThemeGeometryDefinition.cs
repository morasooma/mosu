using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Represents sparse geometry overrides (such as corner radii) for a custom theme.
    /// Unspecified (null) roles inherit base defaults matching today's UI.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class ThemeGeometryDefinition : IEquatable<ThemeGeometryDefinition>
    {
        [JsonProperty("buttonRadius", NullValueHandling = NullValueHandling.Ignore)]
        public float? ButtonRadius { get; set; }

        [JsonProperty("inputRadius", NullValueHandling = NullValueHandling.Ignore)]
        public float? InputRadius { get; set; }

        [JsonProperty("cardRadius", NullValueHandling = NullValueHandling.Ignore)]
        public float? CardRadius { get; set; }

        [JsonProperty("panelRadius", NullValueHandling = NullValueHandling.Ignore)]
        public float? PanelRadius { get; set; }

        [JsonProperty("popupRadius", NullValueHandling = NullValueHandling.Ignore)]
        public float? PopupRadius { get; set; }

        [JsonProperty("topBarVariant", NullValueHandling = NullValueHandling.Ignore)]
        public TopBarVariant? TopBarVariant { get; set; }

        [JsonProperty("topBarRadius", NullValueHandling = NullValueHandling.Ignore)]
        public float? TopBarRadius { get; set; }

        [JsonProperty("enableGradients", NullValueHandling = NullValueHandling.Ignore)]
        public bool? EnableGradients { get; set; }

        /// <summary>
        /// Preserves unknown future geometry fields across round trips.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Extensions { get; set; }

        /// <summary>
        /// Clamps a top bar corner radius value into the safe operational range [0, 24].
        /// </summary>
        public static float ClampTopBarRadius(float radius)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius))
                return 10f;

            return Math.Clamp(radius, 0f, 24f);
        }

        /// <summary>
        /// Clamps a radius value into the safe operational range for a given role,
        /// avoiding NaN/infinity/negative/huge radii that could break masking.
        /// </summary>
        public static float ClampRadius(GeometryRole role, float radius)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius))
                return OverlayColourProvider.GetDefaultRadius(role);

            float max = role switch
            {
                GeometryRole.Button => 20f,
                GeometryRole.Input => 20f,
                GeometryRole.Card => 30f,
                GeometryRole.Panel => 30f,
                GeometryRole.Popup => 30f,
                _ => 50f
            };

            return Math.Clamp(radius, 0f, max);
        }

        /// <summary>
        /// Tries to resolve a custom corner radius override for the given role.
        /// </summary>
        /// <param name="role">The semantic geometry role.</param>
        /// <param name="radius">The sanitized and clamped corner radius if overridden.</param>
        /// <returns>True if explicitly overridden; false to inherit from base theme.</returns>
        public bool TryGetRadius(GeometryRole role, out float radius)
        {
            float? val = role switch
            {
                GeometryRole.Button => ButtonRadius,
                GeometryRole.Input => InputRadius,
                GeometryRole.Card => CardRadius,
                GeometryRole.Panel => PanelRadius,
                GeometryRole.Popup => PopupRadius,
                _ => null
            };

            if (val.HasValue)
            {
                radius = ClampRadius(role, val.Value);
                return true;
            }

            radius = default;
            return false;
        }

        /// <summary>
        /// Sets or replaces an override for a semantic geometry role.
        /// </summary>
        public void SetRadius(GeometryRole role, float radius)
        {
            float clamped = ClampRadius(role, radius);

            switch (role)
            {
                case GeometryRole.Button:
                    ButtonRadius = clamped;
                    break;

                case GeometryRole.Input:
                    InputRadius = clamped;
                    break;

                case GeometryRole.Card:
                    CardRadius = clamped;
                    break;

                case GeometryRole.Panel:
                    PanelRadius = clamped;
                    break;

                case GeometryRole.Popup:
                    PopupRadius = clamped;
                    break;
            }
        }

        /// <summary>
        /// Clears an override for a semantic geometry role, reverting it to inherited default.
        /// </summary>
        public bool ClearRadius(GeometryRole role)
        {
            switch (role)
            {
                case GeometryRole.Button:
                    bool hadB = ButtonRadius.HasValue;
                    ButtonRadius = null;
                    return hadB;

                case GeometryRole.Input:
                    bool hadI = InputRadius.HasValue;
                    InputRadius = null;
                    return hadI;

                case GeometryRole.Card:
                    bool hadC = CardRadius.HasValue;
                    CardRadius = null;
                    return hadC;

                case GeometryRole.Panel:
                    bool hadP = PanelRadius.HasValue;
                    PanelRadius = null;
                    return hadP;

                case GeometryRole.Popup:
                    bool hadPop = PopupRadius.HasValue;
                    PopupRadius = null;
                    return hadPop;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Clears all corner radius overrides without resetting the top-bar layout variant.
        /// </summary>
        public void ClearAllRadii()
        {
            ButtonRadius = null;
            InputRadius = null;
            CardRadius = null;
            PanelRadius = null;
            PopupRadius = null;
            TopBarRadius = null;
        }

        /// <summary>
        /// Clears all geometry overrides including the top-bar layout variant and gradient toggles.
        /// </summary>
        public void ClearAll()
        {
            ClearAllRadii();
            TopBarVariant = null;
            EnableGradients = null;
        }

        /// <summary>
        /// Returns whether any geometry role has an explicit override.
        /// </summary>
        public bool HasOverrides =>
            ButtonRadius.HasValue || InputRadius.HasValue || CardRadius.HasValue || PanelRadius.HasValue || PopupRadius.HasValue || TopBarVariant.HasValue || TopBarRadius.HasValue || EnableGradients.HasValue;

        /// <summary>
        /// Indicates whether no geometry overrides are configured.
        /// </summary>
        public bool IsEmpty => !HasOverrides;

        /// <summary>
        /// Creates a deep clone of this geometry definition.
        /// </summary>
        public ThemeGeometryDefinition DeepClone()
        {
            var clone = new ThemeGeometryDefinition
            {
                ButtonRadius = ButtonRadius,
                InputRadius = InputRadius,
                CardRadius = CardRadius,
                PanelRadius = PanelRadius,
                PopupRadius = PopupRadius,
                TopBarVariant = TopBarVariant,
                TopBarRadius = TopBarRadius,
                EnableGradients = EnableGradients,
            };

            if (Extensions != null)
                clone.Extensions = new Dictionary<string, JToken>(Extensions);

            return clone;
        }

        public bool Equals(ThemeGeometryDefinition? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Nullable.Equals(ButtonRadius, other.ButtonRadius)
                   && Nullable.Equals(InputRadius, other.InputRadius)
                   && Nullable.Equals(CardRadius, other.CardRadius)
                   && Nullable.Equals(PanelRadius, other.PanelRadius)
                   && Nullable.Equals(PopupRadius, other.PopupRadius)
                   && TopBarVariant == other.TopBarVariant
                   && Nullable.Equals(TopBarRadius, other.TopBarRadius)
                   && Nullable.Equals(EnableGradients, other.EnableGradients);
        }

        public override bool Equals(object? obj) => obj is ThemeGeometryDefinition other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(ButtonRadius, InputRadius, CardRadius, PanelRadius, HashCode.Combine(PopupRadius, TopBarVariant, TopBarRadius, EnableGradients));
        }
    }
}
