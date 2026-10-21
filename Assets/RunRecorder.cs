using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// 実行中の事実を記録し、お題のルール(星1・星2・星3)を持つ
public class RunRecorder : MonoBehaviour
{
    public static RunRecorder Instance { get; private set; }
    public static event Action RunStarted;

    [SerializeField] private bool pickRandomOnStart = true;   // お題の生成(ステップ9)までの仮の決め方
    [SerializeField] private bool allowDebugReroll = true;    // 確認用: 編集中に F2 で星3を引き直す(完成版では切る)

    public RunStats Stats { get; } = new RunStats();
    public ChallengeRule StarOneRule { get; private set; }     // 全お題で共通: 死なない
    public ChallengeRule StarTwoRule { get; private set; }     // 全お題で共通: 残りHP
    public ChallengeRule StarThreeRule { get; private set; }   // お題ごとにランダムな1つ(以後は固定)
    public bool HasRules => StarOneRule != null || StarTwoRule != null || StarThreeRule != null;

    private const int RuleTypeCount = 5;
    private int starThreeIndex = -1;
    private PlayerHealth health;

    void Awake()
    {
        Instance = this;
        StarOneRule = new SurviveRule();
        StarTwoRule = new RemainingHpRule(0.5f);
        if (pickRandomOnStart) RerollStarThree();
    }

    void Start()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) health.OnHealthChanged += OnHealthChanged;
        SyncHealth();
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= OnHealthChanged;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (LevelEditor.IsEditing)
        {
            // 編集中は、現在の配置数を、常に最新にする
            if (LevelEditor.Instance != null) Stats.PlacedCount = LevelEditor.Instance.PlacedObjectCount;

            var kb = Keyboard.current;
            if (allowDebugReroll && kb != null && kb.f2Key.wasPressedThisFrame) RerollStarThree();
        }
        else
        {
            Stats.ElapsedTime += Time.deltaTime;
        }
    }

    // 星3の条件を、5種類から1つ選ぶ(前回と同じものは避ける)
    void RerollStarThree()
    {
        int next;
        do { next = UnityEngine.Random.Range(0, RuleTypeCount); }
        while (next == starThreeIndex && RuleTypeCount > 1);

        starThreeIndex = next;
        StarThreeRule = CreateRule(next);
    }

    // 数値は仮の固定値(ステップ9で、お題ごとに決める)
    static ChallengeRule CreateRule(int index)
    {
        switch (index)
        {
            case 0: return new KnifeLimitRule(3);
            case 1: return new NoLandingRule();
            case 2: return new NoMeleeRule();
            case 3: return new TimeLimitRule(60f);
            default: return new PlacementCountRule(3);
        }
    }

    void OnHealthChanged(int hp, int max)
    {
        Stats.RemainingHp = hp;
        Stats.MaxHp = max;
    }

    void SyncHealth()
    {
        if (health == null) return;
        Stats.RemainingHp = health.Hp;
        Stats.MaxHp = health.MaxHp;
    }

    // ---- 呼び出し口(記録の担当がいなくても、呼び出し側は壊れない) ----
    public static void StartRun(int placedCount)
    {
        if (Instance == null) return;
        Instance.Stats.Reset();
        Instance.Stats.PlacedCount = placedCount;
        Instance.SyncHealth();
        RunStarted?.Invoke();
    }
    public static void KnifeThrown() { if (Counting) Instance.Stats.KnifeThrown++; }
    public static void MeleeUsed() { if (Counting) Instance.Stats.MeleeCount++; }
    public static void Landed() { if (Counting) Instance.Stats.Landings++; }
    public static void PlayerDied() { if (Counting) Instance.Stats.Deaths++; }

    static bool Counting => Instance != null && !LevelEditor.IsEditing;

    // HUD用: 星1・星2・星3の条件(外れたら赤)。編集中は、星3を金色にして目立たせる
    public string BuildConstraintText()
    {
        bool editing = LevelEditor.IsEditing;
        var sb = new StringBuilder();
        AppendRule(sb, "★1 共通", StarOneRule, "#ffffff");
        AppendRule(sb, "★2 共通", StarTwoRule, "#ffffff");
        AppendRule(sb, "★3 今回", StarThreeRule, editing ? "#ffd54a" : "#ffffff");
        if (editing && StarThreeRule != null)
            sb.AppendLine("<color=#ffd54a>→ ★3の条件に合う戦場を作ろう</color>");
        return sb.ToString();
    }

    void AppendRule(StringBuilder sb, string label, ChallengeRule r, string baseColor)
    {
        if (r == null) return;

        bool showProgress = !LevelEditor.IsEditing || r.ShowProgressInEdit;
        bool violated = showProgress && r.IsViolated(Stats);
        string color = violated ? "#ff6666" : baseColor;
        string progress = showProgress ? "   " + r.Progress(Stats) : "";
        sb.AppendLine($"<color={color}>{label}   {r.Title}{progress}{(violated ? "   ×" : "")}</color>");
    }

    // 仮の確認用表示(結果画面ができたら置き換える)
    void OnGUI()
    {
        if (LevelEditor.IsEditing) return;
        GUI.Label(new Rect(10, Screen.height - 40, 800, 30),
            $"ナイフ {Stats.KnifeThrown}   近接 {Stats.MeleeCount}   着地 {Stats.Landings}   死亡 {Stats.Deaths}   時間 {Stats.ElapsedTime:F1}秒");
    }
}