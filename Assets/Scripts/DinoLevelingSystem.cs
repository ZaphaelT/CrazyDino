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

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource; 
    [SerializeField] private AudioClip levelUpSound;  

    private int currentLevel = 1;
    private int currentExp = 0;

    public event Action<int> OnLevelUp;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
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

            PlayLevelUpSound();
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

    private void PlayLevelUpSound()
    {
        if (audioSource != null && levelUpSound != null)
        {
            audioSource.PlayOneShot(levelUpSound);
        }
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
            levelText.text = currentLevel + "";
    }

    public int GetCurrentLevel() => currentLevel;
    public int GetCurrentExp() => currentExp;
    public int GetExpPerLevel() => expPerLevel;
}