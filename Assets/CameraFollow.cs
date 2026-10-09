using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public float smooth = 8f;

    void LateUpdate()
    {
        Vector3 target = new Vector3(player.position.x, player.position.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, target, smooth * Time.deltaTime);
    }
}