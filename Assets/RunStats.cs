// 1回の挑戦の中で起きた事実を、数えて持つだけのデータ
public class RunStats
{
    public int KnifeThrown;     // ナイフを投げた回数
    public int MeleeCount;      // 近接攻撃を出した回数
    public int Landings;        // 床への着地回数(ナイフ足場や敵の上は含めない)
    public int Deaths;          // 死亡回数
    public float ElapsedTime;   // プレイ中の経過時間(秒)
    public int RemainingHp;     // プレイヤーの現在のHP
    public int MaxHp;           // プレイヤーの最大HP
    public int PlacedCount;     // 追加で置いた物の合計数

    public void Reset()
    {
        KnifeThrown = 0;
        MeleeCount = 0;
        Landings = 0;
        Deaths = 0;
        ElapsedTime = 0f;
        RemainingHp = 0;
        MaxHp = 0;
        PlacedCount = 0;
    }
}