using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Rope : MonoBehaviour
{
    [SerializeField] private Rock rock;
    private bool cut;

    public void Cut()
    {
        if (cut) return;
        cut = true;

        if (rock != null) rock.Drop();

        GetComponent<Collider2D>().enabled = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;           // ƒ[ƒv‚ÌŒ©‚½–Ú‚ğÁ‚·
    }
}