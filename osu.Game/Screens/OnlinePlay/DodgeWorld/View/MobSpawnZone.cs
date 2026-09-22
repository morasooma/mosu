// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The area a group of mobs lives in. Visible only in the editor; what the player sees are the
    /// mobs it spawns.
    /// </summary>
    /// <remarks>
    /// Its texture is therefore the appearance of <em>its mobs</em>, not of the zone. The zone shows
    /// it as a preview so that authoring a fight does not require closing the editor to see it.
    /// </remarks>
    internal partial class MobSpawnZone : EditableWorldEntity, ITexturedEntity
    {
        private readonly Sprite mobPreview;
        private readonly EntityTexture texture = new EntityTexture();

        public string? TexturePath => texture.Path;
        public bool TextureSmoothing => texture.Smoothing;
        public float TextureOpacity => texture.Opacity;
        public bool TextureRepeats => false;

        /// <summary>A mob is always fitted whole into its box, so there is no choice to offer.</summary>
        public string? TextureFillModeName => null;

        public int TextureFillModeIndex => 0;

        public void CycleTextureFill()
        {
        }

        /// <summary>
        /// The image every mob of this zone is drawn with, once resolved.
        /// </summary>
        public Texture? MobTexture => texture.Resolved;

        public int SpawnCount { get; set; } = 3;
        public int MobMaxHealth { get; set; } = 3;
        public int ContactDamage { get; set; } = 10;
        public int ExperienceReward { get; set; } = 10;
        public int CoinsReward { get; set; } = 1;
        public int? MaximumFarmLevel { get; set; }
        public float DetectionRadius { get; set; } = 420;
        public int ProjectileCount { get; set; } = 8;
        public int ProjectileDamage { get; set; } = 8;
        public float ProjectileSpeed { get; set; } = 110;
        public float ProjectileRange { get; set; } = 240;
        public int AttackCooldown { get; set; } = 2200;
        public float MobSpeed { get; set; } = MobSpawnRules.DEFAULT_SPEED;
        public bool Chases { get; set; }
        public override bool BlocksMovement => false;
        public override float? FixedDepth => -8990;
        public override string LayoutKind => "mob-spawn";
        public override bool CanResize => true;
        public override bool CanScale => false;

        public MobSpawnZone(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select, Vector2 size)
            : base(editing, select, colours.Orange1)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = size;
            Alpha = 0;
            AddRangeInternal(new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Orange3.Opacity(0.28f),
                },
                mobPreview = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = WorldMob.SPRITE_SIZE,
                    Alpha = 0,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "MOB SPAWN ZONE",
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                    Colour = colours.Orange0,
                },
            });
        }

        public override void SetEditing(bool value)
        {
            Alpha = value ? 0.82f : 0;
            base.SetEditing(value);
        }

        public void SetTexturePath(string? path)
        {
            texture.SetPath(path);

            if (texture.Path == null)
                SetTexture(null);
        }

        public void SetTexture(Texture? value)
        {
            texture.SetResolved(value);
            mobPreview.Texture = value;
            mobPreview.Alpha = value == null ? 0 : texture.Opacity;

            // Sized exactly as the mobs will be drawn, so the preview is a preview.
            if (value != null)
                mobPreview.Size = MobAppearance.FitInto(value, WorldMob.SPRITE_SIZE);
        }

        public void SetTextureOpacity(float value)
        {
            texture.SetOpacity(value);

            if (mobPreview.Texture != null)
                mobPreview.Alpha = texture.Opacity;
        }

        public void ToggleTextureSmoothing() => texture.ToggleSmoothing();

        public void ReadTexture(string? path, float? opacity, bool? smoothing) => texture.Read(path, opacity, smoothing);
    }
}
