using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public partial class LocalUserStateFriendsTest
    {
        [Test]
        public void TestFullRefreshCompletesExistingRelation()
        {
            using var storage = new TemporaryNativeStorage($"friends-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            using var api = new FriendsAPI();
            using var state = new LocalUserState(api, config);

            var incomplete = new APIRelation { TargetID = 727, RelationType = RelationType.Friend };
            var full = new APIRelation
            {
                TargetID = 727,
                RelationType = RelationType.Friend,
                TargetUser = new APIUser { Id = 727, Username = "Friend" }
            };

            api.Response = new List<APIRelation> { incomplete };
            state.UpdateFriends();
            api.Flush();
            Assert.That(state.Friends, Has.Count.EqualTo(1));
            Assert.That(state.Friends[0].TargetUser, Is.Null);

            int changes = 0;
            state.Friends.CollectionChanged += (_, _) => changes++;
            api.Response = new List<APIRelation> { full };
            state.UpdateFriends();
            api.Flush();

            Assert.That(state.Friends, Has.Count.EqualTo(1));
            Assert.That(state.Friends[0], Is.SameAs(full));
            Assert.That(changes, Is.GreaterThan(0), "Bound displays must reload the completed profile.");

            api.Response = new List<APIRelation> { incomplete };
            state.UpdateFriends();
            api.Flush();
            Assert.That(state.Friends[0], Is.SameAs(full), "An incomplete response must not discard a known profile.");

            api.Response.Clear();
            state.UpdateFriends();
            api.Flush();
            Assert.That(state.Friends, Is.Empty);
        }

        private partial class FriendsAPI : DummyAPIAccess
        {
            public List<APIRelation> Response = new List<APIRelation>();

            public void Flush() => Scheduler.Update();

            public override void Queue(APIRequest request)
            {
                request.AttachAPI(this);
                Assert.That(request, Is.TypeOf<GetFriendsRequest>());
                ((GetFriendsRequest)request).TriggerSuccess(Response);
            }
        }
    }
}
