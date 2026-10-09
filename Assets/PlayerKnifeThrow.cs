using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerKnifeThrow : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Knife knifePrefab;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.6f, -0.3f);
    [SerializeField] private float cooldown = 0.4f;
    [SerializeField] private int maxKnives = 4;

    private float nextThrowTime;
    private readonly List<Knife> thrown = new List<Knife>();

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && Time.time >= nextThrowTime)
        {
            nextThrowTime = Time.time + cooldown;
            Throw();
        }
    }

    void Throw()
    {
        Vector2 pos = (Vector2)transform.position
            + new Vector2(spawnOffset.x * player.Facing, spawnOffset.y);

        var k = Instantiate(knifePrefab, pos, Quaternion.identity);
        Vector2 castOrigin = new Vector2(transform.position.x, pos.y);   // プレイヤーの中心の高さ=ナイフの高さ
        k.Launch(player.Facing, castOrigin);

        thrown.Add(k);
        thrown.RemoveAll(x => x == null);
        while (thrown.Count > maxKnives)          // 古いナイフから消す
        {
            Destroy(thrown[0].gameObject);
            thrown.RemoveAt(0);
        }
    }
}