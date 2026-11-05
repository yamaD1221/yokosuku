using UnityEngine;
using UnityEngine.InputSystem;

// 1回の挑戦の終了（ボス撃破・プレイヤー死亡）を管理するクラス
// 挑戦終了時に星の評価を計算し、結果画面を表示する
public class RunController : MonoBehaviour
{
    // RunControllerのインスタンスを管理する
    // 外部のスクリプトからRunController.Instanceでアクセスできる
    public static RunController Instance { get; private set; }

    // 結果画面が表示されているかどうかを取得する
    // Instanceやscreenが存在しない場合はfalseを返す
    public static bool IsResultShowing =>
        Instance != null && Instance.screen != null && Instance.screen.IsVisible;

    // 結果画面の管理クラス
    // Inspectorで設定されていない場合はシーン内から自動で探す
    [SerializeField] private ResultScreen screen;

    // デバッグ用の撃破キー設定
    // trueの場合、F1キーを押すとボスを倒した扱いにする
    // 実際のボスが完成したらfalseにする
    [SerializeField] private bool debugVictoryKey = true;

    // オブジェクト生成時に実行される
    void Awake()
    {
        // このオブジェクトをRunControllerのインスタンスとして登録する
        Instance = this;
    }

    // オブジェクトが有効になったときに実行される
    void OnEnable()
    {
        // 編集モードの切り替えを監視する
        // 編集状態が変更されたらOnEditingChangedを呼び出す
        LevelEditor.EditingChanged += OnEditingChanged;
    }

    // オブジェクトが無効になったときに実行される
    void OnDisable()
    {
        // 編集状態の変更通知を解除する
        // 不要なイベント呼び出しを防ぐ
        LevelEditor.EditingChanged -= OnEditingChanged;
    }

    // ゲーム開始時に1回実行される
    void Start()
    {
        // Inspectorで結果画面が設定されていない場合
        // シーン内からResultScreenを探して取得する
        if (screen == null) screen = FindFirstObjectByType<ResultScreen>();

        // 結果画面が見つからなければ処理を終了する
        if (screen == null) return;

        // 結果画面の各ボタンが押されたときの処理を登録する
        screen.RetryClicked += OnRetry;  // もう一度ボタン
        screen.NextClicked += OnNext;    // 次へボタン
        screen.QuitClicked += OnQuit;    // やめるボタン
    }

    // オブジェクトが破棄されるときに実行される
    void OnDestroy()
    {
        // 結果画面が存在する場合、登録したイベントを解除する
        if (screen != null)
        {
            screen.RetryClicked -= OnRetry;
            screen.NextClicked -= OnNext;
            screen.QuitClicked -= OnQuit;
        }

        // 自分自身が現在のインスタンスなら登録を解除する
        if (Instance == this) Instance = null;
    }

    // 毎フレーム実行される
    void Update()
    {
        // 現在のキーボード情報を取得する
        var kb = Keyboard.current;

        // デバッグ用キーが有効で、キーボードが存在し、
        // F1キーが押された瞬間にボス撃破扱いで挑戦を終了する
        if (debugVictoryKey && kb != null && kb.f1Key.wasPressedThisFrame)
            EndRun(true);
    }

    // 挑戦を終了させる処理
    // bossDefeated：ボスを倒した場合はtrue、死亡した場合はfalse
    public void EndRun(bool bossDefeated)
    {
        // 編集中、結果画面表示中、または結果画面が存在しない場合は終了しない
        if (LevelEditor.IsEditing || IsResultShowing || screen == null) return;

        // プレイ記録を管理するRunRecorderを取得する
        var rec = RunRecorder.Instance;

        // RunRecorderが存在しなければ処理を終了する
        if (rec == null) return;

        // 今回の挑戦の記録を終了する
        rec.FinishRun();

        // ボスを倒したかどうかとプレイ記録、
        // 星1～星3の各条件をもとに星の評価を計算する
        var result = StarEvaluator.Evaluate(
            bossDefeated,
            rec.Stats,
            rec.StarOneRule,
            rec.StarTwoRule,
            rec.StarThreeRule
        );

        // ゲームの時間を停止する
        // 結果画面を表示している間、ゲーム内の動きを止める
        Time.timeScale = 0f;

        // 撃破結果・星の評価・プレイ記録を渡して結果画面を表示する
        screen.Show(bossDefeated, result, rec);
    }

    // 「もう一度」ボタンが押されたときの処理
    // 同じお題と星3の条件を維持したまま編集モードに戻る
    void OnRetry()
    {
        // 結果画面を閉じる
        screen.Hide();

        // レベルエディターを編集モードに戻す
        LevelEditor.Instance.ReturnToEdit();
    }

    // 「次へ」ボタンが押されたときの処理
    // 新しいお題を開始する
    // 現在は初期マップ生成機能が未完成のため、配置をすべて消す仮の処理
    void OnNext()
    {
        // 結果画面を閉じる
        screen.Hide();

        // RunRecorderが存在する場合、新しいお題を開始する
        // 新しいお題に合わせて星3の条件も引き直す
        if (RunRecorder.Instance != null)
            RunRecorder.Instance.StartNewChallenge();

        // ステージに追加したオブジェクトをすべて削除する
        LevelEditor.Instance.ClearAllPlaced();

        // 編集モードに戻る
        LevelEditor.Instance.ReturnToEdit();
    }

    // 「やめる」ボタンが押されたときの処理
    // タイトル画面が未完成のため、現在はゲームを終了する
    void OnQuit()
    {
#if UNITY_EDITOR
        // Unityエディター上で実行している場合は再生モードを終了する
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // ビルドしたゲームの場合はアプリケーションを終了する
        Application.Quit();
#endif
    }

    // 編集モードの状態が変更されたときに呼び出される
    void OnEditingChanged(bool editing)
    {
        // 編集モードに切り替わった場合、
        // 結果画面が存在すれば閉じる
        // Tabキーで編集モードに戻った場合にも対応する
        if (editing && screen != null)
            screen.Hide();
    }
}