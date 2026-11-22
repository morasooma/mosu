using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GameplayGlyphPrecacheTest
    {
        [Test]
        public void TestConfiguredFamilyAndCyrillicUseLayoutLookupOrder()
        {
            var lookup = new RecordingLookup();
            var font = new FontUsage("Custom.fnt", weight: "Bold");
            GameplayGlyphPrecache.Prepare(lookup, font, "Яa");
            Assert.That(lookup.Requests, Does.Not.Contain(("Comfortaa Regular.fnt", 'Я')));
            Assert.That(lookup.Requests, Does.Contain((font.FontName, 'Я')));
            Assert.That(lookup.Requests, Does.Contain((font.FontNameNoFamily, 'Я')));
            Assert.That(lookup.Requests, Does.Contain((string.Empty, 'Я')));
            Assert.That(lookup.Requests, Does.Contain((font.FontName, 'a')));
            Assert.That(lookup.Requests, Does.Contain((font.FontNameNoFamily, 'a')));
            Assert.That(lookup.Requests, Does.Contain((string.Empty, 'a')));
        }

        [Test]
        public void TestCancellationBeforeAnyLookup()
        {
            var lookup = new RecordingLookup();
            Assert.Throws<OperationCanceledException>(() => GameplayGlyphPrecache.Prepare(lookup, FontUsage.Default, "test", new CancellationToken(true)));
            Assert.That(lookup.Requests, Is.Empty);
            Assert.That(GameplayGlyphPrecache.UI_CHARACTERS.Length, Is.LessThan(256));
        }

        private sealed class RecordingLookup : ITexturedGlyphLookupStore
        {
            public readonly List<(string?, char)> Requests = new List<(string?, char)>();
            public ITexturedCharacterGlyph? Get(string? fontName, char character)
            {
                Requests.Add((fontName, character));
                return null;
            }
            public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => throw new InvalidOperationException("precache must have a joined loading-thread lifetime");
        }
    }
}
