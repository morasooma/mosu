using System;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EnhancedSettingsAnimationsTest
    {
        [Test]
        public void TestEnabledByDefaultAndOptOutIsSavedIndependentlyFromInterfaceMode()
        {
            using var storage = new TemporaryNativeStorage("enhanced-animations-" + Guid.NewGuid());
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.ForkEnhancedAnimations), Is.True);
                config.SetValue(OsuSetting.ForkEnhancedAnimations, false);
                config.SetValue(OsuSetting.ForkInterfaceAnimations, InterfaceAnimationMode.Minimal);
                config.Save();
            }

            using var reopened = new OsuConfigManager(storage);
            Assert.That(reopened.Get<bool>(OsuSetting.ForkEnhancedAnimations), Is.False);
            Assert.That(reopened.Get<InterfaceAnimationMode>(OsuSetting.ForkInterfaceAnimations), Is.EqualTo(InterfaceAnimationMode.Minimal));
        }

        [TestCase("en", "Enhanced animations", "Smoother, shorter menu and panel transitions, and expanding settings rows. Requires full interface animations. Gameplay is not affected.")]
        [TestCase("ru", "Улучшенные анимации", "Более мягкие и короткие переходы меню и панелей и раскрытие строк настроек. Требуются полные анимации интерфейса. На игровой процесс не влияет.")]
        [TestCase("es", "Animaciones mejoradas", "Transiciones más suaves y breves en menús y paneles y expansión de las filas de ajustes. Requiere las animaciones completas de la interfaz. No afecta al juego.")]
        [TestCase("ko", "향상된 애니메이션", "메뉴와 패널 전환을 더 부드럽고 짧게 하고, 설정 항목이 펼쳐지게 합니다. 인터페이스 애니메이션을 전체로 설정해야 합니다. 게임플레이에는 영향을 주지 않습니다.")]
        [TestCase("zh", "增强动画", "菜单和面板的过渡更柔和、更短，设置行会展开。需要将界面动画设为完整。不影响游戏过程。")]
        public void TestRuntimeLocalisation(string culture, string caption, string hint)
        {
            using var storage = new TemporaryNativeStorage("enhanced-animations-locale-" + Guid.NewGuid());
            using var config = new FrameworkConfigManager(storage);
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(culture);
            manager.AddLanguage(culture, store);
            config.SetValue(FrameworkSetting.Locale, culture);
            Assert.That(manager.GetLocalisedString(InterfaceAnimationStrings.EnhancedCaption), Is.EqualTo(caption));
            Assert.That(manager.GetLocalisedString(InterfaceAnimationStrings.EnhancedHint), Is.EqualTo(hint));

            foreach (var property in typeof(InterfaceAnimationStrings).GetProperties())
            {
                var text = (LocalisableString)property.GetValue(null)!;
                string resolved = manager.GetLocalisedString(text);
                Assert.That(resolved, Is.Not.Empty, property.Name);
                if (culture != "en")
                {
                    string key = "osu.Game.Localisation.InterfaceAnimation:" + (property.Name switch
                    {
                        nameof(InterfaceAnimationStrings.EnhancedCaption) => "enhanced_caption",
                        nameof(InterfaceAnimationStrings.EnhancedHint) => "enhanced_hint",
                        _ => property.Name.ToLowerInvariant(),
                    });
                    string? resource = store.Get(key);
                    Assert.That(resource, Is.Not.Null.And.Not.Empty, key);
                    Assert.That(resolved, Is.EqualTo(resource), key);
                }
            }
        }
    }
}
