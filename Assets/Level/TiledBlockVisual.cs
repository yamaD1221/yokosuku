using UnityEngine;

// ブロックの模様が、オブジェクトの拡大縮小で伸びてしまわないようにする部品。
//
// 使い方:
//   ブロックの Prefab を開き、子オブジェクト「Visual」を作って SpriteRenderer を付け、
//   見せたいテクスチャを設定する。その Visual にこのスクリプトを付ける。
//   親（ブロック本体）の大きさに合わせて、テクスチャを1マス=1単位でタイリングする。
//   ルート側の SpriteRenderer は無効化しておく（当たり判定やパレットの色はルート側が担当）。
[ExecuteAlways]
public class TiledBlockVisual : MonoBehaviour
{
    private SpriteRenderer sr;
    private Transform parentTf;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        parentTf = transform.parent;
        if (sr != null) sr.drawMode = SpriteDrawMode.Tiled;
    }

    void LateUpdate()
    {
        if (sr == null || parentTf == null) return;

        Vector3 s = parentTf.lossyScale;

        // 親の拡大縮小を打ち消す（見た目の大きさはタイリング側で合わせる）
        transform.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            1f);

        // ブロックの大きさぶんだけテクスチャを並べる
        sr.size = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
    }
}
