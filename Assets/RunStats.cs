// 1回の挑戦の中で起きた事実を、数えて持つだけのデータ
public class RunStats
{
    public int KnifeThrown;     // ナイフを投げた回数
    public int MeleeCount;      // 近接攻撃を出した回数
    public int Deaths;          // 死亡回数
    public float ElapsedTime;   // プレイ中の経過時間(秒)

    public void Reset()
    {
        KnifeThrown = 0;
        MeleeCount = 0;
        Deaths = 0;
        ElapsedTime = 0f;
    }
}