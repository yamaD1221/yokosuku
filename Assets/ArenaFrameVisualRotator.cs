
using UnityEngine;

// 箱の見た目だけを回転させる
// このTransformの子には、見た目専用のオブジェクトを配置する
public class ArenaFrameVisualRotator : MonoBehaviour
{
    private Quaternion initialRotation;

    void Awake()
    {
        initialRotation = transform.rotation;
    }

    void LateUpdate()
    {
        var arena = ArenaRotator.Instance;
        if (arena == null) return;

        transform.rotation =
            initialRotation *
            Quaternion.Euler(0f, 0f, arena.ViewAngle);
    }
}
