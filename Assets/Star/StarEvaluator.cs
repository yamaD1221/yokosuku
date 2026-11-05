using UnityEngine;

// 星の計算結果をまとめる構造体
public readonly struct StarResult
{
    // 獲得した星の合計数（0〜3）
    public readonly int Stars;

    // 星1の条件を満たしたか
    // ボスを倒し、死亡していないこと
    public readonly bool StarOne;

    // 星2の条件を満たしたか
    public readonly bool StarTwo;

    // 星3の条件を満たしたか
    public readonly bool StarThree;

    // 星の計算結果を初期化する
    public StarResult(int stars, bool one, bool two, bool three)
    {
        // 星の合計数を設定する
        Stars = stars;

        // 各星の達成状況を設定する
        StarOne = one;
        StarTwo = two;
        StarThree = three;
    }
}

// 挑戦の記録とルールから獲得する星を計算するクラス
public static class StarEvaluator
{
    // 星の獲得数を計算する
    // 星は積み上げ式で、星1を獲得できなければ星2・3も獲得できない
    public static StarResult Evaluate(bool bossDefeated, RunStats s,
        ChallengeRule one, ChallengeRule two, ChallengeRule three)
    {
        // 星1：ボスを倒し、星1の条件も満たしているか判定する
        bool m1 = bossDefeated && Ok(one, s);

        // 星2：星2の条件を満たしているか判定する
        bool m2 = Ok(two, s);

        // 星3：星3の条件を満たしているか判定する
        bool m3 = Ok(three, s);

        // 星1から順番に条件を満たしている場合、星を獲得する
        // 星1未達成なら0個、星1のみなら1個、星1・2なら2個、すべて達成なら3個
        int stars = m1 ? (m2 ? (m3 ? 3 : 2) : 1) : 0;

        // 計算した星の合計数と各条件の達成状況を返す
        return new StarResult(stars, m1, m2, m3);
    }

    // 指定されたルールを達成しているか判定する
    static bool Ok(ChallengeRule r, RunStats s)
    {
        // ルールが設定されていない場合は達成扱いにする
        // ルールがある場合は、条件に違反していなければ達成とする
        return r == null || !r.IsViolated(s);
    }
}