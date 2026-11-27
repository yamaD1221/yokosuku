using UnityEngine;

// ボスの攻撃まわりの簡易エフェクト集。
// Unity標準の ParticleSystem と、コードで生成するスプライトだけで作る（素材ファイル不要）。
public static class BossFx
{
    private static Sprite glowSprite;
    private static Sprite ringSprite;
    private static Sprite[] jaggedSprites;

    // ギザギザの火花・炎のような形（3パターン。切り替えてちらつかせる）
    public static Sprite JaggedSprite(int index)
    {
        if (jaggedSprites == null) jaggedSprites = MakeJaggedSprites();
        return jaggedSprites[Mathf.Abs(index) % jaggedSprites.Length];
    }

    // 上下にトゲのある帯状のスプライトを3パターン生成する
    private static Sprite[] MakeJaggedSprites()
    {
        const int W = 128, H = 48;
        var result = new Sprite[3];
        var rand = new System.Random(12345);

        for (int v = 0; v < result.Length; v++)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            float center = H * 0.5f;

            // 区間ごとのトゲの高さ（上側と下側で別々にランダム）
            const int segments = 9;
            var peakTop = new float[segments];
            var peakBottom = new float[segments];
            for (int i = 0; i < segments; i++)
            {
                peakTop[i] = 5f + (float)rand.NextDouble() * 15f;
                peakBottom[i] = 5f + (float)rand.NextDouble() * 15f;
            }

            for (int x = 0; x < W; x++)
            {
                float s = (x / (float)W) * segments;
                int seg = Mathf.Min((int)s, segments - 1);
                float tri = 1f - Mathf.Abs((s - seg) * 2f - 1f); // 区間の中央で1になる三角波
                float hTop = 3f + peakTop[seg] * tri;
                float hBottom = 3f + peakBottom[seg] * tri;

                // 両端は細く絞る
                float taper = Mathf.Clamp01(Mathf.Min(x, W - 1 - x) / 10f);
                hTop *= taper;
                hBottom *= taper;

                for (int y = 0; y < H; y++)
                {
                    bool inside = y >= center - hBottom && y <= center + hTop;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }
            tex.Apply();

            result[v] = Sprite.Create(tex, new Rect(0f, 0f, W, H),
                new Vector2(0.5f, 0.5f), 64f);
        }
        return result;
    }

    // 柔らかい円（中心が明るく、外側へ透明になる）。光のハロー用。
    public static Sprite GlowSprite
    {
        get
        {
            if (glowSprite == null) glowSprite = MakeRadialSprite(false);
            return glowSprite;
        }
    }

    // 輪（リング）。衝撃の広がり用。
    public static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null) ringSprite = MakeRadialSprite(true);
            return ringSprite;
        }
    }

    // 放射グラデーションのスプライトを生成する（ring=true なら輪になる）
    private static Sprite MakeRadialSprite(bool ring)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float a;
                if (ring)
                {
                    // 半径0.78付近が一番明るい輪
                    a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.22f);
                }
                else
                {
                    float v = Mathf.Clamp01(1f - d);
                    a = v * v;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
    }

    // 火花を散らす（着弾・着地・砕け散り用）
    public static void SparkBurst(Vector2 pos, Color color, int count = 8,
        float speed = 3f, float size = 0.12f, float life = 0.45f)
    {
        var go = new GameObject("FxSparks");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();

        // 追加した直後の ParticleSystem は再生中扱いで、duration を設定するとエラーになる。
        // いったん完全に止めてから設定する。
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
        main.startColor = color;
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)count)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.08f;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sortingOrder = 9;

        ps.Play();
        Object.Destroy(go, 1.5f);
    }
}
