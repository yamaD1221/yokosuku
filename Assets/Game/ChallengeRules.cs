using UnityEngine;

// 縛り条件の基本クラス
// 現在のプレイ記録（RunStats）をもとに、条件を達成できているか判定する
public abstract class ChallengeRule
{
    // 縛り条件の名前を取得する
    // 例：「ナイフは3本以内」
    public abstract string Title { get; }

    // 現在の進行状況を文字列で取得する
    // 例：「2/3」
    public abstract string Progress(RunStats s);

    // 縛り条件に違反しているか判定する
    // 条件から外れている場合は true を返す
    public abstract bool IsViolated(RunStats s);

    // 編集モード中にも進行状況を表示するかどうか
    // 通常は false、配置数の条件など必要な場合だけ true にする
    public virtual bool ShowProgressInEdit => false;
}

// ---- 星1（全お題共通）：死なずに倒す ----
// ボスを倒したかどうかは撃破処理側で判定する
// ここではプレイヤーが死亡していないかだけを確認する
public class SurviveRule : ChallengeRule
{
    // 縛り条件の名前
    public override string Title => "死なずにボスを倒す";

    // 死亡回数が0なら生存中、それ以外なら死亡と表示する
    public override string Progress(RunStats s) => s.Deaths == 0 ? "生存中" : "死亡";

    // 1回でも死亡していたら条件違反とする
    public override bool IsViolated(RunStats s) => s.Deaths > 0;
}

// ---- 星2（全お題共通）：残りHPの条件 ----
public class RemainingHpRule : ChallengeRule
{
    // 残しておく必要があるHPの割合
    private readonly float minRatio;

    // 必要なHPの割合を設定する
    // 初期値は最大HPの50%
    public RemainingHpRule(float minRatio = 0.5f)
    {
        this.minRatio = minRatio;
    }

    // 必要な残りHPの割合を表示する
    // 例：「残りHPが50%以上」
    public override string Title => $"残りHPが{minRatio * 100f:F0}%以上";

    // 現在のHPと最大HPを表示する
    public override string Progress(RunStats s) => $"HP {s.RemainingHp}/{s.MaxHp}";

    // 最大HPが0より大きく、残りHPが設定した割合を下回った場合は違反
    public override bool IsViolated(RunStats s) =>
        s.MaxHp > 0 && s.RemainingHp < s.MaxHp * minRatio;
}

// ---- 星3（5種類の中から、お題ごとに1つ設定） ----

// ナイフの使用本数に制限を設ける条件
public class KnifeLimitRule : ChallengeRule
{
    // 使用できるナイフの最大本数
    private readonly int max;

    // ナイフの最大使用本数を設定する
    public KnifeLimitRule(int max)
    {
        this.max = max;
    }

    // ナイフの使用制限を表示する
    public override string Title => $"ナイフは{max}本以内";

    // 現在投げたナイフの本数と上限を表示する
    public override string Progress(RunStats s) => $"{s.KnifeThrown}/{max}";

    // 投げたナイフの本数が上限を超えたら違反
    public override bool IsViolated(RunStats s) => s.KnifeThrown > max;
}

// 床に着地しないことを条件にするクラス
public class NoLandingRule : ChallengeRule
{
    // 縛り条件の名前
    public override string Title => "床に着地しない";

    // 現在までの着地回数を表示する
    public override string Progress(RunStats s) => $"着地 {s.Landings}回";

    // 1回でも着地したら条件違反
    public override bool IsViolated(RunStats s) => s.Landings > 0;
}

// 近接攻撃を使用しないことを条件にするクラス
public class NoMeleeRule : ChallengeRule
{
    // 縛り条件の名前
    public override string Title => "近接攻撃を使わない";

    // 現在までの近接攻撃の使用回数を表示する
    public override string Progress(RunStats s) => $"近接 {s.MeleeCount}回";

    // 1回でも近接攻撃を使用したら条件違反
    public override bool IsViolated(RunStats s) => s.MeleeCount > 0;
}

// 制限時間以内にボスを倒すことを条件にするクラス
public class TimeLimitRule : ChallengeRule
{
    // 設定された制限時間（秒）
    private readonly float seconds;

    // 制限時間を設定する
    public TimeLimitRule(float seconds)
    {
        this.seconds = seconds;
    }

    // 制限時間を表示する
    public override string Title => $"{seconds:F0}秒以内に倒す";

    // 経過時間と制限時間を表示する
    public override string Progress(RunStats s) => $"{s.ElapsedTime:F0}/{seconds:F0}秒";

    // 経過時間が制限時間を超えたら条件違反
    public override bool IsViolated(RunStats s) => s.ElapsedTime > seconds;
}

// ステージに配置する追加オブジェクトの数を制限するクラス
public class PlacementCountRule : ChallengeRule
{
    // 配置できるオブジェクトの最大数
    private readonly int max;

    // 配置数の上限を設定する
    public PlacementCountRule(int max)
    {
        this.max = max;
    }

    // 配置できるオブジェクトの上限を表示する
    public override string Title => $"追加で置く物は合計{max}個以内";

    // 現在の配置数と上限を表示する
    public override string Progress(RunStats s) => $"{s.PlacedCount}/{max}";

    // 配置数が上限を超えたら条件違反
    public override bool IsViolated(RunStats s) => s.PlacedCount > max;

    // この条件は編集モード中にも進行状況を表示する
    // 配置数を確認できるように true を返す
    public override bool ShowProgressInEdit => true;
}