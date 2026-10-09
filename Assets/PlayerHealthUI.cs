using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Slider slider;

    void Start()
    {
        if (slider == null) slider = GetComponent<Slider>();
        if (playerHealth == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<PlayerHealth>();
        }
        if (playerHealth == null || slider == null) return;

        playerHealth.OnHealthChanged += Refresh;
        Refresh(playerHealth.Hp, playerHealth.MaxHp);   // ç≈èâÇÃï\é¶
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= Refresh;
    }

    void Refresh(int hp, int max)
    {
        slider.maxValue = max;
        slider.value = hp;
    }
}