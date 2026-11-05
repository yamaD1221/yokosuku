using UnityEngine;
using UnityEngine.UI;

// プレイヤーのHPをSliderで画面に表示するクラス
public class PlayerHealthUI : MonoBehaviour
{
    // HP情報を管理するPlayerHealthへの参照
    [SerializeField] private PlayerHealth playerHealth;

    // HPを表示するSliderへの参照
    [SerializeField] private Slider slider;

    // ゲーム開始時に1回実行される
    void Start()
    {
        // InspectorでSliderが設定されていない場合、
        // 同じGameObjectに付いているSliderを取得する
        if (slider == null)
            slider = GetComponent<Slider>();

        // PlayerHealthが設定されていない場合、
        // Playerタグが付いたオブジェクトを探す
        if (playerHealth == null)
        {
            var p = GameObject.FindWithTag("Player");

            // プレイヤーが見つかった場合、PlayerHealthを取得する
            if (p != null)
                playerHealth = p.GetComponent<PlayerHealth>();
        }

        // PlayerHealthまたはSliderが取得できなかった場合は処理を終了する
        if (playerHealth == null || slider == null)
            return;

        // HP変更イベントを登録する
        // HPが変更されるとRefreshメソッドが呼び出される
        playerHealth.OnHealthChanged += Refresh;

        // ゲーム開始時のHPをSliderに反映する
        Refresh(playerHealth.Hp, playerHealth.MaxHp);
    }

    // オブジェクトが破棄されるときに実行される
    void OnDestroy()
    {
        // HP変更イベントの登録を解除する
        // 不要なイベント呼び出しを防ぐ
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= Refresh;
    }

    // HPが変更されたときに呼び出される
    // hp：現在のHP
    // max：最大HP
    void Refresh(int hp, int max)
    {
        // Sliderの最大値を最大HPに設定する
        slider.maxValue = max;

        // Sliderの現在値を現在HPに設定する
        // HPが減るとSliderの表示も減少する
        slider.value = hp;
    }
}