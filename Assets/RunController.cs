using UnityEngine;
using UnityEngine.InputSystem;

// 1回の挑戦の終わり(撃破・死亡)を扱い、星を計算して結果画面を出す
public class RunController : MonoBehaviour
{
    public static RunController Instance { get; private set; }
    public static bool IsResultShowing => Instance != null && Instance.screen != null && Instance.screen.IsVisible;

    [SerializeField] private ResultScreen screen;           // 空なら、シーンから探す
    [SerializeField] private bool debugVictoryKey = true;   // 確認用: F1 で撃破扱い(ボスができたら切る)

    void Awake() { Instance = this; }

    void OnEnable() { LevelEditor.EditingChanged += OnEditingChanged; }
    void OnDisable() { LevelEditor.EditingChanged -= OnEditingChanged; }

    void Start()
    {
        if (screen == null) screen = FindFirstObjectByType<ResultScreen>();
        if (screen == null) return;
        screen.RetryClicked += OnRetry;
        screen.NextClicked += OnNext;
        screen.QuitClicked += OnQuit;
    }

    void OnDestroy()
    {
        if (screen != null)
        {
            screen.RetryClicked -= OnRetry;
            screen.NextClicked -= OnNext;
            screen.QuitClicked -= OnQuit;
        }
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (debugVictoryKey && kb != null && kb.f1Key.wasPressedThisFrame) EndRun(true);
    }

    // 挑戦を終える。bossDefeated: ボスを倒したか(死亡なら false)
    public void EndRun(bool bossDefeated)
    {
        if (LevelEditor.IsEditing || IsResultShowing || screen == null) return;
        var rec = RunRecorder.Instance;
        if (rec == null) return;

        rec.FinishRun();    // 記録を止める
        var result = StarEvaluator.Evaluate(bossDefeated, rec.Stats,
            rec.StarOneRule, rec.StarTwoRule, rec.StarThreeRule);

        Time.timeScale = 0f;
        screen.Show(bossDefeated, result, rec);
    }

    // もう一度: 同じお題(星3の条件は固定)で、配置を引き継いで、編集に戻る
    void OnRetry()
    {
        screen.Hide();
        LevelEditor.Instance.ReturnToEdit();
    }

    // 次へ: 新しいお題へ。(初期マップの生成ができるまでの仮の動き: 配置を全部消して、星3を引き直す)
    void OnNext()
    {
        screen.Hide();
        if (RunRecorder.Instance != null) RunRecorder.Instance.StartNewChallenge();
        LevelEditor.Instance.ClearAllPlaced();
        LevelEditor.Instance.ReturnToEdit();
    }

    // やめる: (タイトル画面ができるまでは、再生を止める)
    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnEditingChanged(bool editing)
    {
        if (editing && screen != null) screen.Hide();   // Tabで編集に戻ったときも、結果画面を閉じる
    }
}