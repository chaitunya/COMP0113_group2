using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WardrobeUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject wardrobePanel;
    public GameObject clothingScrollViewGameObject;
    public GameObject avatarMirroringGameObject;


    public void OpenWardrobe()
    {
        wardrobePanel.SetActive(true);
        ShowCategory("Head"); // Optionally reset to default when opening
    }

    public void CloseWardrobe()
    {
        wardrobePanel.SetActive(false);
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
