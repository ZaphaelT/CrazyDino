using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class DinoLevelingSystem : MonoBehaviour
{
    public static DinoLevelingSystem Instance { get; private set; }

    [Header("Konfiguracja poziomów")]
    [SerializeField] private int expPerLevel = 100;
    [SerializeField] private int maxLevel = 3;
    [SerializeField] private float statsMultiplier = 2;

    [Header("UI")]
    [SerializeField] private Image expFillImage;
    [SerializeField] private TextMeshProUGUI levelText; 

    private int currentLevel = 1;
    private int currentExp = 0;

    public event Action<int> OnLevelUp;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        UpdateUI();
    }

    public void AwardExp(int amount)
    {
        if (amount <= 0)
            return;

        if (currentLevel >= maxLevel)
        {
            currentLevel = maxLevel;
            currentExp = expPerLevel;
            UpdateUI();
            return;
        }

        currentExp += amount;

        while (currentExp >= expPerLevel && currentLevel < maxLevel)
        {
            currentExp -= expPerLevel;
            currentLevel++;
            OnLevelUp?.Invoke(currentLevel);

            if (DinosaurController.Instance != null)
                DinosaurController.Instance.MultiplyStatsOnLevelUp(statsMultiplier); 
        }

        if (currentLevel >= maxLevel)
        {
            currentLevel = maxLevel;
            currentExp = expPerLevel;
        }

        UpdateUI();
    }

 
    public void OnEnemyKilled(int expValue)
    {
        AwardExp(expValue);
    }

    private void UpdateUI()
    {
        if (expFillImage != null)
        {
            expFillImage.fillAmount = (currentLevel >= maxLevel)
                ? 1f
                : Mathf.Clamp01(currentExp / (float)expPerLevel);
        }

        if (levelText != null)
            levelText.text = currentLevel+"";
    }

    public int GetCurrentLevel() => currentLevel;
    public int GetCurrentExp() => currentExp;
    public int GetExpPerLevel() => expPerLevel;
}
