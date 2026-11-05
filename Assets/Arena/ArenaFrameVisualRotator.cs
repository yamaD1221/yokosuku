
using UnityEngine;

// 箱の見た目だけを回転させる
// このTransformの子には、見た目専用のオブジェクトを配置する
public class ArenaFrameVisualRotator : MonoBehaviour
{
    //  初期状態の回転角度の保存用
    private Quaternion initialRotation;

    //  一度だけ実行
    void Awake()
    {
        //  現在の回転角度を保存
        initialRotation = transform.rotation;
    }
    //  すべてのUpdataが終わった後に毎フレーム実行
    void LateUpdate()
    {
        //ArenaRotatorのインスタンスを取得
        var arena = ArenaRotator.Instance;
        //  ArenaRotatorが存在しない場合は処理を終了
        if (arena == null) return;

        //  初期回転角度を基準に,ArenaRotatorのViewAngle分だけZ軸方向に回転
        //  箱の見た目だけを回転させる
        transform.rotation =
            initialRotation *
            Quaternion.Euler(0f, 0f, arena.ViewAngle);
    }
}
