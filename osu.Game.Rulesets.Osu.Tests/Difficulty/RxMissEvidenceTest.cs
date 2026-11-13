// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;
[TestFixture]
public class RxMissEvidenceTest
{
    [Test]
    public void MissingOrInconsistentEvidenceKeepsFlatPenalty()
    {
        var map = makeMap(); var evidence = new RxMissEvidence(map, new Mod[] { new OsuModRelax() });
        Assert.That(evidence.MissLoad(Array.Empty<HitEvent>(), 1), Is.Null);
        var missing = new HitEvent(0,1,HitResult.Miss,map.HitObjects[5],null,null);
        Assert.That(evidence.MissLoad(new[] { missing },1), Is.Null);
        var hit = new HitEvent(0,1,HitResult.Miss,map.HitObjects[5],null,new Vector2(1000,1000));
        Assert.That(evidence.MissLoad(new[] { hit },2), Is.Null);
        Assert.That(evidence.MissLoad(new[] { hit,hit },2), Is.Null);
    }
    [Test]
    public void CloserConfirmedMissCostsLessWithoutChangingActualCount()
    {
        var map=makeMap();var evidence=new RxMissEvidence(map,new Mod[]{new OsuModRelax()});var obj=map.HitObjects[5];
        var near=new HitEvent(0,1,HitResult.Miss,obj,null,obj.StackedPosition+new Vector2((float)obj.Radius+1,0));
        var far=new HitEvent(0,1,HitResult.Miss,obj,null,obj.StackedPosition+new Vector2((float)obj.Radius*3,0));
        double? a=evidence.MissLoad(new[]{near},1),b=evidence.MissLoad(new[]{far},1);
        Assert.That(a,Is.Not.Null);Assert.That(b,Is.GreaterThan(a));Assert.That(b,Is.InRange(0d,1d));
    }
    [Test]
    public void EquivalentGameplayInstancesCanSupplyEvidence()
    {
        var source=makeMap();var gameplay=makeMap();
        var evidence=new RxMissEvidence(source,new Mod[]{new OsuModRelax()});var obj=gameplay.HitObjects[5];
        var hit=new HitEvent(0,1,HitResult.Miss,obj,null,obj.StackedPosition+new Vector2((float)obj.Radius+1,0));
        Assert.That(evidence.MissLoad(new[]{hit},1),Is.Not.Null);
        obj.StartTime+=1;
        Assert.That(evidence.MissLoad(new[]{hit},1),Is.Null);
    }
    private static Beatmap<OsuHitObject> makeMap()
    {
        var map=new Beatmap<OsuHitObject>{Difficulty=new BeatmapDifficulty{CircleSize=4,OverallDifficulty=9,ApproachRate=9}};
        map.ControlPointInfo.Add(0,new TimingControlPoint{BeatLength=300});
        for(int i=0;i<100;i++){var n=new HitCircle{StartTime=1000+i*100,Position=new Vector2(i%2==0?100:400,192)};n.ApplyDefaults(map.ControlPointInfo,map.Difficulty);map.HitObjects.Add(n);}
        return map;
    }
}
