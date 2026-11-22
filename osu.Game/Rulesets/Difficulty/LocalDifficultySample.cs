namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>A local skill index, not a local star rating. Time is in source beatmap milliseconds.</summary>
    public readonly record struct LocalDifficultySample(double Time, double Difficulty);
}
