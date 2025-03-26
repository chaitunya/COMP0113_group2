using UnityEngine;
using UnityEngine.UI;

public class WardrobeUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject wardrobePanel;
    private ClothingScrollView clothingScrollView;
    private AvatarMirroring avatarMirror;

    void Start()
    {
        clothingScrollView = FindObjectOfType<ClothingScrollView>();
        avatarMirror = FindObjectOfType<AvatarMirroring>();
        // ShowCategory("Hair"); // Default category
    }

    public void OpenWardrobe()
    {
        wardrobePanel.SetActive(true);
        ShowCategory("Head"); // Optionally reset to default when opening
        if (avatarMirror != null)
        {
            Debug.Log("Start mirroring>>>>>>");
            avatarMirror.SetWardrobeMirror(true);
        }
    }

    public void CloseWardrobe()
    {
        wardrobePanel.SetActive(false);
        if (avatarMirror != null)
        {
            Debug.Log("Close mirroring>>>>>>");
            avatarMirror.SetWardrobeMirror(false);
        }
    }

    public void ShowCategory(string category)
    {
        if (clothingScrollView != null)
        {
            clothingScrollView.LoadClothingItems(category);
        }
        else
        {
            Debug.LogError("ClothingScrollView not found in scene!");
        }
    }

    public void OnCategoryButtonClicked(string category)
    {
        ShowCategory(category);
    }
}
