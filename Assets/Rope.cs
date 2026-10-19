using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Rope : MonoBehaviour
{
    [SerializeField] private Rock rock;     // Ø‚é‚Æ—‚¿‚éŠâ(”CˆÓ)
    //[SerializeField] private Gate gate;     // Ø‚é‚ÆŠJ‚­–å(”CˆÓ)
    private bool cut;

    public void Cut()
    {
        Debug.Log("Rope.Cut ŒÄ‚Î‚ê‚½");
        if (cut) return;
        cut = true;

        if (rock != null) rock.Drop();
       // if (gate != null) gate.Open();

        GetComponent<Collider2D>().enabled = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;
    }
}