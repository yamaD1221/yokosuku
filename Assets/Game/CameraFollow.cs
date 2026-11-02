using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;

    void LateUpdate()
    {
        if (LevelEditor.IsEditing || RunController.IsResultShowing) return;
        if (player == null) return;

        Vector3 pos = transform.position;
        pos.x = player.position.x;
        pos.y = player.position.y;
        transform.position = pos;
    }
}