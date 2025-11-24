using UnityEngine;
using UnityEngine.UI;

public class PlayerDinoHP : MonoBehaviour
{
    public static PlayerDinoHP Instance { get; private set; }

    [SerializeField] private Image playerHealthBarImage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UpdatePlayerHealth(float currentHealth, float maxHealth)
    {
        if (playerHealthBarImage != null)
        {
            playerHealthBarImage.fillAmount = currentHealth / maxHealth;
        }
    }
}