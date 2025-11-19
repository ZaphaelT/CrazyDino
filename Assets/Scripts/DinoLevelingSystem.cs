using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Dodaj ten using

public class DinoLevelingSystem : MonoBehaviour
{
    public static DinoLevelingSystem Instance { get; private set; }

    [Header("Konfiguracja poziomów")]
    [Tooltip("Ile exp potrzeba na poziom")]
    [SerializeField] private int expPerLevel = 100;
    [Tooltip("Maksymalny poziom")]
    [SerializeField] private int maxLevel = 3;

    [Header("UI")]
    [Tooltip("Image typu Filled (ustawiony w Inspectorze)")]
    [SerializeField] private Image expFillImage;
    [Tooltip("Opcjonalny tekst pokazuj¹cy poziom")]
    [SerializeField] private TextMeshProUGUI levelText; // Zmieniono typ

    [Header("Stan (readonly w Inspectorze dla widocznoœci)")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentExp = 0;

    // Event wywo³ywany przy awansie (mo¿esz subskrybowaæ)
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

    /// <summary>
    /// Wywo³aj, gdy gracz ma otrzymaæ exp (ró¿ni przeciwnicy zwracaj¹ ró¿ne wartoœci).
    /// </summary>
    public void AwardExp(int amount)
    {
        if (amount <= 0)
            return;

        // Je¿eli ju¿ na max poziomie - pasek pe³ny, nie dodajemy wiêcej
        if (currentLevel >= maxLevel)
        {
            currentLevel = maxLevel;
            currentExp = expPerLevel;
            UpdateUI();
            return;
        }

        currentExp += amount;

        // Obs³uga wielokrotnego levelowania (np. gdy zdobyto >> expPerLevel)
        while (currentExp >= expPerLevel && currentLevel < maxLevel)
        {
            currentExp -= expPerLevel;
            currentLevel++;
            OnLevelUp?.Invoke(currentLevel);
        }

        // Je¿eli osi¹gniêto max level - ustaw pasek jako pe³ny
        if (currentLevel >= maxLevel)
        {
            currentLevel = maxLevel;
            currentExp = expPerLevel;
        }

        UpdateUI();
    }

    /// <summary>
    /// Skrócona nazwa: wywo³anie przy zabiciu wroga z podan¹ wartoœci¹ exp.
    /// </summary>
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

    // Gettery do u¿ycia z zewn¹trz
    public int GetCurrentLevel() => currentLevel;
    public int GetCurrentExp() => currentExp;
    public int GetExpPerLevel() => expPerLevel;
}
