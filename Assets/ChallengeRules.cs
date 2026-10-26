// 縛り条件。「現時点で基準を外れているか」を、記録(RunStats)だけから判定する
public abstract class ChallengeRule
{
    public abstract string Title { get; }                // 例: ナイフは3本以内
    public abstract string Progress(RunStats s);         // 例: 2/3
    public abstract bool IsViolated(RunStats s);         // 基準を外れたら true

    // 編集中にも、現在の値を出すか(配置数だけ true)
    public virtual bool ShowProgressInEdit => false;
}

// ---- 星1(全お題で共通): 死なずに倒す ----
// 「ボスを倒す」の部分は、撃破の処理で判定する。ここでは「死なない」を見る
public class SurviveRule : ChallengeRule
{
    public override string Title => "死なずにボスを倒す";
    public override string Progress(RunStats s) => s.Deaths == 0 ? "生存中" : "死亡";
    public override bool IsViolated(RunStats s) => s.Deaths > 0;
}

// ---- 星2(全お題で共通) ----
public class RemainingHpRule : ChallengeRule
{
    private readonly float minRatio;
    public RemainingHpRule(float minRatio = 0.5f) { this.minRatio = minRatio; }

    public override string Title => $"残りHPが{minRatio * 100f:F0}%以上";
    public override string Progress(RunStats s) => $"HP {s.RemainingHp}/{s.MaxHp}";
    public override bool IsViolated(RunStats s) => s.MaxHp > 0 && s.RemainingHp < s.MaxHp * minRatio;
}

// ---- 星3(5種類から、お題ごとに1つ) ----
public class KnifeLimitRule : ChallengeRule
{
    private readonly int max;
    public KnifeLimitRule(int max) { this.max = max; }

    public override string Title => $"ナイフは{max}本以内";
    public override string Progress(RunStats s) => $"{s.KnifeThrown}/{max}";
    public override bool IsViolated(RunStats s) => s.KnifeThrown > max;
}

public class NoLandingRule : ChallengeRule
{
    public override string Title => "床に着地しない";
    public override string Progress(RunStats s) => $"着地 {s.Landings}回";
    public override bool IsViolated(RunStats s) => s.Landings > 0;
}

public class NoMeleeRule : ChallengeRule
{
    public override string Title => "近接攻撃を使わない";
    public override string Progress(RunStats s) => $"近接 {s.MeleeCount}回";
    public override bool IsViolated(RunStats s) => s.MeleeCount > 0;
}

public class TimeLimitRule : ChallengeRule
{
    private readonly float seconds;
    public TimeLimitRule(float seconds) { this.seconds = seconds; }

    public override string Title => $"{seconds:F0}秒以内に倒す";
    public override string Progress(RunStats s) => $"{s.ElapsedTime:F0}/{seconds:F0}秒";
    public override bool IsViolated(RunStats s) => s.ElapsedTime > seconds;
}

public class PlacementCountRule : ChallengeRule
{
    private readonly int max;
    public PlacementCountRule(int max) { this.max = max; }

    public override string Title => $"追加で置く物は合計{max}個以内";
    public override string Progress(RunStats s) => $"{s.PlacedCount}/{max}";
    public override bool IsViolated(RunStats s) => s.PlacedCount > max;
    public override bool ShowProgressInEdit => true;
}