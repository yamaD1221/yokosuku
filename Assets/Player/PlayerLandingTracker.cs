using System.Collections.Generic;
using UnityEngine;

// プレイヤーが床に着地した回数を記録するクラス
// ナイフの足場や敵の上に乗った場合は着地として数えない
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerLandingTracker : MonoBehaviour
{
    // 床として判定するレイヤー
    // Nothingの場合はナイフと敵以外を床として扱う
    // レイヤーを指定した場合は、そのレイヤーだけを床として扱う
    [SerializeField] private LayerMask floorLayers;

    // 現在接触している床のColliderを管理する
    private readonly HashSet<Collider2D> floorContacts = new HashSet<Collider2D>();

    // 一度でも床に接地したことがあるか
    // 最初から床に立っている状態は着地として数えない
    private bool hasBeenGrounded;

    // 有効化されたときに挑戦開始イベントを登録する
    void OnEnable()
    {
        RunRecorder.RunStarted += ResetTracking;
    }

    // 無効化されたときにイベント登録を解除する
    void OnDisable()
    {
        RunRecorder.RunStarted -= ResetTracking;
    }

    // 着地判定の状態をリセットする
    public void ResetTracking()
    {
        // 接触している床の情報をすべて削除する
        floorContacts.Clear();

        // 接地したことがない状態に戻す
        hasBeenGrounded = false;
    }

    // 床との衝突が始まったときに呼び出される
    void OnCollisionEnter2D(Collision2D c)
    {
        Evaluate(c);
    }

    // 床との衝突が続いている間に呼び出される
    void OnCollisionStay2D(Collision2D c)
    {
        Evaluate(c);
    }

    // 床との衝突が終了したときに呼び出される
    void OnCollisionExit2D(Collision2D c)
    {
        // 接触していた床をリストから削除する
        floorContacts.Remove(c.collider);
    }

    // 床に着地しているかを判定する
    void Evaluate(Collision2D c)
    {
        // 床として扱わないオブジェクトなら処理を終了する
        if (!IsFloor(c.collider)) return;

        // プレイヤーが床の上面に接触しているかを判定する
        // 接触面の法線のY方向が0.5より大きければ上面とみなす
        bool standing = false;

        for (int i = 0; i < c.contactCount; i++)
        {
            if (c.GetContact(i).normal.y > 0.5f)
            {
                standing = true;
                break;
            }
        }

        // 床の上面に接触していない場合は床の接触情報を削除する
        if (!standing)
        {
            floorContacts.Remove(c.collider);
            return;
        }

        // 新しく接触した床を登録する
        // すでに接触している床なら処理を終了する
        if (!floorContacts.Add(c.collider)) return;

        // どの床にも接触していなかった状態から床に接触した場合
        if (floorContacts.Count == 1)
        {
            // 最初の接地ではなく、2回目以降の接地なら着地回数を記録する
            if (hasBeenGrounded)
                RunRecorder.Landed();

            // 接地したことがある状態にする
            hasBeenGrounded = true;
        }
    }

    // 指定されたオブジェクトを床として扱うか判定する
    bool IsFloor(Collider2D col)
    {
        // 床レイヤーが指定されている場合、対象レイヤー以外は除外する
        if (floorLayers.value != 0 &&
            (floorLayers.value & (1 << col.gameObject.layer)) == 0)
            return false;

        // ナイフの足場は床として扱わない
        if (col.GetComponent<Knife>() != null)
            return false;

        // 敵の上に乗った場合も床として扱わない
        if (col.GetComponentInParent<Enemy>() != null)
            return false;

        // それ以外は床として扱う
        return true;
    }
}