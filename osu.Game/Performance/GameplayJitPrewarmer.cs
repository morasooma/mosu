using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Performance;
using osu.Framework.Statistics;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Break;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Performance
{
    /// <summary>
    /// Prepares a bounded set of actual gameplay types on Player's background load thread.
    /// Compiles method bodies without calling Update, generating input/judgements, advancing a
    /// clock, playing samples or running gameplay constructors. The only explicitly executed
    /// initializer builds HitResult enum tables. Dynamic serializer delegates remain handled
    /// by the outgoing worker rather than reflection over every assembly.
    /// </summary>
    internal static class GameplayJitPrewarmer
    {
        private static readonly ConcurrentDictionary<Type, bool> prepared = new ConcurrentDictionary<Type, bool>();

        public static void Prepare(Ruleset ruleset, DrawableRuleset drawableRuleset, Drawable hud, CancellationToken cancellationToken, params Type[] actualTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Some editor/test callers synchronously load Player. Never turn their Update
            // into a prewarm stall; the normal PlayerLoader path is background-loaded.
            if (ThreadSafety.IsUpdateThread)
                return;
            using var operation = StutterTraceEventSource.Log.Measure("Gameplay.PrepareJit");
            PrepareScoringTables(cancellationToken);
            prepareHierarchy(typeof(Player), cancellationToken);
            prepareHierarchy(typeof(LifetimeEntryManager), cancellationToken);
            foreach (Type comparer in typeof(LifetimeEntryManager).GetNestedTypes(BindingFlags.NonPublic)
                         .Where(t => typeof(System.Collections.Generic.IComparer<LifetimeEntry>).IsAssignableFrom(t)))
                prepareHierarchy(comparer, cancellationToken);
            prepareHierarchy(typeof(FrameStabilityContainer), cancellationToken);
            prepareHierarchy(typeof(PlayfieldBorder), cancellationToken);
            prepareHierarchy(typeof(BreakOverlay), cancellationToken);
            prepareHierarchy(typeof(LegacyBreakOverlay), cancellationToken);
            prepareHierarchy(typeof(SoloGameplayLeaderboardProvider), cancellationToken);
            prepareHierarchy(typeof(ArgonSkin), cancellationToken);
            prepareHierarchy(typeof(GameplayState), cancellationToken);
            prepareHierarchy(typeof(JudgementResult), cancellationToken);
            prepareHierarchy(typeof(HitResultExtensions), cancellationToken);
            prepareHierarchy(typeof(PerformancePointsCounter), cancellationToken);
            prepareHierarchy(typeof(HealthDisplay), cancellationToken);
            prepareHierarchy(typeof(BarHitErrorMeter), cancellationToken);
            prepareHierarchy(typeof(JudgementCounterDisplay), cancellationToken);
            prepareHierarchy(typeof(SpectatorList), cancellationToken);
            prepareHierarchy(typeof(DrawableGameplayLeaderboard), cancellationToken);
            prepareHierarchy(typeof(DrawableGameplayLeaderboardScore), cancellationToken);
            prepareHierarchy(typeof(GameplayLeaderboardScore), cancellationToken);
            prepareHierarchy(typeof(SongProgress), cancellationToken);
            prepareHierarchy(typeof(SkinnableSound), cancellationToken);
            prepareHierarchy(typeof(PausableSkinnableSound), cancellationToken);
            foreach (Type type in actualTypes)
                prepareHierarchy(type, cancellationToken);
            prepareHierarchy(drawableRuleset.GetType(), cancellationToken);
            prepareHierarchy(hud.GetType(), cancellationToken);
            // Restrict candidates to the implicated object/input/judgement/HUD families,
            // not editor, difficulty, mods or unrelated plugin methods. Reflection does not
            // instantiate drawables, and preparation never executes their constructors.
            PrepareRuleset(ruleset.GetType().Assembly, cancellationToken);
            prepareHud(cancellationToken);
        }

        internal static void PrepareRuleset(Assembly assembly, CancellationToken cancellationToken)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.OfType<Type>().ToArray();
            }
            foreach (Type type in types)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsRulesetCandidate(type))
                    prepareHierarchy(type, cancellationToken);
            }
        }

        private static void prepareHud(CancellationToken cancellationToken)
        {
            // These first-use LoadComplete/skin callbacks were captured in the
            // entry frame. Prepare the bounded built-in HUD family, not editors,
            // arbitrary skin assemblies or a synthetic gameplay Update.
            foreach (Type type in typeof(HUDOverlay).Assembly.GetTypes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsHudCandidate(type))
                    prepareHierarchy(type, cancellationToken);
            }
        }

        internal static bool IsHudCandidate(Type type)
            => !type.ContainsGenericParameters && typeof(Drawable).IsAssignableFrom(type)
               && (type.Namespace == "osu.Game.Screens.Play.HUD"
                   || type.Namespace?.StartsWith("osu.Game.Screens.Play.HUD.", StringComparison.Ordinal) == true
                   || type == typeof(ArgonKeyCounter));

        internal static bool IsRulesetCandidate(Type type)
            => !type.ContainsGenericParameters && !type.IsAbstract
               && (typeof(DrawableHitObject).IsAssignableFrom(type)
                   || type.Name.EndsWith("HitPolicy", StringComparison.Ordinal)
                   || type.Name.EndsWith("InputManager", StringComparison.Ordinal)
                   || type.Name is "RelaxController" or "AimAssistController"
                   || type.Namespace?.EndsWith(".UI.Cursor", StringComparison.Ordinal) == true
                   || type.Namespace?.StartsWith("osu.Game.Rulesets.", StringComparison.Ordinal) == true
                      && type.Namespace.Contains(".Skinning", StringComparison.Ordinal)
                   || type.Namespace?.EndsWith(".Objects.Drawables", StringComparison.Ordinal) == true
                   || type.Namespace?.EndsWith(".Replays", StringComparison.Ordinal) == true
                   || type.Namespace?.Contains(".HUD", StringComparison.Ordinal) == true
                   || type.Namespace?.EndsWith(".Judgements", StringComparison.Ordinal) == true);

        internal static void PrepareType(Type type, CancellationToken token) => prepareHierarchy(type, token);

        internal static void PrepareScoringTables(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ThreadSafety.IsUpdateThread)
                return;

            // PrepareMethod deliberately does not execute static initializers. This one known
            // initializer builds only enum ordering/lookup arrays, with no gameplay side effects.
            // It was still doing reflection/LINQ during the first PopulateScore in gameplay.
            RuntimeHelpers.RunClassConstructor(typeof(HitResultExtensions).TypeHandle);
        }

        private static void prepareHierarchy(Type type, CancellationToken token)
        {
            for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                token.ThrowIfCancellationRequested();
                if (current.ContainsGenericParameters || prepared.ContainsKey(current))
                    continue;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (MethodBase method in current.GetMethods(flags).Cast<MethodBase>().Concat(current.GetConstructors(flags)))
                {
                    token.ThrowIfCancellationRequested();
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                        continue;
                    try
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    }
                    catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException)
                    {
                        // AOT/runtime-specific unsupported method must not prevent loading gameplay.
                    }
                }
                // Compiler-generated non-generic callback bodies otherwise still compile on
                // the first hit/slider. Prepare them, but never instantiate a closure.
                foreach (Type nested in current.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!nested.ContainsGenericParameters && nested.IsDefined(typeof(CompilerGeneratedAttribute), false))
                        prepareHierarchy(nested, token);
                }
                prepared.TryAdd(current, true);
            }
        }
    }
}
