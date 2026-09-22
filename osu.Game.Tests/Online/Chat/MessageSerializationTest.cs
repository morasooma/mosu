// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Online.Chat;

namespace osu.Game.Tests.Online.Chat
{
    [TestFixture]
    public class MessageSerializationTest
    {
        [Test]
        public void TestSenderBeforeSenderId()
        {
            const string json = @"{
                ""sender"": {
                    ""id"": 123,
                    ""username"": ""test_user""
                },
                ""sender_id"": 123,
                ""content"": ""hello""
            }";

            var message = JsonConvert.DeserializeObject<Message>(json);

            ClassicAssert.NotNull(message);
            var sender = message!.Sender;
            ClassicAssert.NotNull(sender);
            ClassicAssert.AreEqual(123, message.SenderId);
            ClassicAssert.AreEqual(123, sender!.Id);
            ClassicAssert.AreEqual("test_user", sender.Username);
        }

        [Test]
        public void TestSenderIdBeforeSender()
        {
            const string json = @"{
                ""sender_id"": 123,
                ""sender"": {
                    ""id"": 123,
                    ""username"": ""test_user""
                },
                ""content"": ""hello""
            }";

            var message = JsonConvert.DeserializeObject<Message>(json);

            ClassicAssert.NotNull(message);
            var sender = message!.Sender;
            ClassicAssert.NotNull(sender);
            ClassicAssert.AreEqual(123, message.SenderId);
            ClassicAssert.AreEqual(123, sender!.Id);
            ClassicAssert.AreEqual("test_user", sender.Username);
        }
    }
}
