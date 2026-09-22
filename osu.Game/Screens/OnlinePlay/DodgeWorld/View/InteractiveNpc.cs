// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Graphics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Anything the player can talk to. What it says is a list of <em>branches</em>, each branch a list of
    /// pages.
    /// </summary>
    /// <remarks>
    /// A branch is one conversation: it is read through in order, page by page. The next conversation takes
    /// the next branch and comes back round to the first, so a character with one branch repeats it — which
    /// is what every character used to do — and a character with several has something else to say each
    /// time. Nothing has to be switched on for that: a second branch is the author saying so.
    /// <para>
    /// A branch is abandoned rather than spent if the player walks away half way through, so the next
    /// conversation picks it up again from the start instead of skipping it.
    /// </para>
    /// </remarks>
    internal abstract partial class InteractiveNpc : EditableWorldEntity
    {
        private readonly Func<string> languageProvider;

        /// <summary>
        /// Every branch of the conversation, per language. Kept normalised: each language holds the same
        /// number of branches, and matching branches hold the same number of pages — a page present in one
        /// language and missing in another would desynchronise the two.
        /// </summary>
        private readonly Dictionary<string, List<string[]>> branchesByLanguage =
            new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Where the camera settles while this character is talking: midway between the speaker's feet
        /// and the middle of their <see cref="DialogueBubble"/>, so that both are in shot.
        /// </summary>
        public Vector2 DialogueCameraFocus => Position + bubbleOffset() / 2;

        /// <summary>Where the middle of the bubble sits, relative to the speaker's feet.</summary>
        private Vector2 bubbleOffset()
        {
            float sideways = Size.X * Scale.X / 2 + DialogueBubble.GAP + DialogueBubble.SIZE.X / 2;
            float height = DialogueBubble.LIFT + (bubble?.Height ?? DialogueBubble.SIZE.Y) / 2;

            return new Vector2(bubbleOnTheRight ? sideways : -sideways, -height);
        }

        private bool bubbleOnTheRight = true;

        /// <summary>
        /// Chooses which side of the speaker the bubble is drawn on.
        /// </summary>
        /// <remarks>
        /// Asked from outside because the answer is about the room, not about the character: a bubble
        /// pointing at the nearest wall is one the camera cannot pan far enough to show.
        /// </remarks>
        public void SetDialogueSide(bool toTheRight)
        {
            bubbleOnTheRight = toTheRight;
            bubble?.PlaceBeside(toTheRight);
        }

        public bool IsDialogueOpen { get; private set; }

        /// <summary>How many branches this character has.</summary>
        public int BranchCount => Math.Max(1, ActiveLanguageBranches.Count);

        /// <summary>Which branch this conversation is playing, from zero.</summary>
        public int ActiveBranch { get; private set; }

        /// <summary>Which branch the next conversation will open on.</summary>
        private int nextBranch;

        /// <summary>Which page of the open branch is being read, from zero.</summary>
        public int DialoguePage { get; private set; }

        public int DialoguePageCount => ActiveDialoguePages.Count;

        public string CurrentDialogueLine =>
            DialoguePage >= 0 && DialoguePage < ActiveDialoguePages.Count ? ActiveDialoguePages[DialoguePage] : string.Empty;

        public abstract void SetInteractionAvailable(bool available);

        public void BeginDialogue()
        {
            IsDialogueOpen = true;
            ActiveBranch = nextBranch % BranchCount;
            DialoguePage = 0;
            SetInteractionAvailable(false);
        }

        /// <summary>
        /// Raised when the last page of a branch has been read, which is what counts as having spoken to
        /// this character. Walking away halfway through does not.
        /// </summary>
        public Action<InteractiveNpc>? DialogueFinished;

        /// <summary>
        /// Moves to the next page, closing the conversation once the branch has been read out.
        /// </summary>
        public void AdvanceDialogue()
        {
            if (!IsDialogueOpen)
                return;

            DialoguePage++;

            if (DialoguePage < ActiveDialoguePages.Count)
                return;

            // Only a branch that was read through moves the rotation on. Abandoning one leaves it waiting.
            nextBranch = (ActiveBranch + 1) % BranchCount;

            CloseDialogue();
            DialogueFinished?.Invoke(this);
        }

        public void CloseDialogue()
        {
            IsDialogueOpen = false;
            DialoguePage = 0;
        }

        /// <summary>
        /// Gives this character a speech bubble. Called by the subclass rather than the constructor,
        /// so that the bubble is added after the character's own body and draws over it.
        /// </summary>
        /// <param name="colours">The game palette.</param>
        /// <param name="accent">The border and name colour, matching the character.</param>
        protected void AddDialogueBubble(OsuColour colours, Color4 accent)
        {
            AddInternal(bubble = new DialogueBubble(colours, accent, AdvanceDialogue));
            bubble.SetSpeaker(DisplayName);
            bubble.PlaceBeside(bubbleOnTheRight);
        }

        protected override void Update()
        {
            base.Update();
            bubble?.Refresh(IsDialogueOpen, CurrentDialogueLine, DialoguePage, DialoguePageCount);
        }

        public override void SetDisplayName(string value)
        {
            base.SetDisplayName(value);
            bubble?.SetSpeaker(value);
        }

        private DialogueBubble? bubble;

        internal DialogueBubble? BubbleForTesting => bubble;

        /// <param name="editing">Whether the editor is open.</param>
        /// <param name="select">Selects this entity in the editor.</param>
        /// <param name="selectionColour">The colour this entity is outlined in when selected.</param>
        /// <param name="languageProvider">Which language the player reads.</param>
        /// <param name="legacyDialogue">
        /// The single-language page list written by older clients, read as one branch of English.
        /// </param>
        /// <param name="localizedDialogue">
        /// The per-language page list, read as one branch. Superseded by <paramref name="branches"/>, and
        /// ignored when that is present — it holds the same pages as the first branch.
        /// </param>
        /// <param name="branches">Every branch, per language, as stored.</param>
        /// <param name="defaults">What this character says when the world says nothing.</param>
        protected InteractiveNpc(Func<bool> editing, Action<EditableWorldEntity> select, Color4 selectionColour,
                                 Func<string> languageProvider, string[]? legacyDialogue,
                                 Dictionary<string, string[]>? localizedDialogue,
                                 Dictionary<string, string[][]>? branches,
                                 IReadOnlyDictionary<string, string[]> defaults)
            : base(editing, select, selectionColour)
        {
            this.languageProvider = languageProvider;

            foreach ((string language, string[] pages) in defaults)
                branchesByLanguage[language] = new List<string[]> { pages.ToArray() };

            if (legacyDialogue is { Length: > 0 })
                branchesByLanguage["en"] = new List<string[]> { legacyDialogue.ToArray() };

            if (localizedDialogue != null)
            {
                foreach ((string language, string[] pages) in localizedDialogue)
                {
                    if (pages is { Length: > 0 })
                        branchesByLanguage[language] = new List<string[]> { pages.ToArray() };
                }
            }

            if (branches != null)
            {
                foreach ((string language, string[][] stored) in branches)
                {
                    if (stored is { Length: > 0 })
                        branchesByLanguage[language] = stored.Select(pages => (pages ?? Array.Empty<string>()).ToArray()).ToList();
                }
            }

            normalise();
        }

        /// <summary>
        /// The pages of one branch in one language, falling back page by page to English for anything the
        /// translation leaves blank.
        /// </summary>
        public IReadOnlyList<string> GetDialoguePages(string language, int branch)
        {
            string[] primary = branchOf(language, branch);
            string[] fallback = branchOf("en", branch);

            if (fallback.Length == 0)
                fallback = branchOf(branchesByLanguage.Keys.FirstOrDefault() ?? "en", branch);

            int count = Math.Max(primary.Length, fallback.Length);

            return Enumerable.Range(0, count)
                             .Select(index => index < primary.Length && !string.IsNullOrWhiteSpace(primary[index])
                                 ? primary[index]
                                 : index < fallback.Length ? fallback[index] : string.Empty)
                             .ToArray();
        }

        /// <summary>What is stored for one language and branch, without falling back to another language.</summary>
        public IReadOnlyList<string> GetConfiguredDialoguePages(string language, int branch) => branchOf(language, branch);

        /// <summary>Every branch stored for one language, for saving the entity.</summary>
        public IReadOnlyList<string[]> GetConfiguredBranches(string language) =>
            branchesByLanguage.TryGetValue(language, out List<string[]>? branches) ? branches : Array.Empty<string[]>();

        public IEnumerable<string> DialogueLanguages => branchesByLanguage.Keys;

        private string[] branchOf(string language, int branch) =>
            branchesByLanguage.TryGetValue(language, out List<string[]>? branches) && branch >= 0 && branch < branches.Count
                ? branches[branch]
                : Array.Empty<string>();

        /// <summary>
        /// Replaces the whole conversation in both languages.
        /// </summary>
        public void SetDialogueBranches(IReadOnlyList<string[]> russianBranches, IReadOnlyList<string[]> englishBranches)
        {
            branchesByLanguage["ru"] = branchList(russianBranches);
            branchesByLanguage["en"] = branchList(englishBranches);
            normalise();
            clampPosition();
        }

        /// <summary>Replaces the conversation with a single branch, which is all a character used to have.</summary>
        public void SetDialoguePages(string[] russianPages, string[] englishPages) =>
            SetDialogueBranches(new[] { russianPages }, new[] { englishPages });

        private static List<string[]> branchList(IReadOnlyList<string[]> branches) =>
            branches.Count > 0
                ? branches.Select(pages => pages.Length > 0 ? pages.ToArray() : new[] { string.Empty }).ToList()
                : new List<string[]> { new[] { string.Empty } };

        /// <summary>
        /// Keeps the open branch and page inside a conversation that was just rewritten in the editor.
        /// </summary>
        private void clampPosition()
        {
            ActiveBranch = Math.Clamp(ActiveBranch, 0, BranchCount - 1);
            nextBranch %= BranchCount;
            DialoguePage = Math.Clamp(DialoguePage, 0, Math.Max(0, ActiveDialoguePages.Count - 1));
        }

        /// <summary>
        /// Pads every language to the same shape: the same number of branches, each with the same number
        /// of pages.
        /// </summary>
        private void normalise()
        {
            int branchCount = Math.Max(1, branchesByLanguage.Values.Count == 0 ? 1 : branchesByLanguage.Values.Max(branches => branches.Count));

            foreach (string language in new[] { "ru", "en" })
            {
                if (!branchesByLanguage.TryGetValue(language, out List<string[]>? branches))
                    branchesByLanguage[language] = branches = new List<string[]>();

                while (branches.Count < branchCount)
                    branches.Add(Array.Empty<string>());
            }

            for (int branch = 0; branch < branchCount; branch++)
            {
                int pageCount = Math.Max(1, branchesByLanguage.Values
                                                              .Where(branches => branch < branches.Count)
                                                              .Max(branches => branches[branch].Length));

                foreach (List<string[]> branches in branchesByLanguage.Values)
                {
                    if (branch < branches.Count && branches[branch].Length != pageCount)
                    {
                        branches[branch] = branches[branch]
                                           .Concat(Enumerable.Repeat(string.Empty, Math.Max(0, pageCount - branches[branch].Length)))
                                           .ToArray();
                    }
                }
            }
        }

        /// <summary>The branches of the language the player reads.</summary>
        protected IReadOnlyList<string[]> ActiveLanguageBranches => GetConfiguredBranches(languageProvider());

        protected IReadOnlyList<string> ActiveDialoguePages => GetDialoguePages(languageProvider(), ActiveBranch);

        protected string DialogueLanguage => languageProvider();
    }
}
