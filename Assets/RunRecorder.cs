using UnityEngine;

// 実行中の事実を記録する。判定(縛り条件)はここでは行わない
public class RunRecorder : MonoBehaviour
{
    public static RunRecorder Instance { get; private set; }
    public RunStats Stats { get; } = new RunStats();

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (!LevelEditor.IsEditing) Stats.ElapsedTime += Time.deltaTime;
    }

    // ---- 呼び出し口(記録の担当がいなくても、呼び出し側は壊れない) ----
    public static void StartRun() { if (Instance != null) Instance.Stats.Reset(); }
    public static void KnifeThrown() { if (Counting) Instance.Stats.KnifeThrown++; }
    public static void MeleeUsed() { if (Counting) Instance.Stats.MeleeCount++; }
    public static void PlayerDied() { if (Counting) Instance.Stats.Deaths++; }

    static bool Counting => Instance != null && !LevelEditor.IsEditing;

    // 仮の確認用表示(ステップ2で、HUDに置き換える)
    void OnGUI()
    {
        if (LevelEditor.IsEditing) return;
        GUI.Label(new Rect(10, Screen.height - 40, 600, 30),
            $"ナイフ {Stats.KnifeThrown}   近接 {Stats.MeleeCount}   死亡 {Stats.Deaths}   時間 {Stats.ElapsedTime:F1}秒");
    }
}