using System.Linq;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Online.Legacy
{
    internal static class StableScoreStatistics
    {
        public static double CalculateAccuracy(ScoreInfo score)
        {
            int count300 = score.GetCount300() ?? 0;
            int count100 = score.GetCount100() ?? 0;
            int count50 = score.GetCount50() ?? 0;
            int countMiss = score.GetCountMiss() ?? 0;
            int countGeki = score.GetCountGeki() ?? 0;
            int countKatu = score.GetCountKatu() ?? 0;
            double numerator;
            int totalHits;
            switch (score.Ruleset.OnlineID)
            {
                case 1:
                    totalHits = count300 + count100 + countMiss;
                    numerator = count300 + count100 * 0.5;
                    break;

                case 2:
                    totalHits = count300 + count100 + count50 + countKatu + countMiss;
                    numerator = count300 + count100 + count50;
                    break;

                case 3:
                    totalHits = count300 + count100 + count50 + countGeki + countKatu + countMiss;
                    numerator = ((count300 + countGeki) * 300d + countKatu * 200d + count100 * 100d + count50 * 50d) / 300d;
                    break;

                default:
                    totalHits = count300 + count100 + count50 + countMiss;
                    numerator = (count300 * 300d + count100 * 100d + count50 * 50d) / 300d;
                    break;
            }

            return totalHits == 0 ? 0 : numerator / totalHits;
        }

        public static ScoreRank CalculateRank(ScoreInfo score)
        {
            if (!score.Passed)
                return ScoreRank.F;

            int count300 = score.GetCount300() ?? 0;
            int count50 = score.GetCount50() ?? 0;
            int countMiss = score.GetCountMiss() ?? 0;
            int totalHits = count300 + (score.GetCount100() ?? 0) + count50 + countMiss;
            if (score.Ruleset.OnlineID == 3)
                totalHits += (score.GetCountGeki() ?? 0) + (score.GetCountKatu() ?? 0);
            if (score.Ruleset.OnlineID == 2)
                totalHits += score.GetCountKatu() ?? 0;

            if (totalHits == 0)
                return ScoreRank.D;

            double ratio300 = count300 / (double)totalHits;
            double ratio50 = count50 / (double)totalHits;
            ScoreRank rank;

            if (score.Ruleset.OnlineID is 2 or 3)
            {
                double accuracy = CalculateAccuracy(score);
                double sThreshold = score.Ruleset.OnlineID == 2 ? 0.98 : 0.95;
                double aThreshold = score.Ruleset.OnlineID == 2 ? 0.94 : 0.90;
                double bThreshold = score.Ruleset.OnlineID == 2 ? 0.90 : 0.80;
                double cThreshold = score.Ruleset.OnlineID == 2 ? 0.85 : 0.70;
                rank = accuracy == 1 ? ScoreRank.X
                    : accuracy > sThreshold ? ScoreRank.S
                    : accuracy > aThreshold ? ScoreRank.A
                    : accuracy > bThreshold ? ScoreRank.B
                    : accuracy > cThreshold ? ScoreRank.C : ScoreRank.D;
            }
            else if (count300 == totalHits)
                rank = ScoreRank.X;
            else if (ratio300 > 0.9 && ratio50 <= 0.01 && countMiss == 0)
                rank = ScoreRank.S;
            else if ((ratio300 > 0.8 && countMiss == 0) || ratio300 > 0.9)
                rank = ScoreRank.A;
            else if ((ratio300 > 0.7 && countMiss == 0) || ratio300 > 0.8)
                rank = ScoreRank.B;
            else if (ratio300 > 0.6)
                rank = ScoreRank.C;
            else
                rank = ScoreRank.D;

            bool silver = score.Mods.Any(mod => mod is Rulesets.Mods.ModHidden or Rulesets.Mods.ModFlashlight
                || (score.Ruleset.OnlineID == 3 && mod.Acronym == "FI"));
            if (silver && rank == ScoreRank.X)
                return ScoreRank.XH;
            if (silver && rank == ScoreRank.S)
                return ScoreRank.SH;

            return rank;
        }
    }
}
