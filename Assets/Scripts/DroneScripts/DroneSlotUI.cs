using System;
using UnityEngine;
using UnityEngine.UI;

public class DroneSlotUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Button spawnButton;
    public Button selectButton;
    public GameObject selectedHighlight;
    public Image hpBar;
    public Image spawnCooldownOverlay;

    [HideInInspector] public int slotIndex;

    public Action<int> OnSelected;
    public Action<int> OnSpawnRequested;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);

        if (spawnButton != null)
            spawnButton.onClick.AddListener(OnSpawnClicked);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(OnSelectClicked);

        if (spawnButton != null)
            spawnButton.onClick.RemoveListener(OnSpawnClicked);
    }

    private void OnSelectClicked()
    {
        OnSelected?.Invoke(slotIndex);
    }

    private void OnSpawnClicked()
    {
        OnSpawnRequested?.Invoke(slotIndex);
    }

    // zewnêtrzne API
    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null)
            selectedHighlight.SetActive(selected);
    }

    public void SetSpawnInteractable(bool interactable)
    {
        if (spawnButton != null)
            spawnButton.interactable = interactable;
    }

    public void SetHPFill(float fill)
    {
        if (hpBar != null)
            hpBar.fillAmount = Mathf.Clamp01(fill);
    }

    public void SetSpawnCooldown(float progress)
    {
        if (spawnCooldownOverlay != null)
        {
            spawnCooldownOverlay.fillAmount = progress;
        }
    }
}
