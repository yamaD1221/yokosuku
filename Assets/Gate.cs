using System.Collections;
using UnityEngine;

public class Gate : MonoBehaviour
{
    [SerializeField] private float openHeight = 4f;
    [SerializeField] private float openTime = 1f;
    private bool opened;

    public void Open()
    {
        if (opened) return;
        opened = true;
        StartCoroutine(Slide());
    }

    IEnumerator Slide()
    {
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * openHeight;
        float t = 0f;
        while (t < openTime)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, t / openTime);
            yield return null;
        }
        transform.position = end;
    }
}