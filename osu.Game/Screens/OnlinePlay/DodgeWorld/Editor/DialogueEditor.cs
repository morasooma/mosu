// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// Edits an NPC's conversation: its branches, the pages of each branch, and both languages side by
    /// side.
    /// </summary>
    /// <remarks>
    /// A branch is one conversation, read through in order; the next conversation takes the next branch and
    /// comes back round to the first. So one branch is a character who says the same thing every time,
    /// several branches is a character with something else to say.
    /// <para>
    /// Everything is kept as a draft and only written back on apply, so that moving between pages cannot
    /// half-save a line. The two languages are edited together because they are stored page for page: a
    /// page present in one and missing in the other would desynchronise them.
    /// </para>
    /// </remarks>
    internal partial class DialogueEditor : CompositeDrawable
    {
        private const int page_length_limit = 500;

        /// <summary>How many branches one character may have. The API refuses more.</summary>
        public const int BRANCH_LIMIT = 8;

        private readonly Action<string> reportStatus;

        private readonly OsuSpriteText header;
        private readonly OsuSpriteText pageIndicator;
        private readonly OsuTextBox russian;
        private readonly OsuTextBox english;

        /// <summary>The draft, as branches of pages, one list per language.</summary>
        private readonly List<List<string>> russianDraft = new List<List<string>>();

        private readonly List<List<string>> englishDraft = new List<List<string>>();

        private InteractiveNpc? target;
        private int branch;
        private int page;

        public DialogueEditor(OsuColour colours, Action<string> reportStatus)
        {
            this.reportStatus = reportStatus;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    header = new OsuSpriteText
                    {
                        Text = "ВЫБЕРИ NPC",
                        Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                        Colour = colours.Orange1,
                    },
                    pageIndicator = new OsuSpriteText
                    {
                        Text = "ВЕТКА — / — • РЕПЛИКА — / —",
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        Colour = colours.GrayA,
                    },
                    WrappedText.Paragraph(
                        "Ветка — это один разговор: реплики читаются по порядку. Следующий разговор берёт следующую"
                        + " ветку и по кругу возвращается к первой. Одна ветка — персонаж говорит одно и то же;"
                        + " несколько — каждый раз другое.", colours.GrayA, 12),
                    new OsuSpriteText
                    {
                        Text = "РУССКИЙ",
                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                        Colour = colours.Pink1,
                    },
                    russian = new OsuTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 32,
                        LengthLimit = page_length_limit,
                        CommitOnFocusLost = true,
                    },
                    new OsuSpriteText
                    {
                        Text = "ENGLISH",
                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                        Colour = colours.Pink1,
                    },
                    english = new OsuTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 32,
                        LengthLimit = page_length_limit,
                        CommitOnFocusLost = true,
                    },
                    button("Предыдущая реплика", () => movePage(-1)),
                    button("Следующая реплика", () => movePage(1)),
                    button("Добавить реплику после текущей", addPage),
                    button("Удалить текущую реплику", deletePage),
                    button("Предыдущая ветка", () => moveBranch(-1)),
                    button("Следующая ветка", () => moveBranch(1)),
                    button("Добавить ветку после текущей", addBranch),
                    button("Удалить текущую ветку", deleteBranch),
                    button("Применить диалог к NPC", Apply),
                },
            };

            russian.OnCommit += (_, _) => captureCurrentPage();
            english.OnCommit += (_, _) => captureCurrentPage();
        }

        private static RoundedButton button(string text, Action action) => new RoundedButton
        {
            RelativeSizeAxes = Axes.X,
            Height = 32,
            Text = text,
            Action = action,
        };

        public void SetTarget(InteractiveNpc? npc)
        {
            target = npc;
            branch = 0;
            page = 0;
            russianDraft.Clear();
            englishDraft.Clear();

            if (npc == null)
            {
                header.Text = "ВЫБЕРИ NPC";
                russian.Text = string.Empty;
                english.Text = string.Empty;
                pageIndicator.Text = "ВЕТКА — / — • РЕПЛИКА — / —";
                return;
            }

            header.Text = npc.DisplayName.ToUpperInvariant();
            russianDraft.AddRange(npc.GetConfiguredBranches("ru").Select(pages => pages.ToList()));
            englishDraft.AddRange(npc.GetConfiguredBranches("en").Select(pages => pages.ToList()));

            normalise();
            showPage();
        }

        /// <summary>
        /// Writes the draft back onto the NPC.
        /// </summary>
        public void Apply()
        {
            if (target == null)
            {
                reportStatus("Сначала выбери NPC.");
                return;
            }

            captureCurrentPage();
            target.SetDialogueBranches(
                russianDraft.Select(pages => pages.ToArray()).ToArray(),
                englishDraft.Select(pages => pages.ToArray()).ToArray());

            reportStatus(russianDraft.Count == 1
                ? $"Диалог применён: {russianDraft[0].Count} реплик, одна ветка — говорит одно и то же."
                : $"Диалог применён: {russianDraft.Count} ветки по кругу, реплик всего {russianDraft.Sum(pages => pages.Count)}.");
        }

        private void captureCurrentPage()
        {
            if (target == null || branch < 0 || branch >= russianDraft.Count)
                return;

            if (page < 0 || page >= russianDraft[branch].Count)
                return;

            russianDraft[branch][page] = russian.Current.Value;
            englishDraft[branch][page] = english.Current.Value;
        }

        private void movePage(int direction)
        {
            if (target == null)
                return;

            captureCurrentPage();
            page = Math.Clamp(page + direction, 0, Math.Max(0, russianDraft[branch].Count - 1));
            showPage();
        }

        private void moveBranch(int direction)
        {
            if (target == null)
                return;

            captureCurrentPage();
            branch = Math.Clamp(branch + direction, 0, Math.Max(0, russianDraft.Count - 1));
            page = 0;
            showPage();
        }

        private void addPage()
        {
            if (target == null)
            {
                reportStatus("Сначала выбери NPC.");
                return;
            }

            captureCurrentPage();
            page = Math.Min(page + 1, russianDraft[branch].Count);
            russianDraft[branch].Insert(page, string.Empty);
            englishDraft[branch].Insert(page, string.Empty);
            showPage();
        }

        private void deletePage()
        {
            if (target == null || russianDraft[branch].Count <= 1)
            {
                reportStatus("Нельзя удалить единственную реплику ветки. Удали саму ветку.");
                return;
            }

            russianDraft[branch].RemoveAt(page);
            englishDraft[branch].RemoveAt(page);
            page = Math.Clamp(page, 0, russianDraft[branch].Count - 1);
            showPage();
        }

        private void addBranch()
        {
            if (target == null)
            {
                reportStatus("Сначала выбери NPC.");
                return;
            }

            if (russianDraft.Count >= BRANCH_LIMIT)
            {
                reportStatus($"Больше {BRANCH_LIMIT} веток у одного NPC быть не может.");
                return;
            }

            captureCurrentPage();
            branch = Math.Min(branch + 1, russianDraft.Count);
            russianDraft.Insert(branch, new List<string> { string.Empty });
            englishDraft.Insert(branch, new List<string> { string.Empty });
            page = 0;
            showPage();
            reportStatus($"Ветка {branch + 1} добавлена. Не забудь «Применить диалог к NPC».");
        }

        private void deleteBranch()
        {
            if (target == null || russianDraft.Count <= 1)
            {
                reportStatus("Нельзя удалить единственную ветку.");
                return;
            }

            russianDraft.RemoveAt(branch);
            englishDraft.RemoveAt(branch);
            branch = Math.Clamp(branch, 0, russianDraft.Count - 1);
            page = 0;
            showPage();
        }

        /// <summary>
        /// Pads the draft so that both languages have the same branches and every branch the same pages.
        /// </summary>
        private void normalise()
        {
            int branchCount = Math.Max(1, Math.Max(russianDraft.Count, englishDraft.Count));

            while (russianDraft.Count < branchCount)
                russianDraft.Add(new List<string>());

            while (englishDraft.Count < branchCount)
                englishDraft.Add(new List<string>());

            for (int index = 0; index < branchCount; index++)
            {
                int pageCount = Math.Max(1, Math.Max(russianDraft[index].Count, englishDraft[index].Count));

                while (russianDraft[index].Count < pageCount)
                    russianDraft[index].Add(string.Empty);

                while (englishDraft[index].Count < pageCount)
                    englishDraft[index].Add(string.Empty);
            }
        }

        private void showPage()
        {
            normalise();
            branch = Math.Clamp(branch, 0, russianDraft.Count - 1);
            page = Math.Clamp(page, 0, russianDraft[branch].Count - 1);

            russian.Text = russianDraft[branch][page];
            english.Text = englishDraft[branch][page];
            pageIndicator.Text = $"ВЕТКА {branch + 1} / {russianDraft.Count} • РЕПЛИКА {page + 1} / {russianDraft[branch].Count}";
        }

        /// <summary>
        /// The draft as currently edited, for tests.
        /// </summary>
        internal IReadOnlyList<IReadOnlyList<string>> RussianDraft => russianDraft.Select(pages => (IReadOnlyList<string>)pages.ToArray()).ToArray();

        internal IReadOnlyList<IReadOnlyList<string>> EnglishDraft => englishDraft.Select(pages => (IReadOnlyList<string>)pages.ToArray()).ToArray();

        internal int BranchForTesting => branch;

        internal void AddBranchForTesting() => addBranch();

        internal void MoveBranchForTesting(int direction) => moveBranch(direction);

        internal void SetLineForTesting(string ru, string en)
        {
            russian.Text = ru;
            english.Text = en;
            captureCurrentPage();
        }
    }
}
