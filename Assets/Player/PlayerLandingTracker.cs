using System.Collections.Generic;
using UnityEngine;

// 「床への着地」を数える。ナイフの足場や敵の上は、床として扱わない
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerLandingTracker : MonoBehaviour
{
    // 空(Nothing)なら、ナイフと敵以外をすべて床とみなす。指定すれば、そのレイヤーだけが床になる
    [SerializeField] private LayerMask floorLayers;

    private readonly HashSet<Collider2D> floorContacts = new HashSet<Collider2D>();
    private bool hasBeenGrounded;    // 最初の接地(開始時に立っている状態)は、着地に数えない

    void OnEnable() { RunRecorder.RunStarted += ResetTracking; }
    void OnDisable() { RunRecorder.RunStarted -= ResetTracking; }

    public void ResetTracking()
    {
        floorContacts.Clear();
        hasBeenGrounded = false;
    }

    void OnCollisionEnter2D(Collision2D c) { Evaluate(c); }
    void OnCollisionStay2D(Collision2D c) { Evaluate(c); }
    void OnCollisionExit2D(Collision2D c) { floorContacts.Remove(c.collider); }

    void Evaluate(Collision2D c)
    {
        if (!IsFloor(c.collider)) return;

        // 足元(上向きの面)に触れているときだけ、床に立っているとみなす
        bool standing = false;
        for (int i = 0; i < c.contactCount; i++)
        {
            if (c.GetContact(i).normal.y > 0.5f) { standing = true; break; }
        }
        if (!standing) { floorContacts.Remove(c.collider); return; }

        if (!floorContacts.Add(c.collider)) return;     // すでに乗っている床

        // どの床にも触れていない状態から、触れた = 着地
        // (隣の床に歩いて移るときは、すでに別の床に乗っているので、数えない)
        if (floorContacts.Count == 1)
        {
            if (hasBeenGrounded) RunRecorder.Landed();
            hasBeenGrounded = true;
        }
    }

    bool IsFloor(Collider2D col)
    {
        if (floorLayers.value != 0 && (floorLayers.value & (1 << col.gameObject.layer)) == 0) return false;
        if (col.GetComponent<Knife>() != null) return false;            // ナイフ足場は、床ではない
        if (col.GetComponentInParent<Enemy>() != null) return false;    // 敵の上も、床ではない
        return true;
    }
}