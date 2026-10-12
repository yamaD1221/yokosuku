using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class LevelEditor : MonoBehaviour
{
    public static LevelEditor Instance { get; private set; }
    public static bool IsEditing { get; private set; }
    public static event Action<bool> EditingChanged;

    [Serializable]
    public class Item
    {
        public string id;
        public GameObject prefab;
        public Sprite icon;                // パレットの画像(空なら部品のスプライト)
        public bool isPlayer;              // プレイヤー(=スタート地点)用の項目
        public bool canRotate = true;
        public bool canResize = true;
    }

    [SerializeField] private List<Item> items = new List<Item>();
    [SerializeField] private GameObject player;          // 空なら Player タグから探す
    [SerializeField] private Transform levelRoot;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float cameraPanSpeed = 12f;
    [SerializeField] private float moveSnap = 0.5f;
    [SerializeField] private float rotateStep = 15f;
    [SerializeField] private float minSize = 0.25f;

    private enum DragMode { None, Move, Resize }

    private struct ObjData
    {
        public int itemIndex;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    private Camera cam;
    private int selectedItem;
    private LevelObject current;
    private LevelObject playerLo;
    private DragMode mode;
    private Vector2 dragOffset;
    private Vector2 handle;
    private bool snap = true;

    private readonly List<ObjData> snapshot = new List<ObjData>();
    private Vector3 playerStartPos;
    private Quaternion playerStartRot;
    private bool cleared;
    private bool showClear;

    private LineRenderer frameLine;
    private readonly LineRenderer[] handleLines = new LineRenderer[8];
    private static readonly Vector2[] HandleDirs =
    {
        new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
        new Vector2(-1,  0),                     new Vector2(1,  0),
        new Vector2(-1,  1), new Vector2(0,  1), new Vector2(1,  1),
    };

    // ---------- 初期化 ----------
    void Awake()
    {
        Instance = this;
        var mat = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        frameLine = MakeLine(mat, 4, true, Color.yellow);
        for (int i = 0; i < handleLines.Length; i++)
            handleLines[i] = MakeLine(mat, 2, false, Color.white);
        HideFrame();
    }

    void Start()
    {
        cam = Camera.main;
        if (player == null) player = GameObject.FindWithTag("Player");

        // プレイヤーも「置いた物」として選べるようにする
        int pi = items.FindIndex(x => x.isPlayer);
        if (player != null && pi >= 0)
        {
            playerLo = player.GetComponent<LevelObject>();
            if (playerLo == null) playerLo = player.AddComponent<LevelObject>();
            playerLo.itemIndex = pi;
        }

        ApplyMode(true);      // 起動時は編集モード
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        IsEditing = false;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb.tabKey.wasPressedThisFrame)
        {
            if (IsEditing) EnterPlay(); else ExitToEdit();
        }
        if (!IsEditing) return;

        HandleKeys(kb);
        HandleMouse(kb);
        UpdateFrame();
    }

    // ---------- 編集とプレイの切り替え ----------
    void ApplyMode(bool editing)
    {
        IsEditing = editing;
        Time.timeScale = editing ? 0f : 1f;
        EditingChanged?.Invoke(editing);
    }

    void EnterPlay()
    {
        TakeSnapshot();
        Select(null);
        HideFrame();
        ApplyMode(false);
    }

    void ExitToEdit()
    {
        StopAllCoroutines();
        cleared = false;
        showClear = false;
        RestoreLevel();
        ResetPlayer();
        if (player != null && cam != null)
            cam.transform.position = new Vector3(playerStartPos.x, playerStartPos.y, cam.transform.position.z);
        ApplyMode(true);
    }

    // 死んだときなど、スタートからやり直す(プレイは続ける)
    public void RestartPlay()
    {
        StopAllCoroutines();
        cleared = false;
        showClear = false;
        RestoreLevel();
        ResetPlayer();
        ApplyMode(false);
    }

    public void OnGoalReached()
    {
        if (IsEditing || cleared) return;
        cleared = true;
        StartCoroutine(ClearRoutine());
    }

    IEnumerator ClearRoutine()
    {
        showClear = true;
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(1.5f);
        ExitToEdit();
    }

    // ---------- 記録と復元 ----------
    void TakeSnapshot()
    {
        snapshot.Clear();
        if (levelRoot != null)
        {
            foreach (Transform child in levelRoot)
            {
                var lo = child.GetComponent<LevelObject>();
                if (lo == null) continue;
                snapshot.Add(new ObjData
                {
                    itemIndex = lo.itemIndex,
                    position = child.position,
                    rotation = child.rotation,
                    scale = child.localScale
                });
            }
        }
        if (player != null)
        {
            playerStartPos = player.transform.position;
            playerStartRot = player.transform.rotation;
        }
    }

    void RestoreLevel()
    {
        if (levelRoot == null) return;

        for (int i = levelRoot.childCount - 1; i >= 0; i--)
            Destroy(levelRoot.GetChild(i).gameObject);

        // 投げたナイフ(親なしで生成されたもの)も消す
        foreach (var k in FindObjectsByType<Knife>(FindObjectsSortMode.None))
            Destroy(k.gameObject);

        foreach (var d in snapshot)
        {
            if (d.itemIndex < 0 || d.itemIndex >= items.Count || items[d.itemIndex].prefab == null) continue;
            var go = Instantiate(items[d.itemIndex].prefab, d.position, d.rotation, levelRoot);
            go.transform.localScale = d.scale;
            go.AddComponent<LevelObject>().itemIndex = d.itemIndex;
        }
        Physics2D.SyncTransforms();
    }

    void ResetPlayer()
    {
        if (player == null) return;

        player.transform.SetPositionAndRotation(playerStartPos, playerStartRot);

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        var health = player.GetComponent<PlayerHealth>();
        if (health != null) health.ResetState();
        var controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.ResetState();

        Physics2D.SyncTransforms();
    }

    // ---------- キー操作 ----------
    void HandleKeys(Keyboard kb)
    {
        if (kb.qKey.wasPressedThisFrame) SelectItem(selectedItem - 1);
        if (kb.eKey.wasPressedThisFrame) SelectItem(selectedItem + 1);
        for (int i = 0; i < items.Count && i < 9; i++)
            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SelectItem(i);

        Vector2 move = Vector2.zero;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
        cam.transform.position += (Vector3)(move * cameraPanSpeed * Time.unscaledDeltaTime);

        if (kb.gKey.wasPressedThisFrame) snap = !snap;
        if (kb.escapeKey.wasPressedThisFrame) Select(null);

        if (current == null) return;

        if (kb.rKey.wasPressedThisFrame && ItemOf(current).canRotate)
        {
            float dir = kb.shiftKey.isPressed ? -1f : 1f;
            current.transform.Rotate(0f, 0f, rotateStep * dir);
            Physics2D.SyncTransforms();
        }
        if (kb.deleteKey.wasPressedThisFrame) Remove(current);
    }

    // ---------- マウス操作 ----------
    void HandleMouse(Keyboard kb)
    {
        var mouse = Mouse.current;
        Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (kb.nKey.wasPressedThisFrame && !overUI) Select(Spawn(world));

        if (mouse.leftButton.wasPressedThisFrame && !overUI)
        {
            if (TryHandleAt(world, out var dir))
            {
                handle = dir;
                mode = DragMode.Resize;
            }
            else
            {
                var hit = PickAt(world);
                Select(hit);
                if (hit != null)
                {
                    mode = DragMode.Move;
                    dragOffset = (Vector2)hit.transform.position - world;
                }
                else mode = DragMode.None;
            }
        }

        if (mouse.leftButton.isPressed && current != null)
        {
            if (mode == DragMode.Move)
            {
                current.transform.position = ToPos(world + dragOffset);
                Physics2D.SyncTransforms();
            }
            else if (mode == DragMode.Resize)
            {
                ResizeByHandle(world);
            }
        }
        if (mouse.leftButton.wasReleasedThisFrame) mode = DragMode.None;

        if (mouse.rightButton.wasPressedThisFrame && !overUI)
        {
            var hit = PickAt(world);
            if (hit != null) Remove(hit);
        }
    }

    // ---------- サイズ変更 ----------
    void ResizeByHandle(Vector2 mouse)
    {
        var t = current.transform;
        Vector2 right = t.right;
        Vector2 up = t.up;
        GetLocalRect(current, out var c, out var h);

        Vector2 anchor = t.TransformPoint(c + new Vector2(-handle.x * h.x, -handle.y * h.y));
        Vector2 v = mouse - anchor;

        var s = t.localScale;
        float newW = 2f * h.x * s.x;
        float newH = 2f * h.y * s.y;
        if (handle.x != 0) newW = SnapSize(Vector2.Dot(v, right) * handle.x);
        if (handle.y != 0) newH = SnapSize(Vector2.Dot(v, up) * handle.y);

        Vector2 centerWorld = t.TransformPoint(c);
        Vector2 rel = centerWorld - anchor;
        float dx = handle.x != 0 ? handle.x * newW * 0.5f : Vector2.Dot(rel, right);
        float dy = handle.y != 0 ? handle.y * newH * 0.5f : Vector2.Dot(rel, up);
        Vector2 newCenter = anchor + right * dx + up * dy;

        s.x = newW / (2f * h.x);
        s.y = newH / (2f * h.y);
        t.localScale = s;

        Vector2 pivot = newCenter - right * (c.x * s.x) - up * (c.y * s.y);
        t.position = new Vector3(pivot.x, pivot.y, t.position.z);
        Physics2D.SyncTransforms();
    }

    float SnapSize(float w)
    {
        if (snap) w = Mathf.Round(w / moveSnap) * moveSnap;
        return Mathf.Max(minSize, w);
    }

    // ---------- 選択枠 ----------
    void UpdateFrame()
    {
        if (current == null) { HideFrame(); return; }

        float unit = cam.orthographicSize * 0.02f;
        GetLocalRect(current, out var c, out var h);
        var t = current.transform;

        frameLine.enabled = true;
        frameLine.widthMultiplier = unit * 0.3f;
        frameLine.SetPosition(0, t.TransformPoint(c + new Vector2(-h.x, -h.y)));
        frameLine.SetPosition(1, t.TransformPoint(c + new Vector2(h.x, -h.y)));
        frameLine.SetPosition(2, t.TransformPoint(c + new Vector2(h.x, h.y)));
        frameLine.SetPosition(3, t.TransformPoint(c + new Vector2(-h.x, h.y)));

        bool showHandles = ItemOf(current).canResize;
        for (int i = 0; i < handleLines.Length; i++)
        {
            var lr = handleLines[i];
            lr.enabled = showHandles;
            if (!showHandles) continue;

            Vector2 p = HandleWorldPos(HandleDirs[i]);
            lr.widthMultiplier = unit * 2f;
            lr.SetPosition(0, p + Vector2.left * unit);
            lr.SetPosition(1, p + Vector2.right * unit);
        }
    }

    void HideFrame()
    {
        if (frameLine != null) frameLine.enabled = false;
        foreach (var lr in handleLines) if (lr != null) lr.enabled = false;
    }

    LineRenderer MakeLine(Material mat, int points, bool loop, Color col)
    {
        var go = new GameObject("EditorLine");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.positionCount = points;
        lr.material = mat;
        lr.startColor = col;
        lr.endColor = col;
        lr.sortingOrder = 1000;
        return lr;
    }

    Vector2 HandleWorldPos(Vector2 dir)
    {
        GetLocalRect(current, out var c, out var h);
        return current.transform.TransformPoint(c + new Vector2(dir.x * h.x, dir.y * h.y));
    }

    bool TryHandleAt(Vector2 world, out Vector2 dir)
    {
        dir = Vector2.zero;
        if (current == null || !ItemOf(current).canResize) return false;

        float hit = cam.orthographicSize * 0.02f * 1.8f;
        foreach (var d in HandleDirs)
        {
            Vector2 p = HandleWorldPos(d);
            if (Mathf.Abs(world.x - p.x) <= hit && Mathf.Abs(world.y - p.y) <= hit)
            {
                dir = d;
                return true;
            }
        }
        return false;
    }

    void GetLocalRect(LevelObject lo, out Vector2 center, out Vector2 half)
    {
        var sr = lo.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            var b = sr.sprite.bounds;
            center = b.center;
            half = b.extents;
            return;
        }
        var box = lo.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            center = box.offset;
            half = box.size * 0.5f;
            return;
        }
        center = Vector2.zero;
        half = Vector2.one * 0.5f;
    }

    // ---------- 置く、選ぶ、消す ----------
    Item ItemOf(LevelObject lo) => items[Mathf.Clamp(lo.itemIndex, 0, items.Count - 1)];

    void SelectItem(int index)
    {
        if (items.Count == 0) return;
        selectedItem = (index + items.Count) % items.Count;
    }

    Vector3 ToPos(Vector2 p)
    {
        if (snap)
        {
            p.x = Mathf.Round(p.x / moveSnap) * moveSnap;
            p.y = Mathf.Round(p.y / moveSnap) * moveSnap;
        }
        return new Vector3(p.x, p.y, 0f);
    }

    LevelObject Spawn(Vector2 world)
    {
        if (items.Count == 0) return null;
        var item = items[selectedItem];

        if (item.isPlayer) return MovePlayer(world);     // プレイヤーは1人だけ。位置を動かす
        if (item.prefab == null) return null;

        var go = Instantiate(item.prefab, ToPos(world), Quaternion.identity, levelRoot);
        var lo = go.AddComponent<LevelObject>();
        lo.itemIndex = selectedItem;
        Physics2D.SyncTransforms();
        return lo;
    }

    LevelObject MovePlayer(Vector2 world)
    {
        if (playerLo == null) return null;
        playerLo.transform.position = ToPos(world);
        var rb = playerLo.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        return playerLo;
    }

    public LevelObject SpawnFromPalette(int itemIndex, Vector2 world)
    {
        SelectItem(itemIndex);
        var lo = Spawn(world);
        Select(lo);
        return lo;
    }

    // 枠の中をクリックしたかで判定(コライダーがなくても選べる)
    LevelObject PickAt(Vector2 world)
    {
        LevelObject best = null;
        float bestArea = float.MaxValue;

        foreach (var lo in FindObjectsByType<LevelObject>(FindObjectsSortMode.None))
        {
            GetLocalRect(lo, out var c, out var h);
            Vector2 local = lo.transform.InverseTransformPoint(world);
            if (Mathf.Abs(local.x - c.x) > h.x || Mathf.Abs(local.y - c.y) > h.y) continue;

            var s = lo.transform.localScale;
            float area = h.x * h.y * Mathf.Abs(s.x * s.y);
            if (area < bestArea) { best = lo; bestArea = area; }
        }
        return best;
    }

    void Select(LevelObject lo)
    {
        current = lo;
        if (lo == null) mode = DragMode.None;
    }

    void Remove(LevelObject lo)
    {
        if (ItemOf(lo).isPlayer) return;      // プレイヤーは消せない
        if (lo == current) Select(null);
        Destroy(lo.gameObject);
    }

    // ---------- パレット用の読み取り ----------
    public int ItemCount => items.Count;
    public string GetItemId(int i) => items[i].id;

    public void GetItemIcon(int i, out Sprite sprite, out Color color)
    {
        var item = items[i];
        sprite = null;
        color = Color.white;
        if (item.icon != null) { sprite = item.icon; return; }

        GameObject src = item.isPlayer ? player : item.prefab;
        if (src != null)
        {
            var sr = src.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) { sprite = sr.sprite; color = sr.color; }
        }
    }

    // ---------- 表示 ----------
    void OnGUI()
    {
        if (showClear)
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "CLEAR!", big);
        }

        if (!IsEditing)
        {
            GUI.Label(new Rect(10, 70, 600, 24), "Tab: 編集に戻る");
            return;
        }

        string name = items.Count > 0 ? items[selectedItem].id : "(no items)";
        GUI.Label(new Rect(10, 70, 1000, 60),
            $"EDIT MODE   置く部品: [{selectedItem + 1}/{items.Count}] {name}   スナップ: {(snap ? "ON" : "OFF")} (G)   Tab: プレイ開始\n" +
            "下のパレットからドラッグで置く  枠の取っ手:サイズ  中をドラッグ:移動  R:回転  右クリック/Delete:削除  Esc:選択解除  WASD:カメラ");
    }
}