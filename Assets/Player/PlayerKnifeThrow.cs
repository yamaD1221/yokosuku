using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤーのナイフ投げを管理するクラス
public class PlayerKnifeThrow : MonoBehaviour
{
    // プレイヤーの操作情報
    [SerializeField] private PlayerController player;

    // 投げるナイフのPrefab
    [SerializeField] private Knife knifePrefab;

    // ナイフの生成位置をずらす距離
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.6f, -0.3f);

    // ナイフを投げる間隔（秒）
    [SerializeField] private float cooldown = 0.4f;

    // 同時に存在できるナイフの最大数
    [SerializeField] private int maxKnives = 4;

    // 次にナイフを投げられる時刻
    private float nextThrowTime;

    // 投げたナイフを管理するリスト
    private readonly List<Knife> thrown = new List<Knife>();

    // オブジェクト生成時の初期化
    void Awake()
    {
        // PlayerControllerが設定されていない場合、同じオブジェクトから取得する
        if (player == null) player = GetComponent<PlayerController>();
    }

    // 毎フレーム実行される
    void Update()
    {
        // 編集中または結果画面表示中はナイフを投げられない
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        // 右クリックされ、クールダウンが終了している場合にナイフを投げる
        if (Mouse.current.rightButton.wasPressedThisFrame && Time.time >= nextThrowTime)
        {
            // 次に投げられる時刻を設定する
            nextThrowTime = Time.time + cooldown;

            // ナイフを投げる処理
            Throw();
        }
    }

    // ナイフを生成して投げる
    void Throw()
    {
        // ナイフを投げた回数をRunRecorderに記録する
        RunRecorder.KnifeThrown();

        // プレイヤーが向いているマウス方向を取得する
        Vector2 direction = player.attackDirection;

        // プレイヤーの位置から投げる方向に生成位置をずらす
        Vector2 pos = (Vector2)transform.position
            + direction * spawnOffset.x;

        // ナイフを生成する
        var k = Instantiate(knifePrefab, pos, Quaternion.identity);

        // プレイヤーの中心位置を取得する
        // ナイフがプレイヤーと同じ高さから飛ぶようにする
        Vector2 castOrigin = transform.position;

        // ナイフに投げる方向と開始位置を渡す
        k.Launch(direction, castOrigin);

        // 生成したナイフをリストに追加する
        thrown.Add(k);

        // すでに破棄されたナイフをリストから削除する
        thrown.RemoveAll(x => x == null);

        // ナイフの数が最大数を超えた場合は古いものから削除する
        while (thrown.Count > maxKnives)
        {
            // 最も古いナイフを破棄する
            Destroy(thrown[0].gameObject);

            // リストからも削除する
            thrown.RemoveAt(0);
        }
    }
}