using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Hand;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    // Uses the same cards and hand layout as the participant UI, without input or outgoing hand replays.
    public partial class RankedPlaySpectatorCardsScreen : RankedPlaySubScreen
    {
        public override LocalisableString StageHeading => global::osu.Game.Localisation.ClientTranslationStrings.J4918;

        [Resolved]
        private RankedPlayMatchInfo matchInfo { get; set; } = null!;

        private SpectatorHand playerHand = null!;
        private SpectatorHand opponentHand = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            CenterColumn.Children = new Drawable[]
            {
                opponentHand = new SpectatorHand(true)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.5f,
                },
                playerHand = new SpectatorHand(false)
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.5f,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            syncHands();
            matchInfo.PlayerCardAdded += onCardChanged;
            matchInfo.PlayerCardRemoved += onCardChanged;
            matchInfo.OpponentCardAdded += onCardChanged;
            matchInfo.OpponentCardRemoved += onCardChanged;
        }

        private void onCardChanged(RankedPlayCardWithPlaylistItem _) => syncHands();

        private void syncHands()
        {
            sync(playerHand, matchInfo.PlayerCards);
            sync(opponentHand, matchInfo.OpponentCards);
            StageCaption = matchInfo.Stage.Value is RankedPlayStage.CardDiscard or RankedPlayStage.FinishCardDiscard
                ? global::osu.Game.Localisation.ClientTranslationStrings.J4919
                : matchInfo.IsOwnTurn ? global::osu.Game.Localisation.ClientTranslationStrings.J4920 : global::osu.Game.Localisation.ClientTranslationStrings.J4921;
        }

        private static void sync(SpectatorHand hand, IReadOnlyList<RankedPlayCardWithPlaylistItem> cards)
        {
            foreach (var existing in hand.Cards.ToArray())
            {
                if (!cards.Contains(existing.Item))
                    hand.RemoveCard(existing.Item, out _, out _);
            }
            foreach (var card in cards)
            {
                if (hand.Cards.All(existing => !existing.Item.Equals(card)))
                    hand.AddCard(card);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            matchInfo.PlayerCardAdded -= onCardChanged;
            matchInfo.PlayerCardRemoved -= onCardChanged;
            matchInfo.OpponentCardAdded -= onCardChanged;
            matchInfo.OpponentCardRemoved -= onCardChanged;
            base.Dispose(isDisposing);
        }

        private partial class SpectatorHand : HandOfCards
        {
            private readonly bool flipped;
            protected override bool Flipped => flipped;
            public SpectatorHand(bool flipped) => this.flipped = flipped;
        }
    }
}
