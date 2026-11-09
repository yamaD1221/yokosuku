
using System.Collections;
using UnityEngine;

public class HitSparkEffect : MonoBehaviour
{
	private static Sprite sparkSprite;
    private Vector2 lastAttackDirection;
    // 火花を発生させる
    public static void Play(Vector2 position, Vector2 direction)
    {
        GameObject effect = new GameObject("HitSparkEffect");
        effect.transform.position = position;

        HitSparkEffect script = effect.AddComponent<HitSparkEffect>();
        script.lastAttackDirection = direction;
        script.StartCoroutine(script.SparkCoroutine());
    }

    private IEnumerator SparkCoroutine()
	{
		// 白い四角形の画像を火花として使用
		if (sparkSprite == null)
		{
			sparkSprite = Sprite.Create(
				Texture2D.whiteTexture,
				new Rect(0, 0, 1, 1),
				new Vector2(0.5f, 0.5f),
				1f
			);
		}

        const int sparkCount = 50;
        const float duration = 0.6f;

        GameObject[] sparks = new GameObject[sparkCount];
		Vector2[] velocities = new Vector2[sparkCount];
		SpriteRenderer[] renderers = new SpriteRenderer[sparkCount];

		Color[] colors =
		{
			new Color(1f, 1f, 0.2f),
			new Color(1f, 0.6f, 0.1f),
			new Color(1f, 0.9f, 0.5f)
		};

        //  生成
        for (int i = 0; i < sparkCount; i++)
        {
            sparks[i] = new GameObject("Spark");
            sparks[i].transform.SetParent(transform);
            sparks[i].transform.localPosition = Vector3.zero;

            renderers[i] = sparks[i].AddComponent<SpriteRenderer>();
            renderers[i].sprite = sparkSprite;
            renderers[i].color = colors[Random.Range(0, colors.Length)];
            renderers[i].sortingOrder = 20;

            // 攻撃方向と逆向き（プレイヤー側）に飛ばす
            Vector2 attackDirection = -lastAttackDirection;

            // 左右30度の範囲でばらつかせる
            float spreadAngle = Random.Range(-30f, 30f);

            Vector2 direction =
                Quaternion.Euler(0f, 0f, spreadAngle) * attackDirection;

            // 大きな火花を5個にする
            if (i < 5)
            {
                velocities[i] = direction * Random.Range(14f, 22f);

                sparks[i].transform.localScale = new Vector3(
                    Random.Range(0.2f, 0.3f),
                    Random.Range(0.6f, 0.9f),
                    1f
                );
            }
            else
            {
                // 通常の火花45個
                velocities[i] = direction * Random.Range(10f, 18f);

                sparks[i].transform.localScale = new Vector3(
                    Random.Range(0.1f, 0.18f),
                    Random.Range(0.25f, 0.45f),
                    1f
                );
            }
        }

        float elapsed = 0f;

		while (elapsed < duration)
		{
			float delta = Time.deltaTime;
			elapsed += delta;

			float rate = elapsed / duration;

			for (int i = 0; i < sparkCount; i++)
			{
				if (sparks[i] == null) continue;

				// 火花を移動させる
				sparks[i].transform.position +=
					(Vector3)(velocities[i] * delta);

                // 徐々に速度を落とす
                velocities[i] *= 0.992f;

                // 徐々に小さくする
                sparks[i].transform.localScale *= 0.985f;

                // 徐々に透明にする
                Color color = renderers[i].color;
				color.a = 1f - rate;
				renderers[i].color = color;
			}

			yield return null;
		}

		Destroy(gameObject);
	}
}
