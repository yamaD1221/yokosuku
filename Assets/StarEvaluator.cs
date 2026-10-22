// 星の計算結果
public readonly struct StarResult
{
    public readonly int Stars;       // 0?3
    public readonly bool StarOne;    // 星1の条件(ボスを倒す + 死なない)を満たしたか
    public readonly bool StarTwo;    // 星2の条件を満たしたか
    public readonly bool StarThree;  // 星3の条件を満たしたか

    public StarResult(int stars, bool one, bool two, bool three)
    {
        Stars = stars;
        StarOne = one;
        StarTwo = two;
        StarThree = three;
    }
}

// 星の計算(記録とルールだけを入力にする純粋な関数)
public static class StarEvaluator
{
    // 星は積み上げ式: 星1を満たさなければ、星2・3の条件を満たしても、星は0
    public static StarResult Evaluate(bool bossDefeated, RunStats s,
        ChallengeRule one, ChallengeRule two, ChallengeRule three)
    {
        bool m1 = bossDefeated && Ok(one, s);
        bool m2 = Ok(two, s);
        bool m3 = Ok(three, s);

        int stars = m1 ? (m2 ? (m3 ? 3 : 2) : 1) : 0;
        return new StarResult(stars, m1, m2, m3);
    }

    static bool Ok(ChallengeRule r, RunStats s) => r == null || !r.IsViolated(s);
}