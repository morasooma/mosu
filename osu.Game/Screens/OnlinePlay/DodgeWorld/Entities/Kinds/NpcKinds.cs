// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds
{
    /// <summary>
    /// Shared dialogue persistence for every talkable entity.
    /// </summary>
    internal abstract class InteractiveNpcKind<T> : WorldEntityKind<T>
        where T : InteractiveNpc, ITexturedEntity
    {
        protected override void Read(T entity, EntityRecord record, WorldEntityContext context)
        {
            entity.ReadTexture(record.Texture, record.TextureOpacity, record.TextureSmoothing);
            context.RequestTexture(entity);
        }

        protected override void Write(T entity, EntityRecord record)
        {
            string[] languages = entity.DialogueLanguages.ToArray();

            // The first branch is written the way it always was, in both the localised and the legacy
            // single-language field, so that an older client opening this world shows something rather
            // than an empty bubble.
            record.Dialogue = entity.GetConfiguredDialoguePages("en", 0).ToArray();
            record.DialogueLocalized = languages.ToDictionary(language => language,
                language => entity.GetConfiguredDialoguePages(language, 0).ToArray());

            // The rest only exists once the author has written a second branch, so a world of one-branch
            // characters keeps exactly the shape it had.
            record.DialogueBranches = entity.BranchCount > 1
                ? languages.ToDictionary(language => language,
                    language => entity.GetConfiguredBranches(language).Select(pages => pages.ToArray()).ToArray())
                : null;

            record.Texture = entity.TexturePath;
            record.TextureOpacity = entity.TextureOpacity;
            record.TextureSmoothing = entity.TextureSmoothing;
        }
    }

    /// <summary>
    /// Mora, the guide standing in the plaza. Exactly one exists per world.
    /// </summary>
    internal sealed class MoraKind : InteractiveNpcKind<MoraNpc>
    {
        public override string Kind => EntityKinds.MORA;

        protected override MoraNpc CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new MoraNpc(context.Textures, context.Colours, context.IsEditing, context.Select,
                context.DialogueLanguage, record.Dialogue, record.DialogueLocalized, record.DialogueBranches);
    }

    /// <summary>
    /// An author-placed NPC with editable dialogue.
    /// </summary>
    internal sealed class NpcKind : InteractiveNpcKind<GenericNpc>
    {
        private const string fallback_name = "NPC";

        public override string Kind => EntityKinds.NPC;

        protected override GenericNpc CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new GenericNpc(context.Colours, context.IsEditing, context.Select, record.DisplayName ?? fallback_name,
                context.DialogueLanguage, record.Dialogue, record.DialogueLocalized, record.DialogueBranches);

        protected override void Read(GenericNpc entity, EntityRecord record, WorldEntityContext context)
        {
            // Before the texture, so that resolving the image applies the right fill mode straight away.
            entity.SetTextureFill(record.TextureFill ?? (int)SurfaceFillMode.Fit);
            entity.Alive = record.Alive ?? true;
            entity.CastsShadow = record.Shadow ?? true;

            base.Read(entity, record, context);
        }

        protected override void Write(GenericNpc entity, EntityRecord record)
        {
            base.Write(entity, record);

            record.TextureFill = entity.TextureFillModeIndex;
            record.Alive = entity.Alive;
            record.Shadow = entity.CastsShadow;
        }

        protected override void Initialise(EntityRecord record)
        {
            record.DialogueLocalized = new Dictionary<string, string[]>();
            record.TextureFill = (int)SurfaceFillMode.Fit;
        }

        protected override IEnumerable<EntityField> DescribeFields()
        {
            yield return Switch("Живое существо", "выключено — это предмет: без имени над ним и с коротким «E»",
                npc => npc.Alive, (npc, alive) => npc.Alive = alive);

            yield return Switch("Тень", "выключено — предмет не отбрасывает тень",
                npc => npc.CastsShadow, (npc, shadow) => npc.CastsShadow = shadow);

            yield return Field("Ширина", "единиц; вместе с высотой задаёт, какого размера картинка",
                npc => EditorValue.Format(npc.Size.X),
                (npc, text) => npc.Size = new Vector2(EditorValue.Float(text, npc.Size.X, 32, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.X), npc.Size.Y));

            yield return Field("Высота", "единиц",
                npc => EditorValue.Format(npc.Size.Y),
                (npc, text) => npc.Size = new Vector2(npc.Size.X, EditorValue.Float(text, npc.Size.Y, 32, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.Y)));
        }
    }
}
