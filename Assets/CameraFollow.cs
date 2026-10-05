using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;

    void LateUpdate()
    {
        Vector3 pos = transform.position;

        pos.y = player.position.y;

        transform.position = pos;
    }
}