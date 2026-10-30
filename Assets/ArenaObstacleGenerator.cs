
using System.Collections.Generic;
using UnityEngine;

public class ArenaObstacleGenerator : MonoBehaviour
{
    public enum WallSide
    {
        Bottom,
        Top,
        Left,
        Right
    }

    [Header("障害物Prefab")]
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Header("配置数（1辺あたり）")]
    [SerializeField] private Vector2Int countPerSide = new Vector2Int(2, 4);

    [Header("障害物のサイズ")]
    [SerializeField] private Vector2 widthRange = new Vector2(2f, 4.5f);
    [SerializeField] private Vector2 depthRange = new Vector2(2f, 5.5f);

    [Header("配置設定")]
    [SerializeField] private float cornerMargin = 1f;
    [SerializeField] private float wallOverlap = 0.15f;
    [SerializeField] private float minSpacing = 0.8f;

    [Header("生成設定")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int seed = 0;

    private Transform generatedRoot;
    private readonly List<Rect> placedAreas = new List<Rect>();

    private System.Random random;

    private void Start()
    {
        if (generateOnStart)
            GenerateObstacles();
    }

    [ContextMenu("障害物を再生成")]
    public void GenerateObstacles()
    {
        ClearObstacles();

        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("障害物Prefabが登録されていません");
            return;
        }

        // ArenaRotatorの実際の箱の大きさを取得
        ArenaRotator arena = GetComponent<ArenaRotator>();

        float innerSize = arena != null
            ? arena.InnerSize
            : 30f;

        float half = innerSize * 0.5f;

        random = seed == 0
            ? new System.Random()
            : new System.Random(seed);

        placedAreas.Clear();

        generatedRoot = new GameObject("Generated Obstacles").transform;
        generatedRoot.SetParent(transform, false);

        // 4方向それぞれに障害物を配置
        GenerateSide(WallSide.Bottom, half);
        GenerateSide(WallSide.Top, half);
        GenerateSide(WallSide.Left, half);
        GenerateSide(WallSide.Right, half);

        Physics2D.SyncTransforms();

        Debug.Log("壁からの障害物生成が完了しました");
    }

    private void GenerateSide(WallSide side, float half)
    {
        int count = random.Next(
            countPerSide.x,
            countPerSide.y + 1
        );

        int generated = 0;

        for (int i = 0; i < count; i++)
        {
            bool success = false;

            for (int attempt = 0; attempt < 80; attempt++)
            {
                GameObject prefab =
                    obstaclePrefabs[random.Next(obstaclePrefabs.Length)];

                if (prefab == null)
                    continue;

                BoxCollider2D box =
                    prefab.GetComponent<BoxCollider2D>();

                if (box == null)
                {
                    Debug.LogWarning(
                        prefab.name + " にBoxCollider2Dがありません"
                    );
                    continue;
                }

                float width = RandomRange(widthRange);
                float depth = RandomRange(depthRange);

                float angle =
                    (side == WallSide.Left || side == WallSide.Right)
                    ? 90f
                    : 0f;

                // 壁に沿った方向の座標
                float tangent = RandomRange(
                    -half + cornerMargin + width * 0.5f,
                     half - cornerMargin - width * 0.5f
                );

                Vector2 position;

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

                // 左右の壁は90度回転するためサイズも入れ替える
                Vector2 areaSize =
                    (side == WallSide.Left || side == WallSide.Right)
                    ? new Vector2(depth, width)
                    : new Vector2(width, depth);

                Rect area = new Rect(
                    position.x - areaSize.x * 0.5f - minSpacing * 0.5f,
                    position.y - areaSize.y * 0.5f - minSpacing * 0.5f,
                    areaSize.x + minSpacing,
                    areaSize.y + minSpacing
                );

                // 他の障害物と重ならないか確認
                bool overlap = false;

                foreach (Rect existing in placedAreas)
                {
                    if (area.Overlaps(existing))
                    {
                        overlap = true;
                        break;
                    }
                }

                if (overlap)
                    continue;

                // 障害物を生成
                GameObject obj = Instantiate(
                    prefab,
                    generatedRoot
                );

                obj.name = prefab.name + "_" + side + "_" + i;

                obj.transform.localPosition =
                    new Vector3(position.x, position.y, 0f);

                obj.transform.localRotation =
                    Quaternion.Euler(0f, 0f, angle);

                // PrefabのColliderサイズを基準に拡大縮小
                Vector3 baseScale = prefab.transform.localScale;

                float scaleX = width /
                    (box.size.x * Mathf.Abs(baseScale.x));

                float scaleY = depth /
                    (box.size.y * Mathf.Abs(baseScale.y));

                obj.transform.localScale = new Vector3(
                    baseScale.x * scaleX,
                    baseScale.y * scaleY,
                    baseScale.z
                );

                placedAreas.Add(area);

                generated++;
                success = true;
                break;
            }

            if (!success)
            {
                Debug.LogWarning(
                    side + "側に障害物を配置できませんでした"
                );
            }
        }

        Debug.Log(side + "側: " + generated + "個生成");
    }

    private float RandomRange(Vector2 range)
    {
        return RandomRange(range.x, range.y);
    }

    // 最小値と最大値を指定するバージョン
    private float RandomRange(float min, float max)
    {
        return min +
            (float)random.NextDouble() * (max - min);
    }

    private void ClearObstacles()
    {
        Transform old = transform.Find("Generated Obstacles");

        if (old != null)
        {
            if (Application.isPlaying)
                Destroy(old.gameObject);
            else
                DestroyImmediate(old.gameObject);
        }

        generatedRoot = null;
        placedAreas.Clear();
    }
}
