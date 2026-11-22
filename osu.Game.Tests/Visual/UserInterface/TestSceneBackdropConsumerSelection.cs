using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneBackdropConsumerSelection : OsuTestScene
    {
        private BackdropBlurContainer source = null!;

        private readonly Consumer[] consumers = { new Consumer(), new Consumer(), new Consumer(), new Consumer() };
        private readonly IDisposable?[] leases = new IDisposable?[4];

        [SetUpSteps]
        public void SetUpSteps() => AddStep("register inactive consumers", () =>
        {
            releaseAll();
            // Reusing a HashSet after removing its leases can reverse its slot order.
            // A fresh source gives this test a known inactive prefix to detect scans.
            Child = source = new BackdropBlurContainer
            {
                SceneContent = new Box { RelativeSizeAxes = Axes.Both },
            };
            for (int i = 0; i < consumers.Length; i++)
            {
                Consumer consumer = consumers[i];
                consumer.Calls = 0;
                consumer.Active = false;
                leases[i] = source.Acquire(consumer.Check);
            }
        });

        [TearDownSteps]
        public void TearDownSteps() => AddStep("release consumers", releaseAll);

        [Test]
        public void TestStableActiveConsumerDoesNotRescanEarlierConsumers()
        {
            int earlierCalls = 0;
            int activeCalls = 0;
            AddStep("activate last consumer", () => consumers[3].Active = true);
            AddUntilStep("effect active", () => source.EffectActive);
            AddWaitStep("settle", 3);
            AddStep("record checks", () =>
            {
                earlierCalls = consumers[0].Calls + consumers[1].Calls + consumers[2].Calls;
                activeCalls = consumers[3].Calls;
            });
            AddAssert("inactive prefix was exercised", () => consumers[0].Calls > 0 && consumers[1].Calls > 0 && consumers[2].Calls > 0);
            AddWaitStep("stable effect frames", 10);
            AddAssert("earlier inactive consumers not rescanned", () => consumers[0].Calls + consumers[1].Calls + consumers[2].Calls == earlierCalls);
            AddAssert("active consumer is still checked every frame", () => consumers[3].Calls > activeCalls);
        }

        [Test]
        public void TestInactiveOrReleasedConsumerFallsBackToAnotherConsumer()
        {
            int releasedCalls = 0;
            AddStep("activate third consumer", () => consumers[2].Active = true);
            AddUntilStep("effect active", () => source.EffectActive);
            AddStep("move activity to fourth consumer", () =>
            {
                consumers[2].Active = false;
                consumers[3].Active = true;
            });
            AddUntilStep("fourth consumer checked", () => consumers[3].Calls > 0 && source.EffectActive);
            AddStep("release active consumer and reactivate first", () =>
            {
                leases[3]!.Dispose();
                releasedCalls = consumers[3].Calls;
                consumers[0].Active = true;
            });
            AddWaitStep("fallback frames", 5);
            AddAssert("effect remains active", () => source.EffectActive);
            AddAssert("released consumer never queried again", () => consumers[3].Calls == releasedCalls);
            AddStep("deactivate last active consumer", () => consumers[0].Active = false);
            AddUntilStep("effect and capture released", () => !source.EffectActive && !source.BufferingActive);
            AddStep("reactivate earlier consumer", () => consumers[1].Active = true);
            AddUntilStep("inactive source finds consumer again", () => source.EffectActive);
        }

        [Test]
        public void TestEffectGateDoesNotQueryCachedConsumer()
        {
            int calls = 0;
            AddStep("activate third consumer", () => consumers[2].Active = true);
            AddUntilStep("effect active", () => source.EffectActive);
            AddStep("forbid effects", () => source.AllowEffects = () => false);
            AddUntilStep("effect disabled", () => !source.EffectActive);
            AddStep("record checks", () => calls = consumers[2].Calls);
            AddWaitStep("forbidden frames", 10);
            AddAssert("cached consumer not queried", () => consumers[2].Calls == calls);
            AddStep("allow effects again", () => source.AllowEffects = () => true);
            AddUntilStep("effect reactivated", () => source.EffectActive && consumers[2].Calls > calls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestConsumerReleasedDuringItsCheckIsNotRetained(bool previouslyCached)
        {
            int calls = 0;
            int releasedCalls = 0;
            bool releaseDuringCheck = !previouslyCached;
            AddStep("register self-releasing consumer", () =>
            {
                leases[3]!.Dispose();
                leases[3] = source.Acquire(() =>
                {
                    calls++;
                    if (releaseDuringCheck && releasedCalls == 0)
                    {
                        leases[3]!.Dispose();
                        releasedCalls = calls;
                    }
                    return true;
                });
            });
            if (previouslyCached)
            {
                AddUntilStep("consumer active", () => source.EffectActive && calls > 0);
                AddStep("release during next check", () => releaseDuringCheck = true);
            }
            AddUntilStep("lease released", () => releasedCalls > 0);
            AddWaitStep("frames after release", 5);
            AddAssert("released consumer never queried again", () => calls == releasedCalls);
            AddAssert("effect and capture released", () => !source.EffectActive && !source.BufferingActive);
        }

        private void releaseAll()
        {
            foreach (IDisposable? lease in leases)
                lease?.Dispose();
            Array.Clear(leases);
        }

        private sealed class Consumer
        {
            public int Calls;
            public bool Active;

            public bool Check()
            {
                Calls++;
                return Active;
            }
        }
    }
}
