using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// プレイ中に発生した記録を管理するクラス
// 星1・星2・星3の縛り条件を保持し、プレイヤーの行動や経過時間を記録する
public class RunRecorder : MonoBehaviour
{
    // RunRecorderのインスタンスを管理する
    // 外部のスクリプトからRunRecorder.Instanceでアクセスできる
    public static RunRecorder Instance { get; private set; }

    // 挑戦が開始されたときに通知するイベント
    // 他のスクリプトから挑戦開始を検知できる
    public static event Action RunStarted;

    // ゲーム開始時に星3の条件をランダムに決めるかどうか
    // お題生成機能が完成するまでの仮の設定
    [SerializeField] private bool pickRandomOnStart = true;

    // デバッグ用の設定
    // trueの場合、編集モード中にF2キーを押すと星3の条件を引き直す
    // 完成版ではfalseにする
    [SerializeField] private bool allowDebugReroll = true;

    // 現在の挑戦で発生した記録を管理する
    // 死亡回数、ナイフ使用回数、経過時間などを保存する
    public RunStats Stats { get; } = new RunStats();

    // 星1の条件（全お題共通：死なずにクリアする）
    // 外部から条件を確認できるが、直接変更はできない
    public ChallengeRule StarOneRule { get; private set; }

    // 星2の条件（全お題共通：一定以上のHPを残す）
    public ChallengeRule StarTwoRule { get; private set; }

    // 星3の条件（お題ごとにランダムで決定する）
    // 一度決まった条件は次のお題になるまで固定する
    public ChallengeRule StarThreeRule { get; private set; }

    // 星1～星3のいずれかの条件が設定されているか確認する
    public bool HasRules =>
        StarOneRule != null || StarTwoRule != null || StarThreeRule != null;

    // 現在の挑戦が終了しているかどうか
    public bool RunEnded { get; private set; }

    // 星3の条件の種類数（現在は5種類）
    private const int RuleTypeCount = 5;

    // 現在選択されている星3の条件の番号
    // -1はまだ選択されていない状態
    private int starThreeIndex = -1;

    // プレイヤーのHPを管理するPlayerHealthへの参照
    private PlayerHealth health;

    // オブジェクト生成時に実行される
    void Awake()
    {
        // このオブジェクトをRunRecorderのインスタンスとして登録する
        Instance = this;

        // 星1の条件を設定する（死亡せずにクリアする）
        StarOneRule = new SurviveRule();

        // 星2の条件を設定する（最大HPの50%以上を残す）
        StarTwoRule = new RemainingHpRule(0.5f);

        // 設定が有効なら、星3の条件をランダムに決定する
        if (pickRandomOnStart) RerollStarThree();
    }

    // ゲーム開始時に1回実行される
    void Start()
    {
        // シーン内からPlayerHealthを探して取得する
        health = FindFirstObjectByType<PlayerHealth>();

        // PlayerHealthが存在する場合、HP変更イベントを登録する
        // HPが変化するたびにOnHealthChangedを呼び出す
        if (health != null) health.OnHealthChanged += OnHealthChanged;

        // 現在のHPをRunStatsに反映する
        // イベント登録前に変化したHPも正しく取得するため
        SyncHealth();
    }

    // オブジェクトが破棄されるときに実行される
    void OnDestroy()
    {
        // HP変更イベントの登録を解除する
        // 不要なイベント呼び出しを防ぐ
        if (health != null) health.OnHealthChanged -= OnHealthChanged;

        // 自分自身が現在のインスタンスなら登録を解除する
        if (Instance == this) Instance = null;
    }

    // 毎フレーム実行される
    void Update()
    {
        // 編集モード中の処理
        if (LevelEditor.IsEditing)
        {
            // 編集中は現在配置されているオブジェクト数を取得する
            // 配置数を常に最新の状態に保つ
            if (LevelEditor.Instance != null)
                Stats.PlacedCount = LevelEditor.Instance.PlacedObjectCount;

            // 現在のキーボード情報を取得する
            var kb = Keyboard.current;

            // デバッグ機能が有効で、F2キーが押された場合
            // 星3の条件をランダムに引き直す
            if (allowDebugReroll && kb != null && kb.f2Key.wasPressedThisFrame)
                RerollStarThree();
        }
        // 編集モードではなく、挑戦が終了していない場合
        else if (!RunEnded)
        {
            // 挑戦中の経過時間を加算する
            Stats.ElapsedTime += Time.deltaTime;
        }
    }

    // 星3の条件を5種類の中からランダムに1つ選ぶ
    // 前回と同じ条件が連続して選ばれないようにする
    void RerollStarThree()
    {
        int next;

        // ランダムで条件番号を決定する
        // 前回と同じ番号だった場合はもう一度抽選する
        do
        {
            next = UnityEngine.Random.Range(0, RuleTypeCount);
        }
        while (next == starThreeIndex && RuleTypeCount > 1);

        // 今回選ばれた条件番号を保存する
        starThreeIndex = next;

        // 条件番号に対応した縛り条件を生成して設定する
        StarThreeRule = CreateRule(next);
    }

    // 条件番号に応じた星3の縛り条件を生成する
    // 数値は現在の仮設定で、将来的にはお題ごとに決定する
    static ChallengeRule CreateRule(int index)
    {
        switch (index)
        {
            // 0：ナイフの使用本数を3本以内にする
            case 0:
                return new KnifeLimitRule(3);

            // 1：床に着地しない
            case 1:
                return new NoLandingRule();

            // 2：近接攻撃を使用しない
            case 2:
                return new NoMeleeRule();

            // 3：60秒以内にボスを倒す
            case 3:
                return new TimeLimitRule(60f);

            // 4：追加配置するオブジェクトを3個以内にする
            default:
                return new PlacementCountRule(3);
        }
    }

    // プレイヤーのHPが変更されたときに呼び出される
    // 現在のHPと最大HPをプレイ記録に保存する
    void OnHealthChanged(int hp, int max)
    {
        Stats.RemainingHp = hp;
        Stats.MaxHp = max;
    }

    // PlayerHealthから現在のHP情報を取得して記録に反映する
    void SyncHealth()
    {
        // PlayerHealthが存在しなければ処理を終了する
        if (health == null) return;

        // 現在のHPと最大HPを記録する
        Stats.RemainingHp = health.Hp;
        Stats.MaxHp = health.MaxHp;
    }

    // ---- 挑戦の開始と終了 ----

    // 新しい挑戦を開始する
    // placedCount：現在ステージに配置されているオブジェクト数
    public static void StartRun(int placedCount)
    {
        // RunRecorderが存在しなければ処理を終了する
        if (Instance == null) return;

        // 前回の挑戦記録をリセットする
        Instance.Stats.Reset();

        // 挑戦を終了状態から開始状態に戻す
        Instance.RunEnded = false;

        // 現在配置されているオブジェクト数を記録する
        Instance.Stats.PlacedCount = placedCount;

        // プレイヤーの現在のHPを記録に反映する
        Instance.SyncHealth();

        // 挑戦開始イベントを通知する
        // このイベントを登録している他のスクリプトにも開始を知らせる
        RunStarted?.Invoke();
    }

    // 挑戦を終了する
    // これ以降はプレイ中の記録を更新しない
    public void FinishRun()
    {
        RunEnded = true;
    }

    // 新しいお題を開始する
    // 現在は星3の条件をランダムに引き直す処理のみ
    public void StartNewChallenge()
    {
        RerollStarThree();
    }

    // ---- 他のスクリプトから記録を追加するためのメソッド ----
    // staticにすることで、Instanceを直接取得しなくても呼び出せる
    // 記録する側にRunRecorderが存在しなくてもエラーにならないようにする

    // ナイフを投げた回数を1増やす
    public static void KnifeThrown()
    {
        if (Counting) Instance.Stats.KnifeThrown++;
    }

    // 近接攻撃を使用した回数を1増やす
    public static void MeleeUsed()
    {
        if (Counting) Instance.Stats.MeleeCount++;
    }

    // 床に着地した回数を1増やす
    public static void Landed()
    {
        if (Counting) Instance.Stats.Landings++;
    }

    // プレイヤーが死亡した回数を1増やす
    public static void PlayerDied()
    {
        if (Counting) Instance.Stats.Deaths++;
    }

    // 現在、記録を追加できる状態かどうかを判定する
    // RunRecorderが存在し、編集モードではなく、挑戦が終了していない場合だけtrue
    static bool Counting =>
        Instance != null && !LevelEditor.IsEditing && !Instance.RunEnded;

    // HUDに表示する縛り条件の文章を作成する
    // 条件違反した場合は赤色、編集中の星3は金色で表示する
    public string BuildConstraintText()
    {
        // 現在編集モードかどうかを取得する
        bool editing = LevelEditor.IsEditing;

        // 表示する文章を組み立てるためのStringBuilder
        var sb = new StringBuilder();

        // ボスの情報を表示する
        sb.AppendLine("ボス: フレーム   (突進 / 遠距離 / 回転)");

        // 星1の条件を表示する（通常は白色）
        AppendRule(sb, "★1 共通", StarOneRule, "#ffffff");

        // 星2の条件を表示する（通常は白色）
        AppendRule(sb, "★2 共通", StarTwoRule, "#ffffff");

        // 星3の条件を表示する
        // 編集中は金色、それ以外は白色で表示する
        AppendRule(sb, "★3 今回", StarThreeRule, editing ? "#ffd54a" : "#ffffff");

        // 編集中で星3の条件が設定されている場合
        // 星3の条件に合わせてステージを作るよう案内を表示する
        if (editing && StarThreeRule != null)
            sb.AppendLine("<color=#ffd54a>→ ★3の条件に合う戦場を作ろう</color>");

        // 完成した文章を文字列として返す
        return sb.ToString();
    }

    // 1つの縛り条件をHUDに追加する
    // label：星の種類
    // r：表示する縛り条件
    // baseColor：通常時の文字色
    void AppendRule(StringBuilder sb, string label, ChallengeRule r, string baseColor)
    {
        // 条件が設定されていなければ表示しない
        if (r == null) return;

        // 編集中でなければ進行状況を表示する
        // 編集中はShowProgressInEditがtrueの条件だけ表示する
        bool showProgress = !LevelEditor.IsEditing || r.ShowProgressInEdit;

        // 進行状況を表示する場合のみ、条件違反しているか確認する
        bool violated = showProgress && r.IsViolated(Stats);

        // 条件違反している場合は赤色にする
        // 違反していない場合は指定された通常色にする
        string color = violated ? "#ff6666" : baseColor;

        // 進行状況を表示する場合は、条件の後ろに数値を追加する
        // 表示しない場合は空文字にする
        string progress = showProgress ? "   " + r.Progress(Stats) : "";

        // 星の種類・条件名・進行状況を色付き文字列として追加する
        // 条件違反している場合は最後に×印を表示する
        sb.AppendLine(
            $"<color={color}>{label}   {r.Title}{progress}{(violated ? "   ×" : "")}</color>"
        );
    }

    // 仮の確認用表示
    // 結果画面が完成するまでのデバッグ用
    // 現在のプレイ記録を画面上に表示する
    //void OnGUI()
    //{
    //    // 編集モード中は表示しない
    //    if (LevelEditor.IsEditing) return;

    //    // ナイフ使用回数、近接攻撃回数、着地回数、死亡回数、経過時間を表示する
    //    GUI.Label(new Rect(10, Screen.height - 40, 800, 30),
    //        $"ナイフ {Stats.KnifeThrown}   近接 {Stats.MeleeCount}   着地 {Stats.Landings}   死亡 {Stats.Deaths}   時間 {Stats.ElapsedTime:F1}秒");
    //}
}