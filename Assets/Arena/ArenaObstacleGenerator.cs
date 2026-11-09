using System.Collections.Generic;
using UnityEngine;

public class ArenaObstacleGenerator : MonoBehaviour
{
    // 障害物を配置する壁の方向
    public enum WallSide
    {
        Bottom,
        Top,
        Left,
        Right
    }

    // [Header]：インスペクターで項目を見やすく区切る
    [Header("障害物Prefab")]
    [SerializeField] private GameObject[] obstaclePrefabs;       // 生成する障害物のPrefab

    [Header("配置数（1辺あたり）")]
    [SerializeField] private Vector2Int countPerSide = new Vector2Int(2, 4); // 1辺あたりの生成数の最小値と最大値

    [Header("障害物のサイズ")]
    [SerializeField] private Vector2 widthRange = new Vector2(2f, 4.5f);     // 障害物の幅の最小値と最大値
    [SerializeField] private Vector2 depthRange = new Vector2(2f, 5.5f);     // 障害物の奥行きの最小値と最大値

    [Header("配置設定")]
    [SerializeField] private float cornerMargin = 1f;       // 箱の角から離す距離
    [SerializeField] private float wallOverlap = 0.15f;     // 壁と障害物を重ねる量
    [SerializeField] private float minSpacing = 0.8f;       // 障害物同士の最低間隔

    [Header("生成設定")]
    [SerializeField] private bool generateOnStart = true;   // ゲーム開始時に自動生成するか
    [SerializeField] private int seed = 0;                  // 乱数のシード値。0なら毎回ランダム

    // 生成した障害物をまとめて管理する親オブジェクト
    private Transform generatedRoot;

    // 生成済み障害物の配置範囲を保存するリスト
    private readonly List<Rect> placedAreas = new List<Rect>();

    // 障害物の配置位置やサイズをランダムに決めるための乱数
    private System.Random random;

    // Start：ゲーム開始時に1回だけ実行。設定に応じて障害物を生成する
    private void Start()
    {
        if (generateOnStart)
            GenerateObstacles();
    }

    // GenerateObstacles：古い障害物を削除して、箱の4辺に障害物を生成する
    // [ContextMenu]：インスペクターのコンポーネントメニューから実行できる
    [ContextMenu("障害物を再生成")]
    public void GenerateObstacles()
    {
        // 以前に生成した障害物を削除
        ClearObstacles();

        // Prefabが登録されていない場合は処理を終了
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("障害物Prefabが登録されていません");
            return;
        }

        // ArenaRotatorの実際の箱の大きさを取得
        ArenaRotator arena = GetComponent<ArenaRotator>();

        // 箱があれば内側の大きさを取得し、なければ30を使う
        float innerSize = arena != null
            ? arena.InnerSize
            : 30f;

        // 箱の中心から壁までの距離を求める
        float half = innerSize * 0.5f;

        // 乱数の初期設定
        // seedが0なら毎回異なる乱数、それ以外なら同じ配置を再現できる
        random = seed == 0
            ? new System.Random()
            : new System.Random(seed);

        // 配置済みの範囲をリセット
        placedAreas.Clear();

        // 生成した障害物をまとめる親オブジェクトを作成
        generatedRoot = new GameObject("Generated Obstacles").transform;
        generatedRoot.SetParent(transform, false);

        // 4方向それぞれに障害物を配置
        GenerateSide(WallSide.Bottom, half);
        GenerateSide(WallSide.Top, half);
        GenerateSide(WallSide.Left, half);
        GenerateSide(WallSide.Right, half);

        // 物理演算にTransformの変更を反映させる
        Physics2D.SyncTransforms();

        Debug.Log("壁からの障害物生成が完了しました");
    }

    // GenerateSide：指定した壁の方向に障害物をランダムに配置する
    private void GenerateSide(WallSide side, float half)
    {
        // 指定された最小数から最大数の間で生成数を決める
        int count = random.Next(
            countPerSide.x,
            countPerSide.y + 1
        );

        // 実際に生成できた障害物の数
        int generated = 0;

        // 決めた数だけ障害物の配置を試す
        for (int i = 0; i < count; i++)
        {
            bool success = false;

            // 1つの障害物につき最大80回まで配置を試す
            for (int attempt = 0; attempt < 80; attempt++)
            {
                // 登録されたPrefabからランダムに選ぶ
                GameObject prefab =
                    obstaclePrefabs[random.Next(obstaclePrefabs.Length)];

                // Prefabが空なら次の試行へ
                if (prefab == null)
                    continue;

                // Prefabに付いているBoxCollider2Dを取得
                BoxCollider2D box =
                    prefab.GetComponent<BoxCollider2D>();

                // BoxCollider2Dが無い場合は生成しない
                if (box == null)
                {
                    Debug.LogWarning(
                        prefab.name + " にBoxCollider2Dがありません"
                    );
                    continue;
                }

                // 障害物の幅と奥行きをランダムに決める
                float width = RandomRange(widthRange);
                float depth = RandomRange(depthRange);

                // 左右の壁に配置する場合は90度回転させる
                float angle =
                    (side == WallSide.Left || side == WallSide.Right)
                    ? 90f
                    : 0f;

                // 壁に沿った方向の配置位置をランダムに決める
                // 箱の角に近づきすぎないようにcornerMarginを考慮する
                float tangent = RandomRange(
                    -half + cornerMargin + width * 0.5f,
                     half - cornerMargin - width * 0.5f
                );

                // 障害物の配置位置
                Vector2 position;

                // 壁の方向に応じて配置位置を決める
                switch (side)
                {
                    case WallSide.Bottom:
                        position = new Vector2(
                            tangent,
                            -half + depth * 0.5f - wallOverlap
                        );
                        break;

                    case WallSide.Top:
                        position = new Vector2(
                            tangent,
                            half - depth * 0.5f + wallOverlap
                        );
                        break;

                    case WallSide.Left:
                        position = new Vector2(
                            -half + depth * 0.5f - wallOverlap,
                            tangent
                        );
                        break;

                    default:
                        position = new Vector2(
                            half - depth * 0.5f + wallOverlap,
                            tangent
                        );
                        break;
                }

                // 左右の壁は90度回転するため、幅と奥行きを入れ替える
                Vector2 areaSize =
                    (side == WallSide.Left || side == WallSide.Right)
                    ? new Vector2(depth, width)
                    : new Vector2(width, depth);

                // 障害物の配置範囲をRectで作成
                // minSpacingを加えて、障害物同士の間隔も確保する
                Rect area = new Rect(
                    position.x - areaSize.x * 0.5f - minSpacing * 0.5f,
                    position.y - areaSize.y * 0.5f - minSpacing * 0.5f,
                    areaSize.x + minSpacing,
                    areaSize.y + minSpacing
                );

                // 他の障害物と配置範囲が重ならないか確認
                bool overlap = false;

                foreach (Rect existing in placedAreas)
                {
                    // Rect同士が重なっている場合は配置できない
                    if (area.Overlaps(existing))
                    {
                        overlap = true;
                        break;
                    }
                }

                // 他の障害物と重なる場合は次の配置を試す
                if (overlap)
                    continue;

                // 障害物をPrefabから生成し、親オブジェクトの子にする
                GameObject obj = Instantiate(
                    prefab,
                    generatedRoot
                );

                // 生成した障害物の名前を設定
                obj.name = prefab.name + "_" + side + "_" + i;

                // 障害物の位置を設定（ローカル座標）
                obj.transform.localPosition =
                    new Vector3(position.x, position.y, 0f);

                // 障害物の回転を設定
                obj.transform.localRotation =
                    Quaternion.Euler(0f, 0f, angle);

                // PrefabのColliderサイズを基準に拡大縮小する
                // Colliderの大きさが指定したwidthとdepthになるように計算
                Vector3 baseScale = prefab.transform.localScale;

                float scaleX = width /
                    (box.size.x * Mathf.Abs(baseScale.x));

                float scaleY = depth /
                    (box.size.y * Mathf.Abs(baseScale.y));

                // 計算した倍率を使って障害物の大きさを設定
                obj.transform.localScale = new Vector3(
                    baseScale.x * scaleX,
                    baseScale.y * scaleY,
                    baseScale.z
                );

                // 配置した範囲をリストに追加
                // 次の障害物が同じ場所に配置されるのを防ぐ
                placedAreas.Add(area);

                // 生成数を増やして、配置成功にする
                generated++;
                success = true;
                break;
            }

            // 80回試しても配置できなかった場合は警告を表示
            if (!success)
            {
                Debug.LogWarning(
                    side + "側に障害物を配置できませんでした"
                );
            }
        }

        // 指定した壁に生成できた障害物の数を表示
        Debug.Log(side + "側: " + generated + "個生成");
    }

    // RandomRange：Vector2で指定した最小値と最大値の間からランダムな数値を返す
    private float RandomRange(Vector2 range)
    {
        return RandomRange(range.x, range.y);
    }

    // RandomRange：最小値と最大値を指定してランダムな数値を返す
    private float RandomRange(float min, float max)
    {
        // NextDoubleで0以上1未満の乱数を取得し、指定範囲の数値に変換する
        return min +
            (float)random.NextDouble() * (max - min);
    }

    // ClearObstacles：以前に生成した障害物を削除して配置情報をリセットする
    // 生成した初期障害物のColliderだけを外部スクリプトへ渡す。
    // 衝撃波の輪郭取得で、プレイヤーやボスなどを障害物として扱わないために使う。
    public Collider2D[] GetGeneratedColliders()
    {
        if (generatedRoot == null) return new Collider2D[0];
        return generatedRoot.GetComponentsInChildren<Collider2D>();
    }

    private void ClearObstacles()
    {
        // 生成した障害物の親オブジェクトを名前で検索
        Transform old = transform.Find("Generated Obstacles");

        if (old != null)
        {
            // プレイ中ならDestroy、編集モードならDestroyImmediateで削除する
            if (Application.isPlaying)
                Destroy(old.gameObject);
            else
                DestroyImmediate(old.gameObject);
        }

        // 親オブジェクトの参照と配置済み範囲をリセット
        generatedRoot = null;
        placedAreas.Clear();
    }
}