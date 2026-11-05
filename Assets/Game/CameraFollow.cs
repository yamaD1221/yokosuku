using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // 追従するプレイヤーのTransform
    public Transform player;

    // 毎フレーム、プレイヤーの移動後にカメラの位置を更新する
    void LateUpdate()
    {
        // レベル編集モード中、またはリザルト画面表示中はカメラを動かさない
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;

        // プレイヤーが設定されていない場合は処理を終了する
        if (player == null) return;

        // 現在のカメラの位置を取得する
        Vector3 pos = transform.position;

        // カメラのX座標をプレイヤーのX座標に合わせる
        pos.x = player.position.x;

        // カメラのY座標をプレイヤーのY座標に合わせる
        pos.y = player.position.y;

        // 更新した位置をカメラに反映する
        transform.position = pos;
    }
}