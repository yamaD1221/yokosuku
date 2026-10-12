using UnityEngine;

public class Goal : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        if (LevelEditor.Instance != null) LevelEditor.Instance.OnGoalReached();
    }
}