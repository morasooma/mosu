// Copyright (c) Morasooma contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneSupporterQuestsOverlay : OsuTestScene
    {
        // Зависимость кнопки внешней ссылки; в тесте переход не выполняется.
        [Cached]
        private readonly OsuGame game = new OsuGame();

        private SupporterQuestsOverlay overlay = null!;
        private List<APISupporterQuest> quests = null!;
        private bool failRequest;
        private int completedCount;
        private ThemeMode previousTheme;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("создать оверлей", () =>
            {
                previousTheme = OverlayColourProvider.CurrentTheme.Value;
                failRequest = false;
                completedCount = -1;
                quests = Enumerable.Range(1, 8).Select(i => new APISupporterQuest
                {
                    Id = 100 + i,
                    Name = "Задание с длинным названием, которое должно переноситься внутри карточки",
                    Description = "Выполняйте задания и вносите вклад в сообщество Morasooma, чтобы получить месяц саппортера.",
                    CurrentValue = i == 2 ? 125.5f : 0,
                    TargetValue = 200,
                    Completed = i == 1,
                }).ToList();

                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetSupporterQuestsRequest supporterRequest)
                        return false;

                    if (failRequest)
                        supporterRequest.Fail(new InvalidOperationException("Тестовая ошибка подключения"));
                    else
                        supporterRequest.TriggerSuccess(quests);

                    return true;
                };

                Child = overlay = new SupporterQuestsOverlay
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1100,
                };
                overlay.QuestsLoaded += count => completedCount = count;
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("восстановить тему", () => OverlayColourProvider.CurrentTheme.Value = previousTheme);

        [Test]
        public void TestLayoutAndLiveThemes()
        {
            AddStep("открыть", () => overlay.Show());
            AddUntilStep("задания загружены", () => completedCount == 1);
            AddAssert("показаны выполненное, текущее и два следующих", () => cards.Count() == 4);
            AddAssert("оставшиеся задания свёрнуты", () => overlay.ChildrenOfType<SupporterQuestPlaceholderCard>().Count() == 1);

            foreach (float width in new[] { 1100f, 700f, 420f, 1100f })
            {
                AddStep($"ширина {width}", () => overlay.Width = width);
                AddWaitStep("дождаться раскладки", 3);
                AddAssert("карточки помещаются по ширине", () => cards.All(card =>
                    card.DrawWidth > 0 && card.ScreenSpaceDrawQuad.TopLeft.X >= overlay.ScreenSpaceDrawQuad.TopLeft.X
                                       && card.ScreenSpaceDrawQuad.TopRight.X <= overlay.ScreenSpaceDrawQuad.TopRight.X + 1));
                AddAssert("текст помещается в карточки", () => cards.All(card => card.ChildrenOfType<OsuTextFlowContainer>()
                    .All(text => text.ScreenSpaceDrawQuad.TopRight.X <= card.ScreenSpaceDrawQuad.TopRight.X + 1)));
            }

            foreach (var theme in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.Default })
            {
                AddStep($"тема {theme}", () => OverlayColourProvider.CurrentTheme.Value = theme);
                AddAssert("текст использует текущую палитру", () => cards.All(card =>
                    card.ChildrenOfType<OsuTextFlowContainer>().First().Colour.AverageColour
                    == (Colour4)new OverlayColourProvider(OverlayColourScheme.Purple).Content1));
            }
        }

        [Test]
        public void TestEmptyResponseAfterProgress()
        {
            AddStep("открыть", () => overlay.Show());
            AddUntilStep("получен прогресс", () => completedCount == 1);
            AddStep("закрыть и очистить ответ", () =>
            {
                overlay.Hide();
                quests.Clear();
            });
            AddStep("открыть снова", () => overlay.Show());
            AddUntilStep("прогресс сброшен", () => completedCount == 0);
            AddAssert("старых карточек нет", () => !cards.Any());
            AddAssert("старый процент не показан", () => !overlay.ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "13%"));
            AddAssert("есть повторная загрузка", () => retryButton != null);
        }

        [Test]
        public void TestFailureAndRetry()
        {
            AddStep("ошибка загрузки", () =>
            {
                failRequest = true;
                overlay.Show();
            });
            AddUntilStep("показана повторная загрузка", () => retryButton != null);
            AddStep("включить светлую тему", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);
            AddAssert("кнопка обновилась вместе с темой", () => retryButton!.BackgroundColour == new OverlayColourProvider(OverlayColourScheme.Purple).Colour3);
            AddStep("повторить успешно", () =>
            {
                failRequest = false;
                retryButton!.TriggerClick();
            });
            AddUntilStep("задания загружены", () => completedCount == 1 && cards.Count() == 4);
        }

        [Test]
        public void TestAllCompleted()
        {
            AddStep("выполнить все задания", () =>
            {
                quests.ForEach(quest => quest.Completed = true);
                overlay.Show();
            });
            AddUntilStep("все задания показаны", () => cards.Count() == quests.Count);
            AddAssert("прогресс 100%", () => overlay.ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "100%"));
            AddAssert("скрытых заданий нет", () => !overlay.ChildrenOfType<SupporterQuestPlaceholderCard>().Any());
        }

        private IEnumerable<SupporterQuestCard> cards => overlay.ChildrenOfType<SupporterQuestCard>();

        private RoundedButton? retryButton => overlay.ChildrenOfType<RoundedButton>()
                                                     .FirstOrDefault(button => button.Text == SupporterQuestsStrings.Retry);

        protected override void Dispose(bool isDisposing)
        {
            game.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
