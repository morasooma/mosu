using System;
using System.Reflection;
using Moq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BackgroundProcessingNotificationTest
    {
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(5, true)]
        [TestCase(10, true)]
        public void TestSmallQueuesAreVisible(int count, bool expected)
        {
            using var processor = new BackgroundDataStoreProcessor();
            var overlay = new Mock<INotificationOverlay>();
            typeof(BackgroundDataStoreProcessor).GetProperty("notificationOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(processor, overlay.Object);
            var method = typeof(BackgroundDataStoreProcessor).GetMethod("showProgressNotification", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var notification = method.Invoke(processor, new object[] { count, "Calculating", "Calculated", Type.Missing });

            Assert.That(notification is ProgressNotification, Is.EqualTo(expected));
            overlay.Verify(o => o.Post(It.IsAny<Notification>()), Times.Exactly(expected ? 1 : 0));
        }
    }
}
