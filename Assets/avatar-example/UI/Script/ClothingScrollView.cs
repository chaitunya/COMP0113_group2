using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Ubiq.Rooms;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Random = UnityEngine.Random;
using System;
using System.IO;


public class ClothingScrollView : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject itemPrefab;
    public Transform contentPanel;

    [Header("Avatar & Networking")]
    public GameObject defaultAvatarPrefab;
    private AvatarManager avatarManager;
    private RoomClient roomClient;
    private GameObject previewAvatarInstance;

    [Header("Avatar Textures")]
    public AvatarTextureCatalogue textureCatalogue;
    
    public int[] headTextures;
    public int[] bodyTextures;
    public int[] leftHandTextures;
    public int[] rightHandTextures;

    [Header("Preview Models")]
    public GameObject head;
    public GameObject body;
    public GameObject leftHand;
    public GameObject rightHand;


    [Header("Preview Cameras")]
    public Camera itemCamera;
    public Camera hatCamera;
    public Camera headCamera;
    public Camera rightHandCamera;
    public Camera leftHandCamera;
    public Camera torsoCamera;

    public GameObject previewPlaceholder;

    private List<Texture2D> catalogueTextures;

    void Start()
    {
        // catalogueTextures = textureCatalogue.Textures;
        var networkScene = NetworkScene.Find(this);
        if (networkScene)
        {
            roomClient = networkScene.GetComponentInChildren<RoomClient>();
            avatarManager = networkScene.GetComponentInChildren<AvatarManager>();
        }
        LoadClothingItems("Head");
    }

    public void LoadClothingItems(string category)
    {
        StartCoroutine(LoadClothingItemsCoroutine(category));
    }

    private IEnumerator LoadClothingItemsCoroutine(string category)
    {
        foreach (Transform child in contentPanel)
        {
            if (child.gameObject != null)
            {
                Destroy(child.gameObject);
            }
        }

        yield return null;

        if (category == "Hats")
        {
            Debug.Log("Loading Hats");
            yield return LoadHats();
        } 
        else if (category == "Items")
        {

            Debug.Log("Loading Items");
            yield return LoadItems();
        }
        else if (category == "Customize")
        {
            CreateCustomizedButton();
        } 
        else 
        {
            BodyPart targetPart = GetBodyPartForCategory(category);
            GameObject previewModel = GetBodyPartPreview(targetPart);
            int[] textureList = GetTextureLists(targetPart);

            // Assign the appropriate camera based on the body part
            Camera camera = targetPart switch
            {
                BodyPart.Head => headCamera,
                BodyPart.Torso => torsoCamera,
                BodyPart.LeftHand => leftHandCamera,
                BodyPart.RightHand => rightHandCamera,
                _ => null
            };
            Debug.Assert(camera != null, $"Invalid body part: {targetPart}");

            string layer = targetPart switch
            {
                BodyPart.Head => "Preview_Head",
                BodyPart.Torso => "Preview_Torso",
                BodyPart.LeftHand => "Preview_LeftHand",
                BodyPart.RightHand => "Preview_RightHand",
                _ => null
            };
            Debug.Assert(layer != null, $"Invalid body part: {targetPart}");


            // Instantiate new items
            foreach (int textureIdx in textureList)
            {

                var texture = textureCatalogue.Get(textureIdx);
                
                GameObject newItem = Instantiate(itemPrefab, contentPanel);

                TMP_Text itemText = newItem.GetComponentInChildren<TMP_Text>();
                if (itemText != null)
                {
                    itemText.text = texture.name;
                }
                // var myTexture = textureCatalogue.Get(3);
                Image itemImage = newItem.GetComponentInChildren<Image>();
                if (itemImage != null && previewModel != null)
                {
                    // itemImage.sprite = GetBodyPartPreview(texture, targetPart);
                    yield return GeneratePreviewFromTexture(texture, previewModel, itemImage, camera, layer);
                }

                // Add button functionality to apply costume
                if (newItem.TryGetComponent<Button>(out var button))
                {
                    button.onClick.AddListener(() => OnClothingItemSelected(texture, targetPart));
                }
            }
        }
        yield break;
    }

    private GameObject GetBodyPartPreview(BodyPart part)
    {
        switch (part)
        {
            case BodyPart.Head: return head;
            case BodyPart.Torso: return body;
            case BodyPart.LeftHand: return leftHand;
            case BodyPart.RightHand: return rightHand;
            default: return head;
        }
    }


    private int[] GetTextureLists(BodyPart bodyPart)
    {
        switch (bodyPart)
        {
            case BodyPart.Head: return headTextures;
            case BodyPart.Torso: return bodyTextures;
            case BodyPart.LeftHand: return leftHandTextures;
            case BodyPart.RightHand: return rightHandTextures;
            default: return headTextures; // Fallback camera
        }
    }


    private IEnumerator GeneratePreviewFromTexture(Texture2D texture, GameObject fbxModel, Image targetUI, Camera camera, string layer)
    {
        // Create a temporary instance of the model for preview
        GameObject modelInstance = Instantiate(fbxModel, previewPlaceholder.transform);
        
        // Set layer to PreviewOnly so it shows up in the preview camera
        int LayerPreviewOnly = LayerMask.NameToLayer(layer);
        SetLayerRecursively(modelInstance, LayerPreviewOnly);
        
        // Ensure the model instance is active
        modelInstance.SetActive(true);

        // Reset transforms initially
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        // Apply the texture to all renderers in the model
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer && renderer.material)
            {
                // Create a new material instance to avoid modifying the shared material
                renderer.material = new Material(renderer.material);
                renderer.material.mainTexture = texture;
                Debug.Log($"Applied texture {texture.name} to {renderer.gameObject.name}");
            }
        }

        // Wait a frame for renderers to initialize with the new texture
        yield return null;
        
        // Calculate bounds of the model
        Bounds bounds = CalculateRendererBounds(modelInstance);
        
        if (bounds.size == Vector3.zero)
        {
            // If no renderers found or invalid bounds, use a default size
            Debug.LogWarning($"No valid bounds found for model, using default.");
            bounds.size = new Vector3(0.2f, 0.2f, 0.2f);
            bounds.center = modelInstance.transform.position;
        }

        // Choose the appropriate camera based on the model type
        Camera previewCamera = camera;
        
        // Calculate distance from camera to placeholder
        float distanceToCamera = Vector3.Distance(previewCamera.transform.position, previewPlaceholder.transform.position);
        
        // Calculate viewport height at this distance using camera FOV
        float viewportHeight = 2.0f * distanceToCamera * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewportWidth = viewportHeight; // Equal to height for 1:1 aspect ratio
        
        // Get the size of the object (using the largest dimension)
        float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        
        // Calculate scale to fit within viewport with a margin
        float marginFactor = 0.6f; // Leave 40% margin to avoid clipping
        float scaleFactor = Mathf.Min(viewportWidth, viewportHeight) * marginFactor / largestDimension;
        
        // Apply the calculated scale
        modelInstance.transform.localScale = Vector3.one * scaleFactor;
        
        // Wait a frame for the renderers to update with new scale
        yield return null;
        
        // Get new bounds after scaling
        bounds = CalculateRendererBounds(modelInstance);
        
        // Center the object relative to the placeholder
        Vector3 offset = bounds.center - previewPlaceholder.transform.position;
        
        // Determine safe distance from camera to avoid clipping
        float cameraForwardDistance = Vector3.Dot(bounds.size, previewCamera.transform.forward) * 0.5f;
        float safeDistance = previewCamera.nearClipPlane + cameraForwardDistance + 0.05f; // Add a small buffer
        
        // Calculate safe position
        Vector3 cameraPosition = previewCamera.transform.position;
        Vector3 cameraForward = previewCamera.transform.forward;
        
        // First center the object horizontally and vertically
        Vector3 newPosition = previewPlaceholder.transform.position - offset;
        
        // Then ensure it's at a safe distance from the camera along the camera's forward axis
        float currentDistance = Vector3.Dot(newPosition - cameraPosition, cameraForward);
        if (currentDistance < safeDistance)
        {
            // Move the object further away from the camera to avoid clipping
            newPosition = newPosition + cameraForward * (safeDistance - currentDistance);
            Debug.Log($"Adjusting model distance from camera to avoid clipping. New distance: {safeDistance}");
        }
        
        modelInstance.transform.position = newPosition;

        // Create a render texture for the camera
        RenderTexture previewRenderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        
        if (previewRenderTexture != null)
        {
            previewCamera.targetTexture = previewRenderTexture;
            previewCamera.Render();
            Debug.Log($"Rendering model with texture {texture.name} to RenderTexture successful.");
        }
        else
        {
            Debug.LogWarning("RenderTexture is null.");
        }

        // Convert RenderTexture to Sprite for UI display
        yield return CapturePreview(previewRenderTexture, targetUI);

        // Clean up temporary objects
        yield return DestroyModelInstanceAfterRender(modelInstance);
    }




    //  private IEnumerator GeneratePreview(Texture2D texture, GameObject fbxModel, Image targetUI, BodyPart bodyPart)
    // {
    //     Camera previewCamera = GetCameraForBodyPart(bodyPart);
    //     // previewCamera.nearClipPlane = 0.1f;
    //     // previewCamera.farClipPlane = 1000f;
      
    //     // Instantiate the model as a separate object in the scene
    //     GameObject modelInstance = Instantiate(fbxModel);
        
    //     // Ensure it is not a prefab asset issue by setting it active
    //     modelInstance.SetActive(true);

    //     modelInstance.transform.localPosition = Vector3.zero;
    //     modelInstance.transform.localRotation = Quaternion.identity;
    //     modelInstance.transform.localScale = Vector3.one;

    //     Renderer renderer = modelInstance.GetComponentInChildren<Renderer>();
    //     if (renderer && renderer.material)
    //     {
    //         renderer.material = new Material(renderer.material);
    //         Debug.Log("renderer material: " + renderer.material.name);
    //         renderer.material.mainTexture = texture;
    //     }

    //     // StartCoroutine(CapturePreview(modelInstance, targetUI));
    //     // Convert Texture2D to RenderTexture
    //     RenderTexture previewRenderTexture = ConvertTextureToRenderTexture(texture);
    //     // Debug.Log("preview targetTexture: " + previewRenderTexture);
    //     if (previewRenderTexture != null)
    //     {
    //         previewCamera.targetTexture = previewRenderTexture;
    //         previewCamera.Render();
    //         Debug.Log("Rendering to RenderTexture successful.");
    //     }
    //     else
    //     {
    //         Debug.LogWarning("RenderTexture is null.");
    //     }

    //     // Convert RenderTexture to Sprite for UI display
    //     StartCoroutine(CapturePreview(previewRenderTexture, targetUI));

    //     // Delay the destruction of the model instance to ensure proper rendering
    //     StartCoroutine(DestroyModelInstanceAfterRender(modelInstance));
    // }


    private RenderTexture ConvertTextureToRenderTexture(Texture2D texture)
    {
        RenderTexture renderTexture = new RenderTexture(texture.width, texture.height, 16, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, renderTexture); // Copy the texture into the RenderTexture
        return renderTexture;
    }

    private IEnumerator LoadItems()
    {
        if (!avatarManager) {
            Debug.LogWarning("Avatar Manager not found");
            yield break;
        }
        var avatar = avatarManager.FindAvatar(roomClient.Me);
        if (!avatar) {
            Debug.LogWarning("Avatar not found");
            yield break;
        }
        var itemAvatar = avatar.GetComponentInChildren<ItemAvatar>();
        if (!itemAvatar) {
            Debug.LogWarning("ItemAvatar not found");
            yield break;
        }

        GameObject noneItem = Instantiate(itemPrefab, contentPanel);
        TMP_Text noneText = noneItem.GetComponentInChildren<TMP_Text>();
        if (noneText != null)
        {
            noneText.text = "None";
        }
        Image noneImage = noneItem.GetComponentInChildren<Image>();
        if (noneImage != null)
        {
            // assign general prohibition sign to noneImage
        }
        if (noneImage.TryGetComponent<Button>(out var noneButton))
        {
            noneButton.onClick.AddListener(() => StartCoroutine(OnItemSelected(null)));
        }

        // Instantiate new items for items
        foreach (GameObject item in itemAvatar.items) {
            GameObject newItem = Instantiate(itemPrefab, contentPanel);

            TMP_Text itemText = newItem.GetComponentInChildren<TMP_Text>();
            if (itemText != null)
            {
                itemText.text = item.name;
            }

            // Set preview image
            Image itemImage = newItem.GetComponentInChildren<Image>();
            if (itemImage != null)
            {
                yield return GeneratePreview(item, itemImage, itemCamera, "Preview_Item");
            }

            // Add button functionality to apply costume
            
            if (newItem.TryGetComponent<Button>(out var button))
            {
                button.onClick.AddListener(() => StartCoroutine(OnItemSelected(item)));
            }
        }
    }

    private IEnumerator LoadHats()
    {
        if (!avatarManager) {
            Debug.LogWarning("Avatar Manager not found");
            yield break;
        }
        var avatar = avatarManager.FindAvatar(roomClient.Me);
        if (!avatar) {
            Debug.LogWarning("Avatar not found");
            yield break;
        }
        var hatAvatar = avatar.GetComponentInChildren<HatAvatar>();
        if (!hatAvatar) {
            Debug.LogWarning("HatAvatar not found");
            yield break;
        }

        GameObject noneHat = Instantiate(itemPrefab, contentPanel);
        TMP_Text noneText = noneHat.GetComponentInChildren<TMP_Text>();
        if (noneText != null)
        {
            noneText.text = "None";
        }
        Image noneImage = noneHat.GetComponentInChildren<Image>();
        if (noneImage != null)
        {
            // assign general prohibition sign to noneImage
        }
        if (noneImage.TryGetComponent<Button>(out var noneButton))
        {
            noneButton.onClick.AddListener(() => StartCoroutine(OnHatSelected(null)));
        }
        // Instantiate new items for hats
        foreach (GameObject hat in hatAvatar.hats)
        {
            GameObject newItem = Instantiate(itemPrefab, contentPanel);

            TMP_Text itemText = newItem.GetComponentInChildren<TMP_Text>();
            if (itemText != null)
            {
                itemText.text = hat.name;
            } else {
                Debug.Log("itemImage is none");
            }

            // Set preview image
            Image itemImage = newItem.GetComponentInChildren<Image>();
            if (itemImage != null)
            {
                yield return GeneratePreview(hat, itemImage, hatCamera, "Preview_Hat");
            } else {
                Debug.Log("itemImage is none");
            }

            // Add button functionality to apply costume
            
            if (newItem.TryGetComponent<Button>(out var button))
            {
                button.onClick.AddListener(() => StartCoroutine(OnHatSelected(hat)));
            }
        }

        
    }
    private IEnumerator GeneratePreview(GameObject accessory, Image targetUI, Camera camera, string layer)
    {
        Debug.Log($"generating preview for {accessory.name}");
        GameObject accessoryInstance = Instantiate(accessory,
            previewPlaceholder.transform);
        
        // Set layer to PreviewOnly so it shows up in the accessory preview camera
        int LayerPreviewOnly = LayerMask.NameToLayer(layer);

        // Set layer recursively for the accessory and all its children
        SetLayerRecursively(accessoryInstance, LayerPreviewOnly);
        
        // Ensure it is not a prefab asset issue by setting it active
        accessoryInstance.SetActive(true);

        // Reset local transforms initially
        accessoryInstance.transform.localPosition = Vector3.zero;
        accessoryInstance.transform.localRotation = Quaternion.identity;
        accessoryInstance.transform.localScale = Vector3.one;

        // Wait a frame for renderers to initialize
        yield return null;
        
        // Calculate bounds of the accessory with scale 1
        Bounds bounds = CalculateRendererBounds(accessoryInstance);
        
        if (bounds.size == Vector3.zero)
        {
            // If no renderers found or invalid bounds, use a default size
            Debug.LogWarning($"No valid bounds found for {accessory.name}, using default.");
            bounds.size = new Vector3(0.2f, 0.2f, 0.2f);
            bounds.center = accessoryInstance.transform.position;
        }

        // Calculate distance from camera to previewPlaceholder
        float distanceToCamera = Vector3.Distance(camera.transform.position, previewPlaceholder.transform.position);
        
        // Calculate viewport height at this distance using camera FOV
        float viewportHeight = 2.0f * distanceToCamera * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        
        // Calculate viewport width using fixed 1:1 aspect ratio for the 256x256 render texture
        float viewportWidth = viewportHeight; // Equal to height for 1:1 aspect ratio
        
        // Get the size of the object (using the largest dimension)
        float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        
        // Calculate scale to fit within viewport with a margin
        float marginFactor = 0.6f; // Leave 40% margin to avoid clipping
        float scaleFactor = Mathf.Min(viewportWidth, viewportHeight) * marginFactor / largestDimension;
        
        // Apply the calculated scale
        accessoryInstance.transform.localScale = Vector3.one * scaleFactor;
        
        // Wait a frame for the renderers to update with new scale
        yield return null;
        
        // Get new bounds after scaling
        bounds = CalculateRendererBounds(accessoryInstance);
        
        // Center the object relative to the previewPlaceholder
        Vector3 offset = bounds.center - previewPlaceholder.transform.position;
        
        // Determine the distance to the camera to avoid clipping
        float cameraForwardDistance = Vector3.Dot(bounds.size, camera.transform.forward) * 0.5f;
        float safeDistance = camera.nearClipPlane + cameraForwardDistance + 0.05f; // Add a small buffer
        
        // Create a position that ensures the accessory is safely beyond the near clip plane
        Vector3 cameraPosition = camera.transform.position;
        Vector3 cameraForward = camera.transform.forward;
        Vector3 safePosition = cameraPosition + (cameraForward * safeDistance);
        
        // First center the object horizontally and vertically
        Vector3 newPosition = previewPlaceholder.transform.position - offset;
        
        // Then ensure it's at a safe distance from the camera along the camera's forward axis
        float currentDistance = Vector3.Dot(newPosition - cameraPosition, cameraForward);
        if (currentDistance < safeDistance)
        {
            // Move the object further away from the camera to avoid clipping
            newPosition = newPosition + cameraForward * (safeDistance - currentDistance);
            Debug.Log($"Adjusting {accessory.name} distance from camera to avoid clipping. New distance: {safeDistance}");
        }
        
        accessoryInstance.transform.position = newPosition;

        // Create a render texture for the camera to render to
        RenderTexture previewRenderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        
        if (previewRenderTexture != null)
        {
            camera.targetTexture = previewRenderTexture;
            camera.Render();
            Debug.Log($"Rendering accessory {accessory.name} to RenderTexture successful.");
        }
        else
        {
            Debug.LogWarning("Accessory RenderTexture is null.");
        }

        // Convert RenderTexture to Sprite for UI display
        yield return CapturePreview(previewRenderTexture, targetUI);

        // Delay the destruction of the accessory instance to ensure proper rendering
        yield return DestroyModelInstanceAfterRender(accessoryInstance);
    }
    private IEnumerator CapturePreview(RenderTexture renderTexture, Image targetUI)
    {
        yield return new WaitForEndOfFrame(); // Ensure rendering is complete

        RenderTexture.active = renderTexture;
        Texture2D previewTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        previewTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        previewTexture.Apply();
        RenderTexture.active = null;

        Sprite previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, previewTexture.width, previewTexture.height), new Vector2(0.5f, 0.5f));
        targetUI.sprite = previewSprite;
    }
    private IEnumerator DestroyModelInstanceAfterRender(GameObject modelInstance)
    {
        // Wait until the next frame to ensure rendering is complete
        yield return null;

        // modelInstance.Release();
        // Now destroy the model instance after it has been rendered and captured
        Destroy(modelInstance);
    }

    // Helper method to set layer for object and all its children
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // Helper method to calculate bounds from all renderers
    private Bounds CalculateRendererBounds(GameObject obj)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length > 0)
        {
            // Initialize with the first renderer's bounds
            bounds = renderers[0].bounds;
            
            // Expand to include all other renderers
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        
        return bounds;
    }

    private IEnumerator OnItemSelected(GameObject item)
    {
        while (true) {
            
            if (!avatarManager)
            {
                yield break;
            }
            
            var avatar = avatarManager.FindAvatar(roomClient.Me);
            if (avatar)
            {
                var itemAvatar = avatar.GetComponentInChildren<ItemAvatar>();
                if (itemAvatar)
                {
                    itemAvatar.SetItem(item);
                    yield break;
                }
            }
            
            // Wait a frame and try again
            yield return null;
            yield return null;
        }
    }

    private IEnumerator OnHatSelected(GameObject hat)
    {
        if (hat == null) {
            Debug.Log("removing hat");
        } else {
            Debug.Log($"Selecting hat {hat.name}");
        }
        while (true) {
            if (!avatarManager)
            {
                Debug.LogWarning("AvatarManager is missing.");
                yield break;
            }
            
            var avatar = avatarManager.FindAvatar(roomClient.Me);
            if (avatar)
            {
                var hatAvatar = avatar.GetComponentInChildren<HatAvatar>();
                if (hatAvatar)
                {
                    hatAvatar.SetHat(hat);
                    yield break;
                }
            }
            
            // Wait a frame and try again
            yield return null;
            yield return null;
        }
    }


    private void CreateCustomizedButton()
    {
        GameObject customizedButton = Instantiate(itemPrefab, contentPanel);
        TMP_Text buttonText = customizedButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = "Drawing Pen";
        }

        Button button = customizedButton.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnCustomizeButtonClicked());
        }
    }

    private void OnCustomizeButtonClicked()
    {
        Debug.Log("Customize button clicked!");
    }

    private void OnClothingItemSelected(Texture2D texture, BodyPart bodyPart)
    {
        Debug.Log($"Selected texture: {texture.name} for {bodyPart}");

        if (avatarManager != null && roomClient != null)
        {
            avatarManager.avatarPrefab = defaultAvatarPrefab;
            StartCoroutine(SetCostume(texture, bodyPart));
        }
        else
        {
            Debug.LogWarning("AvatarManager or RoomClient is missing.");
        }
    }

    private IEnumerator SetCostume(Texture2D selectedTexture, BodyPart bodyPart)
    {
        while (true)
        {
            var avatar = avatarManager.FindAvatar(roomClient.Me);
            if (avatar != null)
            {
                var textured = avatar.GetComponentInChildren<TexturedAvatar>();
                if (textured != null)
                {
                    textured.SetTexture(selectedTexture, bodyPart);
                    Debug.Log($"Applied texture {selectedTexture.name} to {bodyPart}");
                    yield break;
                }
            }
            yield return null;
        }
    }

    private BodyPart GetBodyPartForCategory(string category)
    {
        switch (category)
        {
            case "Head": return BodyPart.Head;
            case "Body": return BodyPart.Torso;
            case "LeftHand": return BodyPart.LeftHand;
            case "RightHand": return BodyPart.RightHand;
            default: return BodyPart.Torso;
        }
    }

}
