using System;                       // Actionなどの基本的な機能を使うため
using System.Collections;           // IEnumeratorなど、コルーチンに必要な機能を使うため
using System.Collections.Generic;   // ListやDictionaryなどのコレクションを使うため
using UnityEngine;                  // Unityの基本機能を使うため
using UnityEngine.EventSystems;     // マウスやタッチなどのUIイベントを扱うため
using UnityEngine.InputSystem;      // 新しいInput Systemでキーボードやマウスの入力を扱うため

// レベル編集画面の管理を行うクラス
// MonoBehaviourを継承することで、UnityのGameObjectにアタッチして使用できる
public class LevelEditor : MonoBehaviour
{
    // LevelEditorのインスタンスを管理する静的プロパティ
    // 外部のクラスから参照できるが、値を変更できるのはLevelEditorクラス内だけ
    public static LevelEditor Instance { get; private set; }

    // 現在レベル編集モードかどうかを管理する
    // trueなら編集中、falseなら通常プレイ中
    public static bool IsEditing { get; private set; }

    // 編集モードが切り替わったときに通知するイベント
    // boolの値によって、編集開始(true)か編集終了(false)かを伝える
    public static event Action<bool> EditingChanged;

    // 現在配置されているオブジェクトの総数を取得するプロパティ
    public int PlacedObjectCount
    {
        get
        {
            int n = 0; // オブジェクト数を数えるための変数

            // Roots()で取得した各親オブジェクトを順番に調べる
            foreach (var root in Roots())
                n += root.childCount; // 各親オブジェクトの子オブジェクト数を加算する

            return n; // 合計した配置オブジェクト数を返す
        }
    }

    // 編集画面に戻るための処理
    // ExitToEdit()を呼び出して、編集モードへ移行する
    public void ReturnToEdit()
    {
        ExitToEdit();
    }

    // インスペクターから設定できるオブジェクトの情報をまとめたクラス
    [Serializable]
    public class Item
    {
        public string id;              // オブジェクトを識別するためのID
        public GameObject prefab;      // 配置するときに生成するプレハブ
        public Sprite icon;            // パレットに表示する画像（未設定なら部品のスプライトを使用）

        public bool isPlayer;          // プレイヤー用の項目かどうか（スタート地点として扱う）

        public bool canRotate = true;  // 編集画面で回転できるかどうか（初期値はtrue）
        public bool canResize = true;  // 編集画面でサイズ変更できるかどうか（初期値はtrue）
    }

    // LevelEditorで使用するアイテムの一覧
    // Inspectorから登録したアイテムを編集や配置に使用する
    [SerializeField] private List<Item> items = new List<Item>();

    // プレイヤーのGameObjectへの参照
    // Inspectorで設定されていない場合は、Playerタグが付いたオブジェクトを探して使用する
    [SerializeField] private GameObject player;

    // ステージ内のオブジェクトをまとめる親オブジェクト
    // ステージに配置するアイテムなどはこのTransformの子として管理する
    [SerializeField] private Transform levelRoot;

    // 敵などの動くオブジェクトをまとめる親オブジェクト
    // levelRootの外側に配置して、ステージの固定オブジェクトと分けて管理する
    // Inspectorで設定されていない場合はlevelRootを使用する
    [SerializeField] private Transform dynamicRoot;

    // エディタ上で表示する線のマテリアル
    // グリッドや範囲などの線を描画するときに使用する
    [SerializeField] private Material lineMaterial;

    // カメラを画面上で移動させる速度
    // 値を大きくすると、編集時にカメラをより速く移動できる
    [SerializeField] private float cameraPanSpeed = 12f;

    // オブジェクトを移動するときのスナップ幅
    // 0.5なら、オブジェクトの位置を0.5単位ごとに揃えて配置する
    [SerializeField] private float moveSnap = 0.5f;

    // オブジェクトを回転するときの角度
    // 15なら、回転操作を15度ずつ行う
    [SerializeField] private float rotateStep = 15f;

    // オブジェクトの最小サイズ
    // オブジェクトを縮小するとき、この値より小さくならないようにする
    [SerializeField] private float minSize = 0.25f;
    //[SerializeField] private float editHalfRange = 4f;

    // オブジェクトをドラッグして操作するときの状態
    // None：何も操作していない
    // Move：オブジェクトを移動している
    // Resize：オブジェクトの大きさを変更している
    private enum DragMode { None, Move, Resize }

    // ステージに配置したオブジェクトの情報をまとめて管理する構造体
    // アイテムの種類、位置、回転、大きさを保存する
    private struct ObjData
    {
        // LevelEditorに登録されているアイテムの番号
        // どのアイテムを配置しているかを識別するために使用する
        public int itemIndex;

        // オブジェクトの位置
        // ステージ上のどこに配置されているかを保存する
        public Vector3 position;

        // オブジェクトの回転
        // ステージ上でどの方向を向いているかを保存する
        public Quaternion rotation;

        // オブジェクトの大きさ
        // X、Y、Zそれぞれの大きさを保存する
        public Vector3 scale;
    }

    // ステージ編集で使用するカメラへの参照
    // オブジェクトの選択やマウス位置の取得などに使用する
    private Camera cam;

    // 現在選択しているアイテムの番号
    // LevelEditorに登録されているアイテムを識別するために使用する
    private int selectedItem;

    // 現在選択しているステージ上のオブジェクト
    // 移動や回転、サイズ変更などの編集対象として使用する
    private LevelObject current;

    // プレイヤーに対応するLevelObject
    // プレイヤーの位置や状態を編集するときに使用する
    private LevelObject playerLo;

    // 現在行っているドラッグ操作の種類
    // 移動やサイズ変更など、現在の操作状態を管理する
    private DragMode mode;

    // オブジェクトをドラッグするときの位置のずれ
    // マウスを押した位置とオブジェクトの位置を維持するために使用する
    private Vector2 dragOffset;

    // サイズ変更用のハンドルの位置
    // オブジェクトのサイズを変更するときの基準位置として使用する
    private Vector2 handle;

    // オブジェクトの位置をグリッドに合わせるかどうか
    // trueなら移動時にグリッドへスナップし、falseなら自由に移動する
    private bool snap = true;

    // ステージ上のオブジェクトの状態を一時的に保存するリスト
    // 編集前の位置や回転、大きさなどを記録して元の状態に戻すために使用する
    private readonly List<ObjData> snapshot = new List<ObjData>();

    // プレイヤーの開始位置
    // ステージを開始するときのプレイヤーの位置を保存する
    private Vector3 playerStartPos;

    // プレイヤーの開始時の回転
    // ステージを開始するときのプレイヤーの向きを保存する
    private Quaternion playerStartRot;

    // ステージがクリアされたかどうか
    // trueならクリア済み、falseならまだクリアしていない状態を表す
    private bool cleared;

    // クリア表示を表示するかどうか
    // trueならクリア時の表示を行い、falseなら表示しない
    private bool showClear;

    // 選択中のオブジェクトを囲む枠線
    // オブジェクトの選択範囲を画面上に表示するために使用する
    private LineRenderer frameLine;

    // サイズ変更用のハンドルを表示する線
    // 8方向のハンドルそれぞれに対応するLineRendererを管理する
    private readonly LineRenderer[] handleLines = new LineRenderer[8];

    // オブジェクトのサイズ変更に使用する8方向のハンドル位置
    // 左下、下、右下、左、右、左上、上、右上の順で方向を表す
    private static readonly Vector2[] HandleDirs =
    {
    // 左下、下、右下の方向
    new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),

    // 左、右の方向
    new Vector2(-1,  0),                     new Vector2(1,  0),

    // 左上、上、右上の方向
    new Vector2(-1,  1), new Vector2(0,  1), new Vector2(1,  1),
};

  
    // オブジェクト生成時に1回実行される
    // LevelEditorで使用する線やハンドルなどを初期化する
    void Awake()
    {
        // このLevelEditorをInstanceに設定する
        // 他のスクリプトからLevelEditor.Instanceを通してアクセスできるようにする
        Instance = this;

        // 線を描画するときに使用するマテリアルを取得する
        // InspectorでlineMaterialが設定されていればそれを使用する
        // 未設定の場合はSprites/Defaultシェーダーを使用して新しく作成する
        var mat = lineMaterial != null
            ? lineMaterial
            : new Material(Shader.Find("Sprites/Default"));

        // 選択中のオブジェクトを囲む枠線を作成する
        // 太さ4、ループする線、黄色で表示する
        frameLine = MakeLine(mat, 4, true, Color.yellow);

        // サイズ変更用のハンドル線を8個作成する
        // それぞれ太さ2、ループしない線、白色で表示する
        for (int i = 0; i < handleLines.Length; i++)
            handleLines[i] = MakeLine(mat, 2, false, Color.white);

        // 初期状態では選択中のオブジェクトの枠線を非表示にする
        HideFrame();
    }


    // ゲーム開始時に1回実行される
    // カメラやプレイヤーを取得し、編集モードを開始するための準備を行う
    void Start()
    {
        // メインカメラを取得する
        // ステージ上のオブジェクトを選択するときなどに使用する
        cam = Camera.main;

        // プレイヤーが設定されていない場合、
        // Playerタグが付いたGameObjectを探して取得する
        if (player == null)
            player = GameObject.FindWithTag("Player");

        // プレイヤーもステージ上に配置したオブジェクトとして選択できるようにする
        // itemsの中からプレイヤーとして登録されているアイテムの番号を取得する
        int pi = items.FindIndex(x => x.isPlayer);

        // プレイヤーが見つかり、プレイヤー用のアイテムが登録されている場合に処理する
        if (player != null && pi >= 0)
        {
            // プレイヤーに付いているLevelObjectを取得する
            // プレイヤーを編集対象として扱うために使用する
            playerLo = player.GetComponent<LevelObject>();

            // LevelObjectが付いていない場合は新しく追加する
            // これによりプレイヤーもステージ上のオブジェクトとして管理できる
            if (playerLo == null)
                playerLo = player.AddComponent<LevelObject>();

            // プレイヤーに対応するアイテムの番号を設定する
            // LevelEditor上でプレイヤーを正しいアイテムとして識別するために使用する
            playerLo.itemIndex = pi;
        }

        // 起動時は編集モードにする
        // ステージ上のオブジェクトを編集できる状態にする
        ApplyMode(true);
    }

    // オブジェクトが破棄されるときに実行される
    // 編集モードやゲームの時間を元の状態に戻し、LevelEditorの参照を解除する
    void OnDestroy()
    {
        // ゲームの時間の進行速度を通常の状態に戻す
        // 編集モード中に時間を停止していた場合でも、破棄後に時間が止まらないようにする
        Time.timeScale = 1f;

        // 編集モードを終了する
        // LevelEditorが存在しなくなったあとも編集モードとして扱われないようにする
        IsEditing = false;

        // 現在のInstanceがこのオブジェクト自身の場合に処理する
        // 別のLevelEditorがInstanceになっている場合は、その参照を消さないようにする
        if (Instance == this)
            Instance = null;
    }

    // 毎フレーム実行される
    // キーボードやマウスの入力を確認し、編集モード中の操作を処理する
    void Update()
    {
        // 現在のキーボード入力を取得する
        // 編集モードの切り替えや各種操作の入力確認に使用する
        var kb = Keyboard.current;

        // Tabキーが押された瞬間に編集モードとプレイモードを切り替える
        if (kb.tabKey.wasPressedThisFrame)
        {
            // 現在編集モードの場合はプレイモードに切り替える
            if (IsEditing)
                EnterPlay();

            // プレイモードの場合は編集モードに戻す
            else
                ExitToEdit();
        }

        // 編集モード中でない場合は、ここから先の編集操作を行わない
        if (!IsEditing) return;

        // キーボード入力による編集操作を処理する
        HandleKeys(kb);

        // マウス入力による編集操作を処理する
        HandleMouse(kb);

        // 選択中のオブジェクトの枠線やハンドルなどを更新する
        UpdateFrame();
    }

 
    // 編集モードとプレイモードを切り替える
    // editing：trueなら編集モード、falseならプレイモードに切り替える
    void ApplyMode(bool editing)
    {
        // 現在の編集モードの状態を設定する
        // trueなら編集モード、falseならプレイモードとして扱う
        IsEditing = editing;

        // 編集モード中はゲームの時間を停止し、プレイモードでは通常の速度に戻す
        // 編集中に敵やプレイヤーなどが動かないようにする
        Time.timeScale = editing ? 0f : 1f;

        // 編集モードの状態が変更されたことを通知する
        // パレットなど、編集モードに応じて表示を切り替える処理で使用する
        EditingChanged?.Invoke(editing);
    }


    // 編集モードからプレイモードに切り替える
    // 編集中のステージ状態を保存し、プレイヤーがステージをプレイできる状態にする
    void EnterPlay()
    {
        // プレイ開始前のステージ状態を保存する
        // プレイ終了後に編集前の状態へ戻すために使用する
        TakeSnapshot();

        // プレイ開始をRunRecorderに通知する
        // ステージ内に配置されているオブジェクト数を渡してプレイ記録を開始する
        RunRecorder.StartRun(snapshot.Count);

        // 現在選択しているオブジェクトの選択状態を解除する
        // プレイ中に編集用の選択状態が残らないようにする
        Select(null);

        // オブジェクトの選択枠やハンドルを非表示にする
        // プレイ中に編集用の表示が残らないようにする
        HideFrame();

        // 編集モードからプレイモードへ切り替える
        // ゲームの時間を通常の速度に戻し、プレイを開始できる状態にする
        ApplyMode(false);
    }


    // プレイモードから編集モードに戻す
    // プレイ中の状態をリセットし、プレイ開始前のステージ状態へ戻す
    void ExitToEdit()
    {
        // 実行中のコルーチンをすべて停止する
        // プレイ中に動いていた処理が編集モードに戻ったあとも続かないようにする
        StopAllCoroutines();

        // クリア状態を解除する
        // プレイ中にクリアしていた場合でも、編集モードでは未クリアの状態に戻す
        cleared = false;

        // クリア表示を非表示にする
        // プレイ中に表示していたクリア画面などを編集モードに残さないようにする
        showClear = false;

        // アリーナの状態を初期状態に戻す
        // ステージを復元する前に箱の回転を元に戻し、正しい状態で復元できるようにする
        ResetArena();

        // プレイ開始前に保存しておいたステージの状態を復元する
        // プレイ中に移動・回転・サイズ変更されたオブジェクトを編集前の状態に戻す
        RestoreLevel();

        // プレイヤーを開始時の位置と回転に戻す
        // プレイ中に移動したプレイヤーをステージ開始時の状態へ戻す
        ResetPlayer();

        // プレイヤーとカメラが存在し、アリーナの回転処理が存在しない場合に処理する
        // 箱がある場合はアリーナ側でカメラを管理するため、ここではカメラを動かさない
        if (player != null && cam != null && ArenaRotator.Instance == null)
            // 箱があるときは、カメラを動かさない
            cam.transform.position = new Vector3(
                playerStartPos.x,
                playerStartPos.y,
                cam.transform.position.z
            );

        // プレイモードから編集モードへ切り替える
        // ゲームの時間を停止し、ステージを編集できる状態に戻す
        ApplyMode(true);
    }


    // 現在のプレイ状態をリセットし、プレイ開始時の状態からもう一度プレイする
    public void RestartPlay()
    {
        // 実行中のコルーチンをすべて停止する
        // 前回のプレイ中に動いていた処理がリスタート後も残らないようにする
        StopAllCoroutines();

        // クリア状態を解除する
        // すでにステージをクリアしていた場合でも、未クリアの状態から再開する
        cleared = false;

        // クリア表示を非表示にする
        // 前回のプレイで表示されていたクリア画面などを消す
        showClear = false;

        // アリーナの状態を初期状態に戻す
        // プレイ中に変更された箱などの回転を元の状態へ戻す
        ResetArena();

        // プレイ開始前に保存しておいたステージの状態を復元する
        // プレイ中に移動・回転・サイズ変更されたオブジェクトを開始時の状態に戻す
        RestoreLevel();

        // プレイヤーを開始時の位置と回転に戻す
        // 前回のプレイで移動したプレイヤーをスタート地点へ戻す
        ResetPlayer();

        // プレイモードを維持する
        // ステージをリセットしたあとも、そのままプレイを続けられる状態にする
        ApplyMode(false);
    }


    // プレイヤーがゴールに到達したときに呼び出される
    // 編集モード中や、すでにクリア済みの場合は処理せず、クリア処理を開始する
    public void OnGoalReached()
    {
        // 編集モード中、またはすでにクリア済みの場合は何もしない
        // 編集中にゴール判定が発生したり、ゴール判定が複数回呼ばれたりするのを防ぐ
        if (IsEditing || cleared) return;

        // ステージをクリアした状態にする
        // ClearRoutineなどでクリア済みかどうかを判定するために使用する
        cleared = true;

        // クリア時に行う処理をコルーチンとして開始する
        // クリア演出や表示など、時間をかけて行う処理を順番に実行できるようにする
        StartCoroutine(ClearRoutine());
    }


    // ステージクリア時に表示や時間の停止などを行い、一定時間後に編集モードへ戻す
    // コルーチンを使用することで、クリア演出中に一定時間待ってから次の処理を実行できる
    IEnumerator ClearRoutine()
    {
        // クリア表示を有効にする
        // プレイヤーがゴールに到達したことを画面上に表示するために使用する
        showClear = true;

        // ゲームの時間を通常の30%まで遅くする
        // クリアした瞬間の動きをゆっくり見せるために使用する
        Time.timeScale = 0.3f;

        // 実時間で1.5秒待つ
        // Time.timeScaleを0.3に変更していても、実際の時間で1.5秒間クリア表示を見せる
        yield return new WaitForSecondsRealtime(1.5f);

        // クリア演出が終わったら編集モードへ戻す
        // ステージをプレイ開始前の状態に戻し、再び編集できるようにする
        ExitToEdit();
    }


    // 現在のステージ上に配置されているオブジェクトの状態を保存する
    // プレイ開始前の位置・回転・大きさなどを記録し、プレイ終了後に元の状態へ戻せるようにする
    void TakeSnapshot()
    {
        // 前回保存したスナップショットをすべて削除する
        // 最新のステージ状態だけを保存するため、古い情報が残らないようにする
        snapshot.Clear();

        // ステージ上の各ルートオブジェクトを取得して処理する
        // 複数の親オブジェクトに分かれて配置されているステージオブジェクトをまとめて調べる
        foreach (var root in Roots())
        {
            // ルートオブジェクトの子オブジェクトを1つずつ調べる
            // 各子オブジェクトがステージ上に配置されたLevelObjectか確認する
            foreach (Transform child in root)
            {
                // 子オブジェクトに付いているLevelObjectを取得する
                // itemIndexなど、ステージ上のオブジェクトとして必要な情報を取得するために使用する
                var lo = child.GetComponent<LevelObject>();

                // LevelObjectが付いていないオブジェクトは保存対象にしない
                // ステージ編集用のオブジェクト以外をスナップショットに含めないようにする
                if (lo == null) continue;

                // 現在のオブジェクトの状態をスナップショットに追加する
                // プレイ中に変更されたオブジェクトを、プレイ開始前の状態へ戻すために使用する
                snapshot.Add(new ObjData
                {
                    // LevelEditorに登録されているアイテムの番号を保存する
                    // 復元時にどの種類のオブジェクトだったかを識別するために使用する
                    itemIndex = lo.itemIndex,

                    // 現在のワールド座標を保存する
                    // プレイ開始時にどこに配置されていたかを記録する
                    position = child.position,

                    // 現在のワールド回転を保存する
                    // プレイ開始時にどの方向を向いていたかを記録する
                    rotation = child.rotation,

                    // 現在のローカルスケールを保存する
                    // プレイ開始時のオブジェクトの大きさを記録する
                    scale = child.localScale
                });
            }
        }

        // プレイヤーが存在する場合に、プレイヤーの開始状態を保存する
        // プレイ終了後にプレイヤーを開始地点へ戻すために使用する
        if (player != null)
        {
            // プレイヤーの現在の位置を開始位置として保存する
            // プレイ開始時にプレイヤーがいた場所を記録する
            playerStartPos = player.transform.position;

            // プレイヤーの現在の回転を開始時の回転として保存する
            // プレイ開始時にプレイヤーが向いていた方向を記録する
            playerStartRot = player.transform.rotation;
        }
    }

    // ---------- ステージの状態を復元 ----------

    // プレイ開始前に保存したスナップショットを使ってステージを元の状態に戻す
    // プレイ中に移動・回転・削除されたオブジェクトなどを、プレイ開始時の状態へ復元する
    void RestoreLevel()
    {
        // ステージのルートが設定されていない場合は復元処理を行わない
        // 復元先となるオブジェクトがない状態で処理を続けるのを防ぐ
        if (levelRoot == null) return;

        // ステージ上の各ルートオブジェクトを取得して処理する
        // 復元前に現在配置されているオブジェクトをすべて削除するために使用する
        foreach (var root in Roots())
        {
            // ルートの子オブジェクトを後ろから順番に削除する
            // 後ろから削除することで、子オブジェクトを削除した際にインデックスがずれても正しく処理できる
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        // 投げたナイフもすべて削除する
        // ナイフが箱などの子オブジェクトになっている場合でも、プレイ中に生成されたナイフを復元後に残さないようにする
        foreach (var k in FindObjectsByType<Knife>(FindObjectsSortMode.None))
            Destroy(k.gameObject);

        // 保存しておいたスナップショットを1つずつ取り出して復元する
        // プレイ開始前に存在していたオブジェクトを同じ種類・位置・回転・大きさで再生成する
        foreach (var d in snapshot)
        {
            // 保存されているアイテム番号が正しい範囲にあるか確認する
            // 存在しないアイテム番号やPrefabが未設定の場合は復元できないため、そのデータをスキップする
            if (d.itemIndex < 0 ||
                d.itemIndex >= items.Count ||
                items[d.itemIndex].prefab == null)
                continue;

            // 保存されているアイテム番号から、復元するPrefabを取得する
            // プレイ開始前に配置されていた種類と同じPrefabを生成するために使用する
            var prefab = items[d.itemIndex].prefab;

            // 保存しておいた位置・回転を使ってPrefabを生成する
            // RootFor(prefab)で適切なステージの親オブジェクトに配置する
            var go = Instantiate(
                prefab,
                d.position,
                d.rotation,
                RootFor(prefab)
            );

            // 保存しておいた大きさに戻す
            // プレイ開始前と同じサイズでオブジェクトを復元する
            go.transform.localScale = d.scale;

            // 復元したオブジェクトにLevelObjectを追加する
            // itemIndexを設定することで、復元したオブジェクトをLevelEditor上のアイテムとして管理できるようにする
            go.AddComponent<LevelObject>().itemIndex = d.itemIndex;
        }

        // 変更したTransformの情報をPhysics2Dへ反映する
        // 復元直後のオブジェクトの位置や大きさを物理演算側にも正しく認識させる
        Physics2D.SyncTransforms();
    }


    // ステージ上に配置したオブジェクトの親となるTransformをすべて返す
    // levelRootやdynamicRootの子オブジェクトをまとめて調べるときに使用する
    IEnumerable<Transform> Roots()
    {
        // levelRootが設定されている場合に処理する
        // 通常のステージオブジェクトが配置されている親として使用する
        if (levelRoot != null)
            yield return levelRoot;

        // dynamicRootが設定されていて、levelRootとは別のオブジェクトの場合に処理する
        // 動的に生成されるオブジェクトなど、別の親に配置されたオブジェクトも取得できるようにする
        if (dynamicRoot != null && dynamicRoot != levelRoot)
            yield return dynamicRoot;
    }


    // 動く物（DynamicのRigidbody2Dを持つ物）を配置する親を決める
    // Dynamicの物は回転する箱の影響を受けない親に配置し、それ以外の物は通常のステージ側に配置する
    Transform RootFor(GameObject prefab)
    {
        // dynamicRootまたはPrefabが設定されていない場合はlevelRootを使用する
        // 配置先を決めるために必要な情報がない場合でも、通常のステージ側へ配置できるようにする
        if (dynamicRoot == null || prefab == null)
            return levelRoot;

        // Prefabに付いているRigidbody2Dを取得する
        // 物理演算によって動くオブジェクトかどうかを判定するために使用する
        var body = prefab.GetComponent<Rigidbody2D>();

        // Rigidbody2DがDynamicの場合はdynamicRoot、それ以外の場合はlevelRootを返す
        // Dynamicの物を回転する箱の外側に置くことで、箱の回転に巻き込まれないようにする
        return (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            ? dynamicRoot
            : levelRoot;
    }


    // 回転する箱（アリーナ）の回転を初期状態に戻す
    // プレイ終了時やステージをリスタートするときに、箱が回転したままにならないようにする
    void ResetArena()
    {
        // ArenaRotatorが存在する場合に処理する
        // ArenaRotatorが存在しないステージでは、何もせずそのまま処理を終了する
        if (ArenaRotator.Instance != null)
            // 回転する箱の回転を初期状態に戻す
            // プレイ中に変更されたアリーナの向きを、プレイ開始時の状態へ戻す
            ArenaRotator.Instance.ResetRotation();
    }


    // プレイヤーをプレイ開始時の状態に戻す
    // 位置や回転だけでなく、速度・体力・操作状態・着地状態なども初期状態へ戻す
    void ResetPlayer()
    {
        // プレイヤーが設定されていない場合は処理を行わない
        // リセットする対象が存在しない状態で処理を続けるのを防ぐ
        if (player == null) return;

        // プレイヤーの位置と回転をプレイ開始時の状態に戻す
        // プレイ中に移動したり向きが変わったりしていても、開始地点から再開できるようにする
        player.transform.SetPositionAndRotation(
            playerStartPos,
            playerStartRot
        );

        // プレイヤーに付いているRigidbody2Dを取得する
        // プレイ中に発生した移動速度や回転速度をリセットするために使用する
        var rb = player.GetComponent<Rigidbody2D>();

        // Rigidbody2Dが存在する場合に物理状態をリセットする
        if (rb != null)
        {
            // プレイヤーの移動速度を0にする
            // リセット直後にプレイ中の勢いが残って動き続けないようにする
            rb.linearVelocity = Vector2.zero;

            // プレイヤーの回転速度を0にする
            // リセット直後にプレイ中の回転が残って回り続けないようにする
            rb.angularVelocity = 0f;
        }

        // プレイヤーに付いているPlayerHealthを取得する
        // プレイ中に減少した体力などを初期状態へ戻すために使用する
        var health = player.GetComponent<PlayerHealth>();

        // PlayerHealthが存在する場合は体力の状態をリセットする
        // プレイ開始時と同じ体力状態で再開できるようにする
        if (health != null)
            health.ResetState();

        // プレイヤーに付いているPlayerControllerを取得する
        // プレイ中に変更された操作状態などを初期状態へ戻すために使用する
        var controller = player.GetComponent<PlayerController>();

        // PlayerControllerが存在する場合は操作状態をリセットする
        // リセット前の入力や移動状態が残ったまま再開されるのを防ぐ
        if (controller != null)
            controller.ResetState();

        // プレイヤーに付いているPlayerLandingTrackerを取得する
        // プレイ中に記録された着地状態などをリセットするために使用する
        var tracker = player.GetComponent<PlayerLandingTracker>();

        // PlayerLandingTrackerが存在する場合は着地状態をリセットする
        // 前回のプレイで記録された着地情報が次のプレイに残らないようにする
        if (tracker != null)
            tracker.ResetTracking();

        // ArenaRotatorが存在する場合は回転する箱の向きをリセットする
        // プレイ中に箱が回転していた場合でも、プレイ開始時の向きへ戻す
        if (ArenaRotator.Instance != null)
            ArenaRotator.Instance.ResetRotation();    // 箱の向きを戻す

        // FrameBossが存在する場合はボスの状態を初期状態へ戻す
        // プレイ中に変化したボスの状態が次のプレイに引き継がれないようにする
        if (FrameBoss.Instance != null)
            FrameBoss.Instance.ResetState();          // ボスを初期状態へ

        // Transformの変更をPhysics2Dへ反映する
        // プレイヤーやアリーナの位置・回転をリセットした直後でも、物理演算側が最新の状態を認識できるようにする
        Physics2D.SyncTransforms();
    }


    // ステージ上に配置されているオブジェクトをすべて削除する
    // 保存しているスナップショットも消去し、配置されている状態を完全にリセットする
    public void ClearAllPlaced()
    {
        // 保存しているステージ状態をすべて削除する
        // 以前の配置状態を復元できないようにして、現在のステージを新しい状態として扱う
        snapshot.Clear();

        // ステージオブジェクトを配置しているすべての親を取得して処理する
        // levelRootやdynamicRootなど、複数の親に配置されたオブジェクトをまとめて削除する
        foreach (var root in Roots())
        {
            // 親オブジェクトの子を後ろから順番に削除する
            // 子オブジェクトを削除するとchildCountやインデックスが変化するため、後ろから削除する
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }
    }

    // 配置したオブジェクトが、回転する箱の内側からはみ出さないように位置を調整する
    // 箱が存在しないシーンでは何もせず、そのまま処理を終了する
    void ClampInside(LevelObject lo)
    {
        // 回転する箱を管理しているArenaRotatorを取得する
        // 箱の内側の範囲を取得するために使用する
        var arena = ArenaRotator.Instance;

        // 箱または調整対象のオブジェクトが存在しない場合は処理しない
        // 箱がないシーンでもエラーにならないようにする
        if (arena == null || lo == null) return;

        // 箱の内側として使用できる範囲を取得する
        // オブジェクトがこの範囲からはみ出していないかを確認するために使用する
        Rect inner = arena.InnerBounds;

        // LevelObjectのワールド座標上での範囲を取得する
        // オブジェクトの位置だけではなく、実際の大きさを含めて箱からはみ出しているか確認する
        Bounds b = WorldBounds(lo);

        // オブジェクトを箱の内側へ移動させるための移動量を初期化する
        // X方向とY方向のどちらにもはみ出していない場合はVector3.zeroのままになる
        Vector3 shift = Vector3.zero;

        // オブジェクトの左端が箱の左端より外側にはみ出している場合に処理する
        if (b.min.x < inner.xMin)
            // 箱の左端にぴったり合うように、右方向へ移動する量を計算する
            shift.x = inner.xMin - b.min.x;

        // オブジェクトの右端が箱の右端より外側にはみ出している場合に処理する
        else if (b.max.x > inner.xMax)
            // 箱の右端にぴったり合うように、左方向へ移動する量を計算する
            shift.x = inner.xMax - b.max.x;

        // オブジェクトの下端が箱の下端より外側にはみ出している場合に処理する
        if (b.min.y < inner.yMin)
            // 箱の下端にぴったり合うように、上方向へ移動する量を計算する
            shift.y = inner.yMin - b.min.y;

        // オブジェクトの上端が箱の上端より外側にはみ出している場合に処理する
        else if (b.max.y > inner.yMax)
            // 箱の上端にぴったり合うように、下方向へ移動する量を計算する
            shift.y = inner.yMax - b.max.y;

        // オブジェクトが箱からはみ出している場合に処理する
        // shiftが0ではない場合は、X方向またはY方向のどちらかを移動する必要がある
        if (shift != Vector3.zero)
        {
            // 計算した移動量をオブジェクトの位置に加える
            // 箱の内側に収まる位置までオブジェクトを移動させる
            lo.transform.position += shift;

            // 変更したTransformの情報をPhysics2Dへ反映する
            // 移動後の位置を物理演算側でも正しく認識できるようにする
            Physics2D.SyncTransforms();
        }
    }


    // オブジェクトの回転を考慮した、画面に平行な四角形の外形を取得する
    // オブジェクトが回転している場合でも、その四隅を囲むBoundsを計算して
    // 箱の内側に収まっているかどうかを判定できるようにする
    Bounds WorldBounds(LevelObject lo)
    {
        // オブジェクトのローカル座標上での中心と半分の大きさを取得する
        // c：オブジェクトの中心位置、h：幅と高さの半分を表す
        GetLocalRect(lo, out var c, out var h);

        // LevelObjectのTransformを取得する
        // ローカル座標からワールド座標へ変換するときに使用する
        var t = lo.transform;

        // オブジェクトの左下の角をワールド座標へ変換してBoundsを作成する
        // この時点では1点だけを登録し、あとから残り3つの角を追加して外形を決定する
        var b = new Bounds(
            t.TransformPoint(c + new Vector2(-h.x, -h.y)),
            Vector3.zero
        );

        // オブジェクトの右下の角をワールド座標へ変換してBoundsに含める
        // TransformPointを使うことで、オブジェクトの位置や回転などを反映した座標になる
        b.Encapsulate(t.TransformPoint(c + new Vector2(h.x, -h.y)));

        // オブジェクトの右上の角をワールド座標へ変換してBoundsに含める
        // 4つの角をすべて含めることで、回転したオブジェクト全体を囲む範囲を計算する
        b.Encapsulate(t.TransformPoint(c + new Vector2(h.x, h.y)));

        // オブジェクトの左上の角をワールド座標へ変換してBoundsに含める
        // 最後の角まで含めることで、オブジェクト全体を囲むBoundsが完成する
        b.Encapsulate(t.TransformPoint(c + new Vector2(-h.x, h.y)));

        // 計算したワールド座標上の外形を返す
        // ClampInsideなどで箱の内側からはみ出しているかを判定するために使用する
        return b;
    }


    // キーボード入力を受け取り、アイテム選択やカメラ移動、オブジェクト操作などを行う
    // 編集モード中に毎フレーム呼び出され、現在の入力状態に応じて編集操作を処理する
    void HandleKeys(Keyboard kb)
    {
        // Qキーが押された瞬間に、1つ前のアイテムを選択する
        // 選択中のアイテム番号を1つ減らして、前のアイテムへ切り替える
        if (kb.qKey.wasPressedThisFrame)
            SelectItem(selectedItem - 1);

        // Eキーが押された瞬間に、1つ次のアイテムを選択する
        // 選択中のアイテム番号を1つ増やして、次のアイテムへ切り替える
        if (kb.eKey.wasPressedThisFrame)
            SelectItem(selectedItem + 1);

        // 1～9キーで対応するアイテムを直接選択する
        // 登録されているアイテム数が9個を超えている場合でも、最初の9個までをショートカットとして使用する
        for (int i = 0; i < items.Count && i < 9; i++)
        {
            // 対応する数字キーが押された瞬間に、その番号のアイテムを選択する
            // Digit1を基準にして、iの値に応じて1～9キーを判定する
            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                SelectItem(i);
        }

        // カメラやオブジェクトを移動させる方向を初期化する
        // 入力された方向だけX方向・Y方向に値を加えていく
        Vector2 move = Vector2.zero;

        // Aキーまたは左矢印キーが押されている間は左方向へ移動する
        // X方向を-1にすることで左方向の移動入力を作る
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            move.x -= 1;

        // Dキーまたは右矢印キーが押されている間は右方向へ移動する
        // X方向を+1にすることで右方向の移動入力を作る
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            move.x += 1;

        // Sキーまたは下矢印キーが押されている間は下方向へ移動する
        // Y方向を-1にすることで下方向の移動入力を作る
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            move.y -= 1;

        // Wキーまたは上矢印キーが押されている間は上方向へ移動する
        // Y方向を+1にすることで上方向の移動入力を作る
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            move.y += 1;

        // 回転する箱が存在しない場合だけカメラを移動する
        // 箱があるシーンではカメラを固定し、箱の回転に合わせた画面を維持する
        if (ArenaRotator.Instance == null)
            // 箱のあるシーンでは、カメラは固定
            cam.transform.position +=
                // 入力方向にカメラ移動速度と実時間を掛けて移動する
                // Time.timeScaleの影響を受けず、編集モード中でも一定速度でカメラを動かせるようにする
                (Vector3)(move * cameraPanSpeed * Time.unscaledDeltaTime);

        // Gキーが押された瞬間にグリッドへのスナップ機能を切り替える
        // trueならオブジェクトをグリッドに合わせ、falseなら自由に配置できるようにする
        if (kb.gKey.wasPressedThisFrame)
            snap = !snap;

        // Escapeキーが押された瞬間に現在の選択を解除する
        // 選択中のオブジェクトがなくなり、編集対象をリセットする
        if (kb.escapeKey.wasPressedThisFrame)
            Select(null);

        // 現在選択しているオブジェクトが存在しない場合は、ここから先の操作を行わない
        // 回転や削除などの操作対象がないため、処理を終了する
        if (current == null) return;

        // Rキーが押された瞬間に、現在のアイテムが回転可能なら回転させる
        // canRotateで回転できないアイテムを誤って回転させないようにする
        if (kb.rKey.wasPressedThisFrame && ItemOf(current).canRotate)
        {
            // 回転方向を決める
            // Shiftキーを押している場合は逆方向、押していない場合は通常方向に回転する
            float dir = kb.shiftKey.isPressed ? -1f : 1f;

            // 現在選択しているオブジェクトをZ軸方向に回転させる
            // rotateStepで指定された角度を1回の入力で回転させる
            current.transform.Rotate(
                0f,
                0f,
                rotateStep * dir
            );

            // 回転後にオブジェクトが箱の外側へはみ出していないか確認する
            // はみ出している場合は箱の内側へ収まるように位置を調整する
            ClampInside(current);

            // 回転後のTransform情報をPhysics2Dへ反映する
            // 変更された位置や回転を物理演算側でも正しく認識できるようにする
            Physics2D.SyncTransforms();
        }

        // Deleteキーが押された瞬間に、現在選択しているオブジェクトを削除する
        // 編集中に不要になったステージオブジェクトを取り除くために使用する
        if (kb.deleteKey.wasPressedThisFrame)
            Remove(current);
    }


    // マウス入力を受け取り、オブジェクトの選択・配置・移動・サイズ変更・削除などを行う
    // 編集モード中に毎フレーム呼び出され、マウスの状態に応じて編集操作を処理する
    void HandleMouse(Keyboard kb)
    {
        // 現在のマウス入力を取得する
        // マウスの位置や左クリック・右クリックなどの状態を確認するために使用する
        var mouse = Mouse.current;

        // マウスの画面上の位置をステージ上のワールド座標へ変換する
        // オブジェクトを配置したり、クリックした場所にあるオブジェクトを調べたりするために使用する
        Vector2 world = cam.ScreenToWorldPoint(
            mouse.position.ReadValue()
        );

        // マウスカーソルがUIの上にあるかを確認する
        // UIを操作しているときに、ステージ上のオブジェクトまで選択・配置されないようにする
        bool overUI =
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();

        // Nキーが押された瞬間に、UIの上にいなければオブジェクトを生成して選択する
        // マウス位置に現在選択しているアイテムを新しく配置する
        if (kb.nKey.wasPressedThisFrame && !overUI)
            Select(Spawn(world));

        // マウス左ボタンが押された瞬間に、UIの上にいなければ処理する
        // 左クリックした場所に応じて、サイズ変更・オブジェクト移動・選択解除などを行う
        if (mouse.leftButton.wasPressedThisFrame && !overUI)
        {
            // クリックした場所がサイズ変更用ハンドルの上にあるか確認する
            // ハンドルをクリックした場合は、オブジェクトのサイズ変更操作を開始する
            if (TryHandleAt(world, out var dir))
            {
                // クリックしたハンドルの方向を保存する
                // どの方向からサイズを変更するかをResizeByHandleで使用する
                handle = dir;

                // 現在のドラッグ操作をサイズ変更に設定する
                mode = DragMode.Resize;
            }
            else
            {
                // クリックした場所にあるステージオブジェクトを取得する
                // オブジェクトを選択したり、移動対象にしたりするために使用する
                var hit = PickAt(world);

                // クリックしたオブジェクトを選択する
                // 何もない場所をクリックした場合は選択を解除する
                Select(hit);

                // オブジェクトが見つかった場合は移動操作を開始する
                if (hit != null)
                {
                    // 現在のドラッグ操作を移動に設定する
                    mode = DragMode.Move;

                    // マウスを押した位置とオブジェクトの位置のずれを保存する
                    // ドラッグ中にマウス位置へオブジェクトの中心が移動してしまわないようにする
                    dragOffset = (Vector2)hit.transform.position - world;
                }
                else
                {
                    // オブジェクトがない場所をクリックした場合は、操作状態を解除する
                    // 移動やサイズ変更を行わない状態にする
                    mode = DragMode.None;
                }
            }
        }

        // マウス左ボタンを押している間、現在選択しているオブジェクトを操作する
        // Moveなら移動、Resizeならサイズ変更を毎フレーム行う
        if (mouse.leftButton.isPressed && current != null)
        {
            // 現在の操作が移動の場合に処理する
            if (mode == DragMode.Move)
            {
                // マウス位置とドラッグ開始時のずれを加えた位置へオブジェクトを移動する
                // クリックした位置とオブジェクトの位置関係を維持したままドラッグできるようにする
                current.transform.position = ToPos(world + dragOffset);

                // 移動後にオブジェクトが箱の外側へはみ出していないか確認する
                // はみ出している場合は箱の内側へ収まるように位置を調整する
                ClampInside(current);

                // 変更したTransform情報をPhysics2Dへ反映する
                // 移動後の位置を物理演算側でも正しく認識できるようにする
                Physics2D.SyncTransforms();
            }

            // 現在の操作がサイズ変更の場合に処理する
            else if (mode == DragMode.Resize)
            {
                // マウス位置に合わせて選択中のオブジェクトのサイズを変更する
                // 保存しておいたハンドルの方向を使って、どの方向へサイズを変更するか決定する
                ResizeByHandle(world);
            }
        }

        // マウス左ボタンを離した瞬間に、現在のドラッグ操作を終了する
        // 次のクリックまで移動やサイズ変更を行わない状態に戻す
        if (mouse.leftButton.wasReleasedThisFrame)
            mode = DragMode.None;

        // マウス右ボタンが押された瞬間に、UIの上にいなければ処理する
        // 右クリックした場所にあるオブジェクトを削除するために使用する
        if (mouse.rightButton.wasPressedThisFrame && !overUI)
        {
            // 右クリックした場所にあるステージオブジェクトを取得する
            // 削除対象となるオブジェクトを探すために使用する
            var hit = PickAt(world);

            // オブジェクトが見つかった場合は削除する
            // 何もない場所を右クリックした場合は何もしない
            if (hit != null)
                Remove(hit);
        }
    }


    // 選択中のオブジェクトを、クリックしたサイズ変更用ハンドルの方向に合わせてサイズ変更する
    // マウスの位置から新しい幅・高さを計算し、オブジェクトの回転やハンドルの位置を維持しながら大きさを変更する
    void ResizeByHandle(Vector2 mouse)
    {
        // 回転する箱が存在する場合に処理する
        // サイズ変更中にマウスが箱の外側へ出ても、箱の内側の範囲だけでサイズを変更できるようにする
        if (ArenaRotator.Instance != null)
        {
            // 箱の内側でオブジェクトを配置できる範囲を取得する
            // マウス位置をこの範囲内に制限するために使用する
            Rect r = ArenaRotator.Instance.InnerBounds;

            // マウス位置を箱の内側の範囲に収める
            // X方向とY方向それぞれで、箱の外側へ出ないように最大・最小値を制限する
            mouse = new Vector2(
                Mathf.Clamp(mouse.x, r.xMin, r.xMax),
                Mathf.Clamp(mouse.y, r.yMin, r.yMax)
            );
        }

        // 現在選択しているオブジェクトのTransformを取得する
        // 位置・回転・大きさを変更するときに使用する
        var t = current.transform;

        // オブジェクトのローカル座標上での右方向をワールド座標として取得する
        // オブジェクトが回転していても、正しい横方向にサイズを変更できるようにする
        Vector2 right = t.right;

        // オブジェクトのローカル座標上での上方向をワールド座標として取得する
        // オブジェクトが回転していても、正しい縦方向にサイズを変更できるようにする
        Vector2 up = t.up;

        // オブジェクトのローカル座標上での中心と半分の大きさを取得する
        // c：中心位置、h：幅と高さの半分を表し、実際のサイズ計算に使用する
        GetLocalRect(current, out var c, out var h);

        // 現在選択しているハンドルと反対側にある点を、ワールド座標で取得する
        // この点をサイズ変更中に動かさない基準点（アンカー）として使用する
        Vector2 anchor = t.TransformPoint(
            c + new Vector2(-handle.x * h.x, -handle.y * h.y)
        );

        // アンカーから現在のマウス位置までのベクトルを計算する
        // この距離から変更後の幅や高さを求める
        Vector2 v = mouse - anchor;

        // 現在のオブジェクトのローカルスケールを取得する
        // サイズ変更後のスケールを計算するために使用する
        var s = t.localScale;

        // 現在のオブジェクトの実際の幅を計算する
        // ローカルサイズの半分にスケールを掛けて、左右両方の分を2倍する
        float newW = 2f * h.x * s.x;

        // 現在のオブジェクトの実際の高さを計算する
        // ローカルサイズの半分にスケールを掛けて、上下両方の分を2倍する
        float newH = 2f * h.y * s.y;

        // 横方向のハンドルを操作している場合に幅を変更する
        if (handle.x != 0)
            // マウス位置をオブジェクトの右方向へ投影し、ハンドルの向きを考慮して幅を求める
            // SnapSizeを通すことで、設定されているサイズの刻みに合わせて変更する
            newW = SnapSize(Vector2.Dot(v, right) * handle.x);

        // 縦方向のハンドルを操作している場合に高さを変更する
        if (handle.y != 0)
            // マウス位置をオブジェクトの上方向へ投影し、ハンドルの向きを考慮して高さを求める
            // SnapSizeを通すことで、設定されているサイズの刻みに合わせて変更する
            newH = SnapSize(Vector2.Dot(v, up) * handle.y);

        // 現在のオブジェクトの中心位置をワールド座標で取得する
        // サイズ変更後に新しい中心位置を計算するために使用する
        Vector2 centerWorld = t.TransformPoint(c);

        // 現在の中心からアンカーまでの相対的な位置を計算する
        // ハンドルを固定したまま中心位置を移動させるために使用する
        Vector2 rel = centerWorld - anchor;

        // 新しい中心位置を求めるためのX方向の距離を計算する
        // 横方向のハンドルを操作している場合は、新しい幅の半分を使用する
        // 横方向を操作していない場合は、現在の中心とアンカーの距離をそのまま使用する
        float dx = handle.x != 0
            ? handle.x * newW * 0.5f
            : Vector2.Dot(rel, right);

        // 新しい中心位置を求めるためのY方向の距離を計算する
        // 縦方向のハンドルを操作している場合は、新しい高さの半分を使用する
        // 縦方向を操作していない場合は、現在の中心とアンカーの距離をそのまま使用する
        float dy = handle.y != 0
            ? handle.y * newH * 0.5f
            : Vector2.Dot(rel, up);

        // アンカーからX方向・Y方向へ移動した位置を、新しい中心位置として計算する
        // オブジェクトが回転していても、正しい方向に中心を移動できるようにする
        Vector2 newCenter = anchor + right * dx + up * dy;

        // 新しい幅からX方向のローカルスケールを計算する
        // 元のローカル幅に対して、指定した新しい幅になるようにスケールを調整する
        s.x = newW / (2f * h.x);

        // 新しい高さからY方向のローカルスケールを計算する
        // 元のローカル高さに対して、指定した新しい高さになるようにスケールを調整する
        s.y = newH / (2f * h.y);

        // 計算した新しいスケールをオブジェクトへ反映する
        // ここで実際にオブジェクトの大きさが変更される
        t.localScale = s;

        // スケール変更後の中心位置から、Transformのピボット位置を計算する
        // オブジェクトの中心を新しい位置に合わせつつ、選択したハンドルの反対側を固定するために使用する
        Vector2 pivot =
            newCenter
            - right * (c.x * s.x)
            - up * (c.y * s.y);

        // 計算したピボット位置をオブジェクトのワールド座標として設定する
        // Z座標は現在の値を維持し、X・Yだけをサイズ変更に合わせて変更する
        t.position = new Vector3(
            pivot.x,
            pivot.y,
            t.position.z
        );

        // サイズ変更後にオブジェクトが箱の外側へはみ出していないか確認する
        // 箱からはみ出している場合は、箱の内側に収まるように位置を調整する
        ClampInside(current);

        // 変更したTransform情報をPhysics2Dへ反映する
        // サイズや位置の変更を物理演算側でも正しく認識できるようにする
        Physics2D.SyncTransforms();
    }


    // サイズ変更時の幅や高さを、設定した刻みに合わせて調整する
    // スナップが有効な場合は一定間隔ごとのサイズに揃え、最後に最小サイズを下回らないようにする
    float SnapSize(float w)
    {
        // グリッドへのスナップが有効な場合に処理する
        // サイズを自由に変更するのではなく、moveSnapで指定した間隔に揃える
        if (snap)
            // サイズをmoveSnapの倍数に丸める
            // 例えばmoveSnapが0.5なら、1.2は1.0、1.3は1.5のように調整される
            w = Mathf.Round(w / moveSnap) * moveSnap;

        // 計算したサイズが最小サイズを下回らないようにする
        // オブジェクトが小さくなりすぎて、ほとんど見えなくなったり操作できなくなったりするのを防ぐ
        return Mathf.Max(minSize, w);
    }


    // 現在選択しているオブジェクトの枠線とサイズ変更用ハンドルを更新する
    // オブジェクトの位置・回転・大きさに合わせて枠線を表示し、サイズ変更可能な場合はハンドルも表示する
    void UpdateFrame()
    {
        // 現在選択しているオブジェクトがない場合に処理する
        // 選択対象がない状態で枠線やハンドルを表示し続けないようにする
        if (current == null)
        {
            // 選択枠とハンドルを非表示にする
            HideFrame();
            return;
        }

        // カメラの表示サイズを基準に、選択枠やハンドルの大きさを計算する
        // カメラがズームしても画面上で見やすい太さになるようにする
        float unit = cam.orthographicSize * 0.02f;

        // 選択中オブジェクトのローカル座標上での中心と半分の大きさを取得する
        // 選択枠をオブジェクトの四隅に合わせて表示するために使用する
        GetLocalRect(current, out var c, out var h);

        // 選択中オブジェクトのTransformを取得する
        // ローカル座標上の四隅をワールド座標へ変換するために使用する
        var t = current.transform;

        // 選択枠のLineRendererを有効にする
        // 現在選択しているオブジェクトの周囲に枠線を表示する
        frameLine.enabled = true;

        // 選択枠の線の太さを設定する
        // カメラの表示サイズに合わせて、画面上で見やすい太さになるようにする
        frameLine.widthMultiplier = unit * 0.3f;

        // 選択枠の左下の角を設定する
        // オブジェクトの回転や位置を反映したワールド座標へ変換して配置する
        frameLine.SetPosition(
            0,
            t.TransformPoint(c + new Vector2(-h.x, -h.y))
        );

        // 選択枠の右下の角を設定する
        // 左下から右下へ線をつなげるための位置を設定する
        frameLine.SetPosition(
            1,
            t.TransformPoint(c + new Vector2(h.x, -h.y))
        );

        // 選択枠の右上の角を設定する
        // 右下から右上へ線をつなげるための位置を設定する
        frameLine.SetPosition(
            2,
            t.TransformPoint(c + new Vector2(h.x, h.y))
        );

        // 選択枠の左上の角を設定する
        // 右上から左上へ線をつなげるための位置を設定する
        frameLine.SetPosition(
            3,
            t.TransformPoint(c + new Vector2(-h.x, h.y))
        );

        // 現在のアイテムがサイズ変更可能かどうかを確認する
        // canResizeがtrueの場合だけ、8方向のサイズ変更ハンドルを表示する
        bool showHandles = ItemOf(current).canResize;

        // 8方向にあるサイズ変更用ハンドルを1つずつ更新する
        // 左下・下・右下・左・右・左上・上・右上の各ハンドルを処理する
        for (int i = 0; i < handleLines.Length; i++)
        {
            // 現在処理しているハンドルのLineRendererを取得する
            // このLineRendererを使ってサイズ変更用のハンドルを表示する
            var lr = handleLines[i];

            // サイズ変更可能なアイテムの場合だけハンドルを表示する
            // サイズ変更できないアイテムではハンドル自体を非表示にする
            lr.enabled = showHandles;

            // サイズ変更できない場合は、このハンドルの位置更新を行わない
            // 非表示のハンドルを無駄に計算しないようにする
            if (!showHandles) continue;

            // 現在のハンドルの方向から、ワールド座標上の位置を取得する
            // オブジェクトが移動・回転・サイズ変更されても正しい位置にハンドルを表示する
            Vector2 p = HandleWorldPos(HandleDirs[i]);

            // ハンドルの線の太さを設定する
            // 選択枠よりも太くして、マウスで操作しやすくする
            lr.widthMultiplier = unit * 2f;

            // ハンドルの左端の位置を設定する
            // ハンドルを短い横線として表示するために、中心位置から左へunit分ずらす
            lr.SetPosition(0, p + Vector2.left * unit);

            // ハンドルの右端の位置を設定する
            // 中心位置から右へunit分ずらし、サイズ変更用のハンドルを横線として表示する
            lr.SetPosition(1, p + Vector2.right * unit);
        }
    }


    // 選択中のオブジェクトを囲む枠線とサイズ変更用ハンドルをすべて非表示にする
    // オブジェクトの選択を解除したときや、プレイモードへ切り替えるときなどに使用する
    void HideFrame()
    {
        // 選択枠のLineRendererが存在する場合に処理する
        // 生成されていない状態でアクセスしてエラーになるのを防ぐ
        if (frameLine != null)
            // 選択枠の表示を無効にする
            // 現在選択しているオブジェクトの周囲に表示されている枠線を非表示にする
            frameLine.enabled = false;

        // 8方向のサイズ変更用ハンドルを1つずつ処理する
        // 各ハンドルに対応したLineRendererをまとめて非表示にする
        foreach (var lr in handleLines)
        {
            // ハンドルのLineRendererが存在する場合に処理する
            // まだ生成されていないハンドルがあってもエラーにならないようにする
            if (lr != null)
                // サイズ変更用ハンドルの表示を無効にする
                // 選択枠と一緒にすべてのハンドルを画面から非表示にする
                lr.enabled = false;
        }
    }

    // 選択枠やサイズ変更用ハンドルに使用するLineRendererを作成する
    // mat：線の描画に使用するマテリアル
    // points：LineRendererに設定する頂点の数
    // loop：trueなら最後の頂点と最初の頂点をつないで閉じた線にする
    // col：線の色
    LineRenderer MakeLine(Material mat, int points, bool loop, Color col)
    {
        // LineRendererを配置するためのGameObjectを新しく作成する
        // 選択枠やハンドルを個別のオブジェクトとして管理するために使用する
        var go = new GameObject("EditorLine");

        // 作成したLineRenderer用のGameObjectを、このLevelEditorの子オブジェクトにする
        // LevelEditorが破棄されたときに、一緒に削除されるようにする
        // falseにすることで、親を変更してもワールド座標を維持するのではなく、親のローカル座標として設定する
        go.transform.SetParent(transform, false);

        // GameObjectにLineRendererコンポーネントを追加する
        // 選択枠やハンドルなどの線を実際に画面へ描画するために使用する
        var lr = go.AddComponent<LineRenderer>();

        // LineRendererの頂点位置をワールド座標として扱う
        // 選択中のオブジェクトの位置や回転に合わせて、ワールド座標をそのまま指定できるようにする
        lr.useWorldSpace = true;

        // 線を閉じるかどうかを設定する
        // loopがtrueの場合は最後の頂点と最初の頂点が自動的につながり、選択枠のような閉じた線を作れる
        lr.loop = loop;

        // LineRendererで使用する頂点の数を設定する
        // 選択枠なら4点、ハンドルなら2点など、用途に合わせて必要な点数を指定する
        lr.positionCount = points;

        // 線の描画に使用するマテリアルを設定する
        // 渡されたマテリアルを使ってLineRendererを描画する
        lr.material = mat;

        // 線の始点の色を設定する
        // 線全体を同じ色で表示するために、始点と終点の両方へ同じ色を設定する
        lr.startColor = col;

        // 線の終点の色を設定する
        // startColorと同じ色にすることで、線の途中で色が変化しないようにする
        lr.endColor = col;

        // 線の描画順を設定する
        // 数値を大きくすることで、ステージ上のオブジェクトより手前に選択枠やハンドルを表示しやすくする
        lr.sortingOrder = 1000;

        // 作成して初期設定したLineRendererを呼び出し元へ返す
        // Awakeなどで選択枠やハンドルを管理するために使用する
        return lr;
    }


    // 指定された方向にあるサイズ変更用ハンドルのワールド座標を取得する
    // dir：ハンドルの方向を表すVector2
    //      Xが-1なら左、0なら中央、1なら右を表し、
    //      Yが-1なら下、0なら中央、1なら上を表す
    // オブジェクトが移動・回転・サイズ変更されていても、現在の形状に合わせて正しい位置を計算する
    Vector2 HandleWorldPos(Vector2 dir)
    {
        // 選択中のオブジェクトのローカル座標上での中心と半分の大きさを取得する
        // c：オブジェクトの中心位置、h：幅と高さの半分を表す
        // ハンドルをオブジェクトの外周に配置するために使用する
        GetLocalRect(current, out var c, out var h);

        // ローカル座標上で計算したハンドル位置をワールド座標へ変換して返す
        // dirのX・Y方向にhを掛けることで、オブジェクトの中心から各方向へ移動した位置を求める
        // TransformPointを使うことで、オブジェクトの位置や回転も反映されたワールド座標になる
        return current.transform.TransformPoint(
            c + new Vector2(dir.x * h.x, dir.y * h.y)
        );
    }

    // 指定されたワールド座標が、現在表示しているサイズ変更用ハンドルの上にあるか確認する
    // world：マウスカーソルのワールド座標
    // dir：マウスが重なっているハンドルの方向を返す
    // ハンドルの上にある場合はtrue、ない場合はfalseを返す
    bool TryHandleAt(Vector2 world, out Vector2 dir)
    {
        // ハンドルの方向を初期化する
        // どのハンドルにも重なっていない場合にVector2.zeroが返るようにする
        dir = Vector2.zero;

        // 選択中のオブジェクトが存在しない場合、またはサイズ変更できないアイテムの場合は処理しない
        // サイズ変更の対象がない状態でハンドルを判定する必要がないため、falseを返す
        if (current == null || !ItemOf(current).canResize)
            return false;

        // マウスがハンドルに重なっていると判定する範囲を計算する
        // カメラの表示サイズを基準にすることで、ズームしてもハンドルをクリックしやすい大きさを維持する
        // 1.8fを掛けることで、実際のハンドル線より少し広い範囲をクリック判定として使用する
        float hit = cam.orthographicSize * 0.02f * 1.8f;

        // 8方向のサイズ変更用ハンドルを1つずつ確認する
        // 左下・下・右下・左・右・左上・上・右上の順に判定する
        foreach (var d in HandleDirs)
        {
            // 現在確認しているハンドルのワールド座標を取得する
            // オブジェクトの位置・回転・大きさを反映した実際のハンドル位置になる
            Vector2 p = HandleWorldPos(d);

            // マウス位置とハンドル位置のX方向・Y方向の距離を確認する
            // 両方の距離がhit以内なら、マウスがハンドルの判定範囲内にあると判断する
            if (Mathf.Abs(world.x - p.x) <= hit &&
                Mathf.Abs(world.y - p.y) <= hit)
            {
                // マウスが重なっているハンドルの方向をdirへ保存する
                // ResizeByHandleで、どの方向のハンドルを操作しているかを判断するために使用する
                dir = d;

                // ハンドルが見つかったことを呼び出し元へ通知する
                return true;
            }
        }

        // すべてのハンドルを確認しても見つからなかった場合はfalseを返す
        // マウスがどのサイズ変更用ハンドルにも重なっていないことを表す
        return false;
    }


    // LevelObjectの見た目やコライダーを基準に、ローカル座標上での中心位置と半分の大きさを取得する
    // lo：外形を取得したいLevelObject
    // center：オブジェクトのローカル座標上での中心位置を返す
    // half：オブジェクトの幅・高さの半分を返す
    // SpriteRendererがある場合はスプライトの大きさを優先し、ない場合はBoxCollider2Dを使用する
    void GetLocalRect(LevelObject lo, out Vector2 center, out Vector2 half)
    {
        // LevelObjectに付いているSpriteRendererを取得する
        // スプライトの実際の表示範囲を、選択枠やサイズ変更の基準として使用するために取得する
        var sr = lo.GetComponent<SpriteRenderer>();

        // SpriteRendererとスプライトの両方が存在する場合に処理する
        // 見た目として表示されているスプライトの大きさを優先して外形を決定する
        if (sr != null && sr.sprite != null)
        {
            // スプライトのローカル座標上でのBoundsを取得する
            // sprite.boundsには、スプライトの中心位置と外形の大きさが保存されている
            var b = sr.sprite.bounds;

            // スプライトの中心位置を取得する
            // 選択枠やハンドルをスプライトの中心に合わせるために使用する
            center = b.center;

            // スプライトの外形の半分の大きさを取得する
            // Xが幅の半分、Yが高さの半分を表し、四隅の位置を計算するときに使用する
            half = b.extents;

            // スプライトの情報を使って外形を取得できたので、ここで処理を終了する
            return;
        }

        // SpriteRendererがない場合は、BoxCollider2Dを取得する
        // スプライトが設定されていないオブジェクトでも、コライダーを基準に外形を取得できるようにする
        var box = lo.GetComponent<BoxCollider2D>();

        // BoxCollider2Dが存在する場合に処理する
        // コライダーの位置と大きさを、選択枠やサイズ変更の基準として使用する
        if (box != null)
        {
            // BoxCollider2Dのローカル座標上での中心位置を取得する
            // offsetには、オブジェクトのTransform位置からのコライダー中心のずれが保存されている
            center = box.offset;

            // BoxCollider2Dのサイズを2で割り、幅・高さの半分を取得する
            // サイズ変更や四隅の位置計算で使用するため、半分の大きさに変換する
            half = box.size * 0.5f;

            // コライダーの情報を使って外形を取得できたので、ここで処理を終了する
            return;
        }

        // SpriteRendererもBoxCollider2Dも存在しない場合は、標準の外形を使用する
        // 外形を取得できないオブジェクトでも、選択枠などを表示できるようにする
        center = Vector2.zero;

        // 幅1、高さ1の四角形を基準とするため、半分の大きさを0.5に設定する
        // これにより、外形が取得できない場合でも最低限のサイズで処理を続けられる
        half = Vector2.one * 0.5f;
    }


    // LevelObjectに対応するItem情報を取得する
    // lo：Item情報を取得したいLevelObject
    // itemIndexがitemsの範囲外になっている場合でも、配列外アクセスが発生しないようにする
    Item ItemOf(LevelObject lo)
    {
        // LevelObjectのitemIndexをitemsの有効な範囲に収めて、対応するItemを取得する
        // Mathf.Clampを使うことで、itemIndexが0未満の場合は0、最大値を超えた場合は最後のItemを使用する
        return items[Mathf.Clamp(lo.itemIndex, 0, items.Count - 1)];
    }

    // 現在選択しているアイテムを変更する
    // index：選択したいアイテムの番号
    // 先頭や最後を超えた場合は、反対側へ回り込んで選択できるようにする
    void SelectItem(int index)
    {
        // アイテムが1つも登録されていない場合は処理しない
        // items.Countが0のときにitemsへアクセスするとエラーになるため、先に処理を終了する
        if (items.Count == 0) return;

        // 指定された番号をitemsの範囲内に収める
        // 例えば最後のアイテムの次を選ぶと0番へ戻り、0番より前を選ぶと最後のアイテムへ移動する
        selectedItem = (index + items.Count) % items.Count;
    }

    // オブジェクトを配置する位置を、箱の内側やグリッドに合わせて調整する
    // p：配置したいワールド座標
    // 箱が存在する場合は箱の内側に収め、スナップが有効な場合はグリッドの位置に合わせる
    Vector3 ToPos(Vector2 p)
    {
        // 回転する箱が存在する場合に処理する
        // 箱があるステージでは、オブジェクトを箱の外側へ配置できないようにする
        if (ArenaRotator.Instance != null)
        {
            // 箱の内側として使用できる範囲を取得する
            // オブジェクトを配置できるX方向・Y方向の範囲を決めるために使用する
            Rect inner = ArenaRotator.Instance.InnerBounds;

            // X座標を箱の内側の範囲に制限する
            // 箱の左端より外側なら左端、右端より外側なら右端の位置に調整する
            p.x = Mathf.Clamp(p.x, inner.xMin, inner.xMax);

            // Y座標を箱の内側の範囲に制限する
            // 箱の下端より外側なら下端、上端より外側なら上端の位置に調整する
            p.y = Mathf.Clamp(p.y, inner.yMin, inner.yMax);
        }

        // グリッドへのスナップが有効な場合に処理する
        // オブジェクトを自由な位置ではなく、一定間隔のグリッド上へ配置できるようにする
        if (snap)
        {
            // X座標をmoveSnapの間隔に合わせて丸める
            // 例えばmoveSnapが0.5なら、1.2は1.0、1.3は1.5のように調整される
            p.x = Mathf.Round(p.x / moveSnap) * moveSnap;

            // Y座標をmoveSnapの間隔に合わせて丸める
            // X座標と同じように、指定したグリッド間隔に位置を揃える
            p.y = Mathf.Round(p.y / moveSnap) * moveSnap;
        }

        // 調整したX・Y座標からワールド座標を作成して返す
        // Z座標は2Dステージ上で使用するため0に固定する
        return new Vector3(p.x, p.y, 0f);
    }


    // 現在選択しているアイテムを、指定したワールド座標へ配置する
    // world：オブジェクトを配置したいワールド座標
    // プレイヤーが選択されている場合は新しく生成せず、既存のプレイヤーを移動する
    // 通常のアイテムの場合はPrefabから新しいオブジェクトを生成してLevelObjectとして登録する
    LevelObject Spawn(Vector2 world)
    {
        // アイテムが1つも登録されていない場合は何も生成しない
        // selectedItemからアイテムを取得できないため、先に処理を終了する
        if (items.Count == 0) return null;

        // 現在選択しているアイテムの情報を取得する
        // どのPrefabを生成するか、プレイヤーかどうかなどを確認するために使用する
        var item = items[selectedItem];

        // 現在選択しているアイテムがプレイヤーの場合に処理する
        // プレイヤーはステージ上に1人だけ存在するため、新しいプレイヤーを生成せず既存のプレイヤーを移動する
        if (item.isPlayer)
            // 既存のプレイヤーを指定した位置へ移動し、そのLevelObjectを返す
            return MovePlayer(world);

        // 配置するPrefabが設定されていない場合は何も生成しない
        // InstantiateするPrefabがない状態で生成処理を行うとエラーになるため、先に処理を終了する
        if (item.prefab == null)
            return null;

        // 選択中のアイテムのPrefabを新しく生成する
        // ToPosで配置位置を箱の内側やグリッドに合わせ、
        // Quaternion.identityで回転していない状態から生成する
        // RootForでPrefabの種類に応じたステージの親オブジェクトへ配置する
        var go = Instantiate(
            item.prefab,
            ToPos(world),
            Quaternion.identity,
            RootFor(item.prefab)
        );

        // 生成したGameObjectにLevelObjectを追加する
        // LevelEditorで配置したオブジェクトとして管理できるようにする
        var lo = go.AddComponent<LevelObject>();

        // 生成したLevelObjectに、現在選択しているアイテムの番号を設定する
        // 後からこのオブジェクトがどの種類のアイテムなのかを識別するために使用する
        lo.itemIndex = selectedItem;

        // 生成したオブジェクトが箱の外側へはみ出していないか確認する
        // はみ出している場合は箱の内側へ収まるように位置を調整する
        ClampInside(lo);

        // 変更したTransform情報をPhysics2Dへ反映する
        // 生成直後の位置や大きさを物理演算側でも正しく認識できるようにする
        Physics2D.SyncTransforms();

        // 生成してLevelObjectとして登録したオブジェクトを返す
        // 呼び出し元で生成したオブジェクトを選択対象などとして使用するために返す
        return lo;
    }

    // 既に存在しているプレイヤーを指定したワールド座標へ移動する
    // world：プレイヤーを移動させたいワールド座標
    // プレイヤーはステージ上に1人だけ存在するため、新しく生成せず既存のplayerLoを移動する
    LevelObject MovePlayer(Vector2 world)
    {
        // プレイヤーのLevelObjectが設定されていない場合は何もしない
        // 移動させる対象が存在しない状態で処理を続けないようにする
        if (playerLo == null) return null;

        // プレイヤーの位置を指定された位置へ移動する
        // ToPosを通すことで、箱の内側やグリッドに合わせた位置へ調整してから配置する
        playerLo.transform.position = ToPos(world);

        // プレイヤーが箱の外側へはみ出していないか確認する
        // プレイヤーの大きさも考慮して、箱の内側へ収まるように位置を調整する
        ClampInside(playerLo);

        // プレイヤーに付いているRigidbody2Dを取得する
        // プレイヤーを移動した際に、以前の移動速度が残らないようにするために使用する
        var rb = playerLo.GetComponent<Rigidbody2D>();

        // Rigidbody2Dが存在する場合に処理する
        // Rigidbody2Dを使用していないプレイヤーでもエラーにならないようにする
        if (rb != null)
            // プレイヤーの移動速度を0にする
            // 編集中にプレイヤーを配置し直したあと、以前の速度で動き出さないようにする
            rb.linearVelocity = Vector2.zero;

        // 変更したTransform情報をPhysics2Dへ反映する
        // プレイヤーの移動後の位置を物理演算側でもすぐに認識できるようにする
        Physics2D.SyncTransforms();

        // 移動したプレイヤーのLevelObjectを返す
        // Spawnから呼び出された場合に、移動後のプレイヤーを選択対象として使用できるようにする
        return playerLo;
    }

    

    // パレットで選択したアイテムを、指定したワールド座標へ配置して選択状態にする
    // itemIndex：LevelEditorに登録されている配置したいアイテムの番号
    // world：アイテムを配置するワールド座標
    // 配置したLevelObjectを返す
    public LevelObject SpawnFromPalette(int itemIndex, Vector2 world)
    {
        // 指定されたアイテムを現在の選択アイテムとして設定する
        // パレットからアイテムを選択したとき、そのアイテムをSpawnで配置できるようにする
        SelectItem(itemIndex);

        // 現在選択しているアイテムを指定したワールド座標へ配置する
        // 通常のアイテムならPrefabから生成し、プレイヤーなら既存のプレイヤーを移動する
        var lo = Spawn(world);

        // 配置したオブジェクトを現在の選択対象にする
        // パレットから配置した直後に、そのオブジェクトを移動・回転・サイズ変更などの編集対象として扱えるようにする
        Select(lo);

        // 配置して選択状態にしたLevelObjectを返す
        // パレット側で配置したオブジェクトを取得して、その後の処理に使用できるようにする
        return lo;
    }


    // 指定したワールド座標にあるLevelObjectを取得する
    // world：マウスクリックしたワールド座標
    // コライダーが付いていないオブジェクトでも、表示範囲の枠内をクリックすれば選択できるようにする
    // 複数のオブジェクトが重なっている場合は、最も小さいオブジェクトを選択する
    LevelObject PickAt(Vector2 world)
    {
        // 現在見つかっている選択候補を初期化する
        // まだ対象が見つかっていない状態ではnullにしておく
        LevelObject best = null;

        // 選択候補の中で最も小さい面積を保存する
        // 最初はどのオブジェクトよりも大きな値にして、最初に見つかったオブジェクトを候補にできるようにする
        float bestArea = float.MaxValue;

        // シーン内に存在するすべてのLevelObjectを取得して1つずつ確認する
        // コライダーがなくてもLevelObjectとして登録されているオブジェクトをすべて選択候補にする
        foreach (var lo in FindObjectsByType<LevelObject>(FindObjectsSortMode.None))
        {
            // オブジェクトのローカル座標上での中心と半分の大きさを取得する
            // スプライトやBoxCollider2Dを基準に、クリック判定を行う範囲を決める
            GetLocalRect(lo, out var c, out var h);

            // クリックしたワールド座標を、オブジェクトのローカル座標へ変換する
            // オブジェクトが移動・回転・拡大縮小されていても、オブジェクト自身を基準にクリック位置を判定できるようにする
            Vector2 local = lo.transform.InverseTransformPoint(world);

            // クリック位置がオブジェクトの外側にある場合は、このオブジェクトを選択候補から外す
            // X方向またはY方向のどちらか一方でも外形を超えていれば、枠の外をクリックしたことになる
            if (Mathf.Abs(local.x - c.x) > h.x ||
                Mathf.Abs(local.y - c.y) > h.y)
                continue;

            // オブジェクトの現在のローカルスケールを取得する
            // 実際に画面上で表示されている大きさを計算するために使用する
            var s = lo.transform.localScale;

            // オブジェクトの面積を計算する
            // hは幅・高さの半分なので、XとYを掛けた値にスケールの大きさを掛けて比較する
            // 小さいオブジェクトを優先することで、重なっているオブジェクトの中から細かい方を選択しやすくする
            float area = h.x * h.y * Mathf.Abs(s.x * s.y);

            // 今回のオブジェクトが、これまでに見つかった候補より小さい場合に処理する
            // 同じ場所に複数のオブジェクトがある場合でも、最も小さいものを選択対象にする
            if (area < bestArea)
            {
                // 現在のオブジェクトを選択候補として保存する
                best = lo;

                // 現在のオブジェクトの面積を、次の候補と比較するために保存する
                bestArea = area;
            }
        }

        // クリック位置にある最も小さいLevelObjectを返す
        // 該当するオブジェクトがなかった場合はnullを返す
        return best;
    }


    // 指定したLevelObjectを現在の選択対象として設定する
    // lo：選択するLevelObject
    // nullを渡した場合は選択を解除し、ドラッグ操作も終了する
    void Select(LevelObject lo)
    {
        // 指定されたLevelObjectを現在の選択対象として保存する
        // 以降の移動・回転・サイズ変更などは、このオブジェクトを対象にして行われる
        current = lo;

        // 選択対象が存在しない場合に処理する
        // 選択を解除したときに、前回のドラッグ操作が残らないようにする
        if (lo == null)
            // ドラッグ操作の状態を解除する
            // 次にオブジェクトを選択するまで、移動やサイズ変更を行わない状態に戻す
            mode = DragMode.None;
    }

    // 指定したLevelObjectをステージから削除する
    // lo：削除するLevelObject
    // プレイヤーはステージに1人だけ必要なため、削除できないようにする
    void Remove(LevelObject lo)
    {
        // 削除対象がプレイヤーの場合は処理しない
        // プレイヤーが存在しなくなるとステージを正常にプレイできなくなるため、削除を禁止する
        if (ItemOf(lo).isPlayer)
            return;      // プレイヤーは消せない

        // 削除するオブジェクトが現在選択中のオブジェクトの場合に処理する
        // 削除後もcurrentが破棄されたオブジェクトを参照し続けないようにする
        if (lo == current)
            // 現在の選択を解除する
            // 選択枠やドラッグ操作なども次の状態へリセットする
            Select(null);

        // LevelObjectが付いているGameObjectを破棄する
        // ステージ上からオブジェクトを完全に削除する
        Destroy(lo.gameObject);
    }

    // パレットからアイテム情報を取得するためのメソッド群
    // パレット側でアイテムの数やID、表示用アイコンを取得するときに使用する

    // LevelEditorに登録されているアイテムの数を取得する
    // パレットでアイテムボタンを生成するときなどに使用する
    public int ItemCount => items.Count;

    // 指定した番号のアイテムIDを取得する
    // i：LevelEditorに登録されているアイテムの番号
    // パレットのボタン名など、アイテムを識別するために使用する
    public string GetItemId(int i) => items[i].id;

    // 指定したアイテムのパレット表示用スプライトと色を取得する
    // i：LevelEditorに登録されているアイテムの番号
    // sprite：パレットに表示するスプライトを返す
    // color：パレットに表示するスプライトの色を返す
    // アイコンが設定されていればそれを優先し、未設定の場合はPrefabやプレイヤーのSpriteRendererから取得する
    public void GetItemIcon(int i, out Sprite sprite, out Color color)
    {
        // 指定された番号のアイテム情報を取得する
        // アイコンやPrefabなど、パレット表示に必要な情報を確認するために使用する
        var item = items[i];

        // 最初はスプライトを未設定にする
        // アイコンもPrefabのスプライトも取得できなかった場合は、nullのまま返す
        sprite = null;

        // 最初は白色を設定する
        // スプライトの色が取得できなかった場合でも、元の色をそのまま表示できるようにする
        color = Color.white;

        // アイテム専用のアイコンが設定されている場合に処理する
        // 専用アイコンがある場合はPrefabのスプライトよりもこちらを優先して使用する
        if (item.icon != null)
        {
            // 設定されているアイコンをパレット表示用スプライトとして使用する
            sprite = item.icon;

            // アイコンを使用するので、ここから先のPrefabやプレイヤーのスプライト取得は必要ない
            return;
        }

        // パレット表示に使用する元のGameObjectを決める
        // プレイヤー用アイテムならplayer、それ以外なら通常のprefabを使用する
        GameObject src = item.isPlayer ? player : item.prefab;

        // 表示元のGameObjectが存在する場合に処理する
        // 元となるオブジェクトがない状態でSpriteRendererを探さないようにする
        if (src != null)
        {
            // 元のGameObject自身と、その子オブジェクトからSpriteRendererを取得する
            // SpriteRendererが子オブジェクトに付いているPrefabでも、パレットに画像を表示できるようにする
            var sr = src.GetComponentInChildren<SpriteRenderer>();

            // SpriteRendererが見つかった場合に処理する
            // スプライトと、そのSpriteRendererに設定されている色をパレットへ反映する
            if (sr != null)
            {
                // SpriteRendererに設定されているスプライトを取得する
                // パレットのボタンにアイテムの見た目を表示するために使用する
                sprite = sr.sprite;

                // SpriteRendererに設定されている色を取得する
                // アイテムごとの色もパレット上で再現できるようにする
                color = sr.color;
            }
        }
    }

    // 編集モードやクリア時のメッセージを画面上に表示する
    // OnGUIはUnityのGUI描画タイミングで呼び出され、現在のゲーム状態に応じた説明やクリア表示を画面へ描画する
    void OnGUI()
    {
        // ステージがクリア済みの場合に処理する
        // プレイヤーがゴールに到達したときに、画面中央へCLEAR!を表示する
        if (showClear)
        {
            // クリア表示用のGUIスタイルを作成する
            // Unity標準のラベルスタイルを元にして、文字を大きく中央揃えにする
            var big = new GUIStyle(GUI.skin.label)
            {
                // クリア文字を大きく表示する
                // 通常の操作説明より目立つように72ピクセルの文字サイズを使用する
                fontSize = 72,

                // ラベル内の文字を中央に配置する
                // 画面中央に「CLEAR!」を表示するために使用する
                alignment = TextAnchor.MiddleCenter
            };

            // クリア表示の文字色を白に設定する
            // 背景の上でも見やすいように、目立つ色で表示する
            big.normal.textColor = Color.white;

            // 画面全体を使ってCLEAR!を表示する
            // Rectの中央に文字が配置されるように、画面全体の範囲を指定する
            GUI.Label(
                new Rect(0, 0, Screen.width, Screen.height),
                "CLEAR!",
                big
            );
        }

        // 編集モード中ではない場合に処理する
        // プレイ中は編集用の操作説明を表示せず、編集モードへ戻るための案内だけを表示する
        if (!IsEditing)
        {
            // プレイ中にTabキーで編集モードへ戻れることを画面上に表示する
            // プレイヤーが現在のモードと操作方法を確認できるようにする
            GUI.Label(
                new Rect(10, 70, 600, 24),
                "Tab: 編集に戻る"
            );

            // プレイモード中は編集用の表示をここで終了する
            // 以下の編集モード用の操作説明を表示しないようにする
            return;
        }

        // 現在選択しているアイテムの名前を取得する
        // アイテムが登録されている場合はIDを表示し、何も登録されていない場合は代わりの文字を表示する
        string name = items.Count > 0
            ? items[selectedItem].id
            : "(no items)";

        // 編集モード中の操作説明と現在の状態を画面上に表示する
        // 現在選択しているアイテム、スナップの状態、各種操作方法をまとめて確認できるようにする
        GUI.Label(
            new Rect(10, 70, 1000, 60),

            // 現在の編集状態と操作方法を1つの文章として表示する
            // selectedItem + 1は人間が見るための1始まりの番号に変換して表示する
            // snapがtrueならON、falseならOFFとして表示する
            // \nで改行し、1行目に状態、2行目に操作方法を表示する
            $"EDIT MODE   置く部品: [{selectedItem + 1}/{items.Count}] {name}   スナップ: {(snap ? "ON" : "OFF")} (G)   Tab: プレイ開始\n" +
            "下のパレットからドラッグで置く  枠の取っ手:サイズ  中をドラッグ:移動  R:回転  右クリック/Delete:削除  Esc:選択解除  WASD:カメラ"
        );
    }
}