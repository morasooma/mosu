// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;

namespace osu.Game.Graphics
{
    public static class OsuIcon
    {
        public const string FONT_NAME = @"Icons";

        // ruleset icons
        public static IconUsage RulesetOsu => get(OsuIconMapping.RulesetOsu);
        public static IconUsage RulesetMania => get(OsuIconMapping.RulesetMania);
        public static IconUsage RulesetCatch => get(OsuIconMapping.RulesetCatch);
        public static IconUsage RulesetTaiko => get(OsuIconMapping.RulesetTaiko);
        public static IconUsage RulesetDodge => get(OsuIconMapping.RulesetDodge);

        public static IconUsage Logo => get(OsuIconMapping.Logo);
        public static IconUsage EditCircle => get(OsuIconMapping.EditCircle);
        public static IconUsage LeftCircle => get(OsuIconMapping.LeftCircle);
        public static IconUsage RightCircle => get(OsuIconMapping.RightCircle);
        public static IconUsage Undo => get(OsuIconMapping.Undo);

        public static IconUsage Audio => get(OsuIconMapping.Audio);
        public static IconUsage Beatmap => get(OsuIconMapping.Beatmap);
        public static IconUsage Calendar => get(OsuIconMapping.Calendar);
        public static IconUsage ChangelogA => get(OsuIconMapping.ChangelogA);
        public static IconUsage ChangelogB => get(OsuIconMapping.ChangelogB);
        public static IconUsage Chat => get(OsuIconMapping.Chat);
        public static IconUsage CheckCircle => get(OsuIconMapping.CheckCircle);
        public static IconUsage Clock => get(OsuIconMapping.Clock);
        public static IconUsage CollapseA => get(OsuIconMapping.CollapseA);
        public static IconUsage Collections => get(OsuIconMapping.Collections);
        public static IconUsage Cross => get(OsuIconMapping.Cross);
        public static IconUsage CrossCircle => get(OsuIconMapping.CrossCircle);
        public static IconUsage Crown => get(OsuIconMapping.Crown);
        public static IconUsage DailyChallenge => get(OsuIconMapping.DailyChallenge);
        public static IconUsage Debug => get(OsuIconMapping.Debug);
        public static IconUsage Delete => get(OsuIconMapping.Delete);
        public static IconUsage Details => get(OsuIconMapping.Details);
        public static IconUsage Discord => get(OsuIconMapping.Discord);
        public static IconUsage EllipsisHorizontal => get(OsuIconMapping.EllipsisHorizontal);
        public static IconUsage EllipsisVertical => get(OsuIconMapping.EllipsisVertical);
        public static IconUsage ExpandA => get(OsuIconMapping.ExpandA);
        public static IconUsage ExpandB => get(OsuIconMapping.ExpandB);
        public static IconUsage FeaturedArtist => get(OsuIconMapping.FeaturedArtist);
        public static IconUsage FeaturedArtistCircle => get(OsuIconMapping.FeaturedArtistCircle);
        public static IconUsage GameplayA => get(OsuIconMapping.GameplayA);
        public static IconUsage GameplayB => get(OsuIconMapping.GameplayB);
        public static IconUsage GameplayC => get(OsuIconMapping.GameplayC);
        public static IconUsage Global => get(OsuIconMapping.Global);
        public static IconUsage Graphics => get(OsuIconMapping.Graphics);
        public static IconUsage Heart => get(OsuIconMapping.Heart);
        public static IconUsage Home => get(OsuIconMapping.Home);
        public static IconUsage Input => get(OsuIconMapping.Input);
        public static IconUsage Maintenance => get(OsuIconMapping.Maintenance);
        public static IconUsage Megaphone => get(OsuIconMapping.Megaphone);
        public static IconUsage Metronome => get(OsuIconMapping.Metronome);
        public static IconUsage Music => get(OsuIconMapping.Music);
        public static IconUsage News => get(OsuIconMapping.News);
        public static IconUsage Next => get(OsuIconMapping.Next);
        public static IconUsage NextCircle => get(OsuIconMapping.NextCircle);
        public static IconUsage Notification => get(OsuIconMapping.Notification);
        public static IconUsage Online => get(OsuIconMapping.Online);
        public static IconUsage Play => get(OsuIconMapping.Play);
        public static IconUsage Player => get(OsuIconMapping.Player);
        public static IconUsage PlayerFollow => get(OsuIconMapping.PlayerFollow);
        public static IconUsage Prev => get(OsuIconMapping.Prev);
        public static IconUsage PrevCircle => get(OsuIconMapping.PrevCircle);
        public static IconUsage Ranking => get(OsuIconMapping.Ranking);
        public static IconUsage Rulesets => get(OsuIconMapping.Rulesets);
        public static IconUsage Search => get(OsuIconMapping.Search);
        public static IconUsage Settings => get(OsuIconMapping.Settings);
        public static IconUsage SkinA => get(OsuIconMapping.SkinA);
        public static IconUsage SkinB => get(OsuIconMapping.SkinB);
        public static IconUsage Star => get(OsuIconMapping.Star);
        public static IconUsage Storyboard => get(OsuIconMapping.Storyboard);
        public static IconUsage Team => get(OsuIconMapping.Team);
        public static IconUsage ThumbsUp => get(OsuIconMapping.ThumbsUp);
        public static IconUsage Tournament => get(OsuIconMapping.Tournament);
        public static IconUsage Twitter => get(OsuIconMapping.Twitter);
        public static IconUsage UserInterface => get(OsuIconMapping.UserInterface);
        public static IconUsage Wiki => get(OsuIconMapping.Wiki);
        public static IconUsage EditorHitCircle => get(OsuIconMapping.EditorHitCircle);
        public static IconUsage EditorSlider => get(OsuIconMapping.EditorSlider);
        public static IconUsage EditorSpinner => get(OsuIconMapping.EditorSpinner);
        public static IconUsage EditorHit => get(OsuIconMapping.EditorHit);
        public static IconUsage EditorDrumRoll => get(OsuIconMapping.EditorDrumRoll);
        public static IconUsage EditorSwell => get(OsuIconMapping.EditorSwell);
        public static IconUsage EditorFruit => get(OsuIconMapping.EditorFruit);
        public static IconUsage EditorJuiceStream => get(OsuIconMapping.EditorJuiceStream);
        public static IconUsage EditorNote => get(OsuIconMapping.EditorNote);
        public static IconUsage EditorHoldNote => get(OsuIconMapping.EditorHoldNote);
        public static IconUsage EditorBananaShower => get(OsuIconMapping.EditorBananaShower);
        public static IconUsage EditorGrid => get(OsuIconMapping.EditorGrid);
        public static IconUsage EditorAddControlPoint => get(OsuIconMapping.EditorAddControlPoint);
        public static IconUsage EditorConvertToStream => get(OsuIconMapping.EditorConvertToStream);
        public static IconUsage EditorDistanceSnap => get(OsuIconMapping.EditorDistanceSnap);
        public static IconUsage EditorFinish => get(OsuIconMapping.EditorFinish);
        public static IconUsage EditorGridSnap => get(OsuIconMapping.EditorGridSnap);
        public static IconUsage EditorNewComboSparkles => get(OsuIconMapping.EditorNewComboSparkles);
        public static IconUsage EditorSelect => get(OsuIconMapping.EditorSelect);
        public static IconUsage EditorSound => get(OsuIconMapping.EditorSound);
        public static IconUsage EditorWhistle => get(OsuIconMapping.EditorWhistle);
        public static IconUsage EditorClap => get(OsuIconMapping.EditorClap);
        public static IconUsage EditorBankAuto => get(OsuIconMapping.EditorBankAuto);
        public static IconUsage EditorBankAutoCompact => get(OsuIconMapping.EditorBankAutoCompact);
        public static IconUsage EditorBankNormal => get(OsuIconMapping.EditorBankNormal);
        public static IconUsage EditorBankNormalCompact => get(OsuIconMapping.EditorBankNormalCompact);
        public static IconUsage EditorBankSoft => get(OsuIconMapping.EditorBankSoft);
        public static IconUsage EditorBankSoftCompact => get(OsuIconMapping.EditorBankSoftCompact);
        public static IconUsage EditorBankDrum => get(OsuIconMapping.EditorBankDrum);
        public static IconUsage EditorBankDrumCompact => get(OsuIconMapping.EditorBankDrumCompact);
        public static IconUsage Tortoise => get(OsuIconMapping.Tortoise);
        public static IconUsage Hare => get(OsuIconMapping.Hare);

        // Consistent outline navigation, separate from accepted mode/mod artwork.
        public static IconUsage NavigationCrown => get(OsuIconMapping.NavigationCrown);
        public static IconUsage NavigationPalette => get(OsuIconMapping.NavigationPalette);
        public static IconUsage NavigationCompass => get(OsuIconMapping.NavigationCompass);
        public static IconUsage NavigationPlug => get(OsuIconMapping.NavigationPlug);
        public static IconUsage NavigationSliders => get(OsuIconMapping.NavigationSliders);
        public static IconUsage NavigationGauge => get(OsuIconMapping.NavigationGauge);
        public static IconUsage NavigationBrain => get(OsuIconMapping.NavigationBrain);
        public static IconUsage NavigationBug => get(OsuIconMapping.NavigationBug);
        public static IconUsage NavigationUsers => get(OsuIconMapping.NavigationUsers);
        public static IconUsage NavigationBackup => get(OsuIconMapping.NavigationBackup);
        public static IconUsage NavigationServers => get(OsuIconMapping.NavigationServers);
        public static IconUsage NavigationExpand => get(OsuIconMapping.NavigationExpand);
        public static IconUsage NavigationSettings => get(OsuIconMapping.NavigationSettings);

        // mod icons

        public static IconUsage ModNoMod => get(OsuIconMapping.ModNoMod);
        public static IconUsage ModMosuAimAssist => get(OsuIconMapping.ModMosuAimAssist);
        public static IconUsage ModMosuAntiAimAssist => get(OsuIconMapping.ModMosuAntiAimAssist);
        public static IconUsage ModMosuStaticBpm => get(OsuIconMapping.ModMosuStaticBpm);
        public static IconUsage ModMosuTargetDifficulty => get(OsuIconMapping.ModMosuTargetDifficulty);
        public static IconUsage ModMosuMalevich => get(OsuIconMapping.ModMosuMalevich);

        /*
         can be regenerated semi-automatically using osu-web's mod database via

         $ jq -r '.[].Mods[].Name' mods.json | sort | uniq | \
           sed 's/ //g' | \
           awk '{print "public static IconUsage Mod" $0 " => get(OsuIconMapping.Mod" $0 ");"}' | pbcopy
         */

        public static IconUsage ModAccuracyChallenge => get(OsuIconMapping.ModAccuracyChallenge);
        public static IconUsage ModAdaptiveSpeed => get(OsuIconMapping.ModAdaptiveSpeed);
        public static IconUsage ModAlternate => get(OsuIconMapping.ModAlternate);
        public static IconUsage ModApproachDifferent => get(OsuIconMapping.ModApproachDifferent);
        public static IconUsage ModAutopilot => get(OsuIconMapping.ModAutopilot);
        public static IconUsage ModAutoplay => get(OsuIconMapping.ModAutoplay);
        public static IconUsage ModBarrelRoll => get(OsuIconMapping.ModBarrelRoll);
        public static IconUsage ModBlinds => get(OsuIconMapping.ModBlinds);
        public static IconUsage ModBloom => get(OsuIconMapping.ModBloom);
        public static IconUsage ModBubbles => get(OsuIconMapping.ModBubbles);
        public static IconUsage ModCinema => get(OsuIconMapping.ModCinema);
        public static IconUsage ModClassic => get(OsuIconMapping.ModClassic);
        public static IconUsage ModConstantSpeed => get(OsuIconMapping.ModConstantSpeed);
        public static IconUsage ModCover => get(OsuIconMapping.ModCover);
        public static IconUsage ModDaycore => get(OsuIconMapping.ModDaycore);
        public static IconUsage ModDeflate => get(OsuIconMapping.ModDeflate);
        public static IconUsage ModDepth => get(OsuIconMapping.ModDepth);
        public static IconUsage ModDifficultyAdjust => get(OsuIconMapping.ModDifficultyAdjust);
        public static IconUsage ModDoubleTime => get(OsuIconMapping.ModDoubleTime);
        public static IconUsage ModDualStages => get(OsuIconMapping.ModDualStages);
        public static IconUsage ModEasy => get(OsuIconMapping.ModEasy);
        public static IconUsage ModEightKeys => get(OsuIconMapping.ModEightKeys);
        public static IconUsage ModFadeIn => get(OsuIconMapping.ModFadeIn);
        public static IconUsage ModFiveKeys => get(OsuIconMapping.ModFiveKeys);
        public static IconUsage ModFlashlight => get(OsuIconMapping.ModFlashlight);
        public static IconUsage ModFloatingFruits => get(OsuIconMapping.ModFloatingFruits);
        public static IconUsage ModFourKeys => get(OsuIconMapping.ModFourKeys);
        public static IconUsage ModFreezeFrame => get(OsuIconMapping.ModFreezeFrame);
        public static IconUsage ModGrow => get(OsuIconMapping.ModGrow);
        public static IconUsage ModHalfTime => get(OsuIconMapping.ModHalfTime);
        public static IconUsage ModHardRock => get(OsuIconMapping.ModHardRock);
        public static IconUsage ModHidden => get(OsuIconMapping.ModHidden);
        public static IconUsage ModHoldOff => get(OsuIconMapping.ModHoldOff);
        public static IconUsage ModInvert => get(OsuIconMapping.ModInvert);
        public static IconUsage ModMagnetised => get(OsuIconMapping.ModMagnetised);
        public static IconUsage ModMirror => get(OsuIconMapping.ModMirror);
        public static IconUsage ModMovingFast => get(OsuIconMapping.ModMovingFast);
        public static IconUsage ModMuted => get(OsuIconMapping.ModMuted);
        public static IconUsage ModNightcore => get(OsuIconMapping.ModNightcore);
        public static IconUsage ModNineKeys => get(OsuIconMapping.ModNineKeys);
        public static IconUsage ModNoFail => get(OsuIconMapping.ModNoFail);
        public static IconUsage ModNoRelease => get(OsuIconMapping.ModNoRelease);
        public static IconUsage ModNoScope => get(OsuIconMapping.ModNoScope);
        public static IconUsage ModOneKey => get(OsuIconMapping.ModOneKey);
        public static IconUsage ModPerfect => get(OsuIconMapping.ModPerfect);
        public static IconUsage ModRandom => get(OsuIconMapping.ModRandom);
        public static IconUsage ModRelax => get(OsuIconMapping.ModRelax);
        public static IconUsage ModRepel => get(OsuIconMapping.ModRepel);
        public static IconUsage ModScoreV2 => get(OsuIconMapping.ModScoreV2);
        public static IconUsage ModSevenKeys => get(OsuIconMapping.ModSevenKeys);
        public static IconUsage ModSimplifiedRhythm => get(OsuIconMapping.ModSimplifiedRhythm);
        public static IconUsage ModSingleTap => get(OsuIconMapping.ModSingleTap);
        public static IconUsage ModSixKeys => get(OsuIconMapping.ModSixKeys);
        public static IconUsage ModSpinIn => get(OsuIconMapping.ModSpinIn);
        public static IconUsage ModSpunOut => get(OsuIconMapping.ModSpunOut);
        public static IconUsage ModStrictTracking => get(OsuIconMapping.ModStrictTracking);
        public static IconUsage ModSuddenDeath => get(OsuIconMapping.ModSuddenDeath);
        public static IconUsage ModSwap => get(OsuIconMapping.ModSwap);
        public static IconUsage ModSynesthesia => get(OsuIconMapping.ModSynesthesia);
        public static IconUsage ModTargetPractice => get(OsuIconMapping.ModTargetPractice);
        public static IconUsage ModTenKeys => get(OsuIconMapping.ModTenKeys);
        public static IconUsage ModThreeKeys => get(OsuIconMapping.ModThreeKeys);
        public static IconUsage ModTouchDevice => get(OsuIconMapping.ModTouchDevice);
        public static IconUsage ModTraceable => get(OsuIconMapping.ModTraceable);
        public static IconUsage ModTransform => get(OsuIconMapping.ModTransform);
        public static IconUsage ModTwoKeys => get(OsuIconMapping.ModTwoKeys);
        public static IconUsage ModWiggle => get(OsuIconMapping.ModWiggle);
        public static IconUsage ModWindDown => get(OsuIconMapping.ModWindDown);
        public static IconUsage ModWindUp => get(OsuIconMapping.ModWindUp);

        private static IconUsage get(OsuIconMapping glyph) => new IconUsage((char)glyph, FONT_NAME);

        private enum OsuIconMapping
        {
            [Description(@"Logo"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3398))]
            Logo,

            [Description(@"RulesetOsu"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3399))]
            RulesetOsu,

            [Description(@"RulesetMania"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3400))]
            RulesetMania,

            [Description(@"RulesetCatch"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3401))]
            RulesetCatch,

            [Description(@"RulesetTaiko"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3402))]
            RulesetTaiko,

            [Description(@"RulesetDodge"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3403))]
            RulesetDodge,

            [Description(@"EditCircle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3404))]
            EditCircle,

            [Description(@"LeftCircle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3405))]
            LeftCircle,

            [Description(@"RightCircle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3406))]
            RightCircle,

            [Description(@"audio"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3407))]
            Audio,

            [Description(@"beatmap"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3408))]
            Beatmap,

            [Description(@"calendar"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3409))]
            Calendar,

            [Description(@"changelog-a"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3410))]
            ChangelogA,

            [Description(@"changelog-b"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3411))]
            ChangelogB,

            [Description(@"chat"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3412))]
            Chat,

            [Description(@"check-circle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3413))]
            CheckCircle,

            [Description(@"clock"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3414))]
            Clock,

            [Description(@"collapse-a"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3415))]
            CollapseA,

            [Description(@"collections"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3416))]
            Collections,

            [Description(@"cross"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3417))]
            Cross,

            [Description(@"cross-circle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3418))]
            CrossCircle,

            [Description(@"crown"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3419))]
            Crown,

            [Description(@"daily-challenge"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3420))]
            DailyChallenge,

            [Description(@"debug"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3421))]
            Debug,

            [Description(@"delete"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3422))]
            Delete,

            [Description(@"details"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3423))]
            Details,

            [Description(@"discord"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3424))]
            Discord,

            [Description(@"ellipsis-horizontal"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3425))]
            EllipsisHorizontal,

            [Description(@"ellipsis-vertical"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3426))]
            EllipsisVertical,

            [Description(@"expand-a"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3427))]
            ExpandA,

            [Description(@"expand-b"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3428))]
            ExpandB,

            [Description(@"featured-artist"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3429))]
            FeaturedArtist,

            [Description(@"featured-artist-circle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3430))]
            FeaturedArtistCircle,

            [Description(@"gameplay-a"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3431))]
            GameplayA,

            [Description(@"gameplay-b"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3432))]
            GameplayB,

            [Description(@"gameplay-c"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3433))]
            GameplayC,

            [Description(@"global"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3434))]
            Global,

            [Description(@"graphics"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3435))]
            Graphics,

            [Description(@"heart"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3436))]
            Heart,

            [Description(@"home"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3437))]
            Home,

            [Description(@"input"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3438))]
            Input,

            [Description(@"maintenance"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3439))]
            Maintenance,

            [Description(@"megaphone"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3440))]
            Megaphone,

            [Description(@"metronome"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3441))]
            Metronome,

            [Description(@"music"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3442))]
            Music,

            [Description(@"news"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3443))]
            News,

            [Description(@"next"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3444))]
            Next,

            [Description(@"next-circle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3445))]
            NextCircle,

            [Description(@"notification"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3446))]
            Notification,

            [Description(@"online"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3447))]
            Online,

            [Description(@"play"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3448))]
            Play,

            [Description(@"player"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3449))]
            Player,

            [Description(@"player-follow"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3450))]
            PlayerFollow,

            [Description(@"prev"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3451))]
            Prev,

            [Description(@"prev-circle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3452))]
            PrevCircle,

            [Description(@"ranking"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3453))]
            Ranking,

            [Description(@"rulesets"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3454))]
            Rulesets,

            [Description(@"search"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3455))]
            Search,

            [Description(@"settings"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3456))]
            Settings,

            [Description(@"skin-a"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3457))]
            SkinA,

            [Description(@"skin-b"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3458))]
            SkinB,

            [Description(@"star"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3459))]
            Star,

            [Description(@"storyboard"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3460))]
            Storyboard,

            [Description(@"team"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3461))]
            Team,

            [Description(@"thumbs-up"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3462))]
            ThumbsUp,

            [Description(@"tournament"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3463))]
            Tournament,

            [Description(@"twitter"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3464))]
            Twitter,

            [Description(@"undo"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3465))]
            Undo,

            [Description(@"user-interface"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3466))]
            UserInterface,

            [Description(@"wiki"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3467))]
            Wiki,

            [Description(@"Editor/hitcircle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3468))]
            EditorHitCircle,

            [Description(@"Editor/slider"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3469))]
            EditorSlider,

            [Description(@"Editor/spinner"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3470))]
            EditorSpinner,

            [Description(@"Editor/hit"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3471))]
            EditorHit,

            [Description(@"Editor/drum-roll"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3472))]
            EditorDrumRoll,

            [Description(@"Editor/swell"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3473))]
            EditorSwell,

            [Description(@"Editor/fruit"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3474))]
            EditorFruit,

            [Description(@"Editor/juice-stream"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3475))]
            EditorJuiceStream,

            [Description(@"Editor/banana-shower"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3476))]
            EditorBananaShower,

            [Description(@"Editor/note"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3477))]
            EditorNote,

            [Description(@"Editor/hold-note"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3478))]
            EditorHoldNote,

            [Description(@"Editor/grid"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3479))]
            EditorGrid,

            [Description(@"Editor/add-control-point"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3480))]
            EditorAddControlPoint = 1000,

            [Description(@"Editor/convert-to-stream"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3481))]
            EditorConvertToStream,

            [Description(@"Editor/distance-snap"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3482))]
            EditorDistanceSnap,

            [Description(@"Editor/finish"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3483))]
            EditorFinish,

            [Description(@"Editor/grid-snap"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3484))]
            EditorGridSnap,

            [Description(@"Editor/new-combo-sparkles"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3485))]
            EditorNewComboSparkles,

            [Description(@"Editor/select"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3486))]
            EditorSelect,

            [Description(@"Editor/sound"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3487))]
            EditorSound,

            [Description(@"Editor/whistle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3488))]
            EditorWhistle,

            [Description(@"Editor/clap"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3489))]
            EditorClap,

            [Description(@"Editor/bank-auto"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3490))]
            EditorBankAuto,

            [Description(@"Editor/bank-auto-compact"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3491))]
            EditorBankAutoCompact,

            [Description(@"Editor/bank-normal"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3492))]
            EditorBankNormal,

            [Description(@"Editor/bank-normal-compact"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3493))]
            EditorBankNormalCompact,

            [Description(@"Editor/bank-soft"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3494))]
            EditorBankSoft,

            [Description(@"Editor/bank-soft-compact"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3495))]
            EditorBankSoftCompact,

            [Description(@"Editor/bank-drum"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3496))]
            EditorBankDrum,

            [Description(@"Editor/bank-drum-compact"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3497))]
            EditorBankDrumCompact,

            [Description(@"tortoise"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3498))]
            Tortoise,

            [Description(@"hare"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3499))]
            Hare,

            // mod icons

            [Description(@"Mods/mod-no-mod"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3500))]
            ModNoMod,

            /*
             rest can be regenerated semi-automatically using osu-web's mod database via
             $ jq -r '.[].Mods[].Name' mods.json | sort | uniq | \
               awk '{kebab = $0; gsub(" ", "-", kebab); pascal = $0; gsub(" ", "", pascal); print "[Description(@\"Mods/mod-" tolower(kebab) "\")]\nMod" pascal ",\n" }' | pbcopy
             */

            [Description(@"Mods/mod-accuracy-challenge"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3501))]
            ModAccuracyChallenge,

            [Description(@"Mods/mod-adaptive-speed"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3502))]
            ModAdaptiveSpeed,

            [Description(@"Mods/mod-alternate"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3503))]
            ModAlternate,

            [Description(@"Mods/mod-approach-different"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3504))]
            ModApproachDifferent,

            [Description(@"Mods/mod-autopilot"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3505))]
            ModAutopilot,

            [Description(@"Mods/mod-autoplay"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3506))]
            ModAutoplay,

            [Description(@"Mods/mod-barrel-roll"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3507))]
            ModBarrelRoll,

            [Description(@"Mods/mod-blinds"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3508))]
            ModBlinds,

            [Description(@"Mods/mod-bloom"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3509))]
            ModBloom,

            [Description(@"Mods/mod-bubbles"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3510))]
            ModBubbles,

            [Description(@"Mods/mod-cinema"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3511))]
            ModCinema,

            [Description(@"Mods/mod-classic"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3512))]
            ModClassic,

            [Description(@"Mods/mod-constant-speed"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3513))]
            ModConstantSpeed,

            [Description(@"Mods/mod-cover"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3514))]
            ModCover,

            [Description(@"Mods/mod-daycore"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3515))]
            ModDaycore,

            [Description(@"Mods/mod-deflate"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3516))]
            ModDeflate,

            [Description(@"Mods/mod-depth"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3517))]
            ModDepth,

            [Description(@"Mods/mod-difficulty-adjust"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3518))]
            ModDifficultyAdjust,

            [Description(@"Mods/mod-double-time"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3519))]
            ModDoubleTime,

            [Description(@"Mods/mod-dual-stages"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3520))]
            ModDualStages,

            [Description(@"Mods/mod-easy"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3521))]
            ModEasy,

            [Description(@"Mods/mod-eight-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3522))]
            ModEightKeys,

            [Description(@"Mods/mod-fade-in"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3523))]
            ModFadeIn,

            [Description(@"Mods/mod-five-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3524))]
            ModFiveKeys,

            [Description(@"Mods/mod-flashlight"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3525))]
            ModFlashlight,

            [Description(@"Mods/mod-floating-fruits"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3526))]
            ModFloatingFruits,

            [Description(@"Mods/mod-four-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3527))]
            ModFourKeys,

            [Description(@"Mods/mod-freeze-frame"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3528))]
            ModFreezeFrame,

            [Description(@"Mods/mod-grow"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3529))]
            ModGrow,

            [Description(@"Mods/mod-half-time"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3530))]
            ModHalfTime,

            [Description(@"Mods/mod-hard-rock"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3531))]
            ModHardRock,

            [Description(@"Mods/mod-hidden"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3532))]
            ModHidden,

            [Description(@"Mods/mod-hold-off"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3533))]
            ModHoldOff,

            [Description(@"Mods/mod-invert"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3534))]
            ModInvert,

            [Description(@"Mods/mod-magnetised"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3535))]
            ModMagnetised,

            [Description(@"Mods/mod-mirror"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3536))]
            ModMirror,

            [Description(@"Mods/mod-moving-fast"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3537))]
            ModMovingFast,

            [Description(@"Mods/mod-muted"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3538))]
            ModMuted,

            [Description(@"Mods/mod-nightcore"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3539))]
            ModNightcore,

            [Description(@"Mods/mod-nine-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3540))]
            ModNineKeys,

            [Description(@"Mods/mod-no-fail"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3541))]
            ModNoFail,

            [Description(@"Mods/mod-no-release"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3542))]
            ModNoRelease,

            [Description(@"Mods/mod-no-scope"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3543))]
            ModNoScope,

            [Description(@"Mods/mod-one-key"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3544))]
            ModOneKey,

            [Description(@"Mods/mod-perfect"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3545))]
            ModPerfect,

            [Description(@"Mods/mod-random"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3546))]
            ModRandom,

            [Description(@"Mods/mod-relax"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3547))]
            ModRelax,

            [Description(@"Mods/mod-repel"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3548))]
            ModRepel,

            [Description(@"Mods/mod-score-v2"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3549))]
            ModScoreV2,

            [Description(@"Mods/mod-seven-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3550))]
            ModSevenKeys,

            [Description(@"Mods/mod-simplified-rhythm"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3551))]
            ModSimplifiedRhythm,

            [Description(@"Mods/mod-single-tap"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3552))]
            ModSingleTap,

            [Description(@"Mods/mod-six-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3553))]
            ModSixKeys,

            [Description(@"Mods/mod-spin-in"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3554))]
            ModSpinIn,

            [Description(@"Mods/mod-spun-out"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3555))]
            ModSpunOut,

            [Description(@"Mods/mod-strict-tracking"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3556))]
            ModStrictTracking,

            [Description(@"Mods/mod-sudden-death"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3557))]
            ModSuddenDeath,

            [Description(@"Mods/mod-swap"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3558))]
            ModSwap,

            [Description(@"Mods/mod-synesthesia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3559))]
            ModSynesthesia,

            [Description(@"Mods/mod-target-practice"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3560))]
            ModTargetPractice,

            [Description(@"Mods/mod-ten-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3561))]
            ModTenKeys,

            [Description(@"Mods/mod-three-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3562))]
            ModThreeKeys,

            [Description(@"Mods/mod-touch-device"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3563))]
            ModTouchDevice,

            [Description(@"Mods/mod-traceable"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3564))]
            ModTraceable,

            [Description(@"Mods/mod-transform"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3565))]
            ModTransform,

            [Description(@"Mods/mod-two-keys"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3566))]
            ModTwoKeys,

            [Description(@"Mods/mod-wiggle"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3567))]
            ModWiggle,

            [Description(@"Mods/mod-wind-down"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3568))]
            ModWindDown,

            [Description(@"Mods/mod-wind-up"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3569))]
            ModWindUp,

            [Description(@"Mods/mod-mosu-aim-assist"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3570))]
            ModMosuAimAssist,

            [Description(@"Mods/mod-mosu-anti-aim-assist"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3571))]
            ModMosuAntiAimAssist,

            [Description(@"Mods/mod-mosu-static-bpm"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3572))]
            ModMosuStaticBpm,

            [Description(@"Mods/mod-mosu-target-difficulty"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3573))]
            ModMosuTargetDifficulty,

            [Description(@"Mods/mod-mosu-malevich"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3574))]
            ModMosuMalevich,

            [Description(@"Navigation/crown"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3575))] NavigationCrown,
            [Description(@"Navigation/palette"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3576))] NavigationPalette,
            [Description(@"Navigation/compass"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3577))] NavigationCompass,
            [Description(@"Navigation/plug"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3578))] NavigationPlug,
            [Description(@"Navigation/sliders-horizontal"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3579))] NavigationSliders,
            [Description(@"Navigation/gauge"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3580))] NavigationGauge,
            [Description(@"Navigation/brain"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3581))] NavigationBrain,
            [Description(@"Navigation/bug"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3582))] NavigationBug,
            [Description(@"Navigation/users"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3583))] NavigationUsers,
            [Description(@"Navigation/floppy-disk"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3584))] NavigationBackup,
            [Description(@"Navigation/hard-drives"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3585))] NavigationServers,
            [Description(@"Navigation/caret-down"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3586))] NavigationExpand,
            [Description(@"Navigation/gear-six"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3587))] NavigationSettings,
        }

        public class OsuIconStore : ITextureStore, ITexturedGlyphLookupStore
        {
            private readonly TextureStore textures;

            public OsuIconStore(TextureStore textures)
            {
                this.textures = textures;
            }

            public ITexturedCharacterGlyph? Get(string? fontName, char character)
            {
                if (fontName == FONT_NAME)
                    return new Glyph(textures.Get($@"{fontName}/{((OsuIconMapping)character).GetDescription()}"));

                return null;
            }

            public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.Run(() => Get(fontName, character));

            public Texture? Get(string name, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public Texture Get(string name) => throw new NotImplementedException();

            public Task<Texture> GetAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Stream GetStream(string name) => throw new NotImplementedException();

            public IEnumerable<string> GetAvailableResources() => throw new NotImplementedException();

            public Task<Texture?> GetAsync(string name, WrapMode wrapModeS, WrapMode wrapModeT, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public class Glyph : ITexturedCharacterGlyph
            {
                public float XOffset => 0;
                public float YOffset => 0;
                public float XAdvance => 0;
                public float Baseline => 0;
                public char Character => '\0';

                public float GetKerning<T>(T lastGlyph) where T : ICharacterGlyph => throw new NotImplementedException();

                public Texture Texture { get; }
                public float Width => Texture.Width;
                public float Height => Texture.Height;

                public Glyph(Texture texture)
                {
                    Texture = texture;
                }
            }

            public void Dispose()
            {
                textures.Dispose();
            }
        }
    }
}
