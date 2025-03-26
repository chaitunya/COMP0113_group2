using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WardrobeUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject wardrobePanel;
    public GameObject clothingScrollViewGameObject;
    public GameObject avatarMirroringGameObject;

    void Start()
    {
    }

    public void OpenWardrobe()
    {
        wardrobePanel.SetActive(true);
        ShowCategory("Head"); // Optionally reset to default when opening
        if (avatarMirroringGameObject.TryGetComponent<AvatarMirroring>(out var avatarMirroring))
        {
            Debug.Log("Start mirroring>>>>>>");
            avatarMirroring.SetWardrobeMirror(true);
        }
    }

    public void CloseWardrobe()
    {
        wardrobePanel.SetActive(false);
        if (avatarMirroringGameObject.TryGetComponent<AvatarMirroring>(out var avatarMirroring))
        {
            Debug.Log("Close mirroring>>>>>>");
            avatarMirroring.SetWardrobeMirror(false);
        }
    }

    public void ShowCategory(string category)
    {
        if (clothingScrollViewGameObject.TryGetComponent<ClothingScrollView>(out var clothingScrollView))
        {
            clothingScrollView.LoadClothingItems(category);
        }
        else
        {
            Debug.LogError("ClothingScrollView not found");
        }
    }

    public void OnCategoryButtonClicked(string category)
    {
        ShowCategory(category);
    }
}
