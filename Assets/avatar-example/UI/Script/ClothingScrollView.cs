using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Ubiq.Rooms;


public class ClothingScrollView : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject itemPrefab;
    public Transform contentPanel;

    [Header("Avatar & Networking")]
    public GameObject defaultAvatarPrefab;
    private AvatarManager avatarManager;
    private RoomClient roomClient;

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

    void Start()
    {
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


            foreach (int textureIdx in textureList)
            {

                var texture = textureCatalogue.Get(textureIdx);
                
                GameObject newItem = Instantiate(itemPrefab, contentPanel);

                TMP_Text itemText = newItem.GetComponentInChildren<TMP_Text>();
                if (itemText != null)
                {
                    itemText.text = texture.name;
                }
                Image itemImage = newItem.GetComponentInChildren<Image>();
                if (itemImage != null && previewModel != null)
                {
                    yield return GeneratePreviewFromTexture(texture, previewModel, itemImage, camera, layer, targetPart);
                }

                if (newItem.TryGetComponent<Button>(out var button))
                {
                    button.onClick.AddListener(() => OnClothingItemSelected(texture, targetPart));
                }
            }
        }
        Debug.Log($"Loaded {category} items");
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


    private IEnumerator GeneratePreviewFromTexture(Texture2D texture,
        GameObject fbxModel, Image targetUI, Camera camera, string layer,
        BodyPart bodyPart)
    {
        GameObject modelInstance = Instantiate(fbxModel, previewPlaceholder.transform);
        
        // Set layer to PreviewOnly so it shows up in the preview camera
        int LayerPreviewOnly = LayerMask.NameToLayer(layer);
        SetLayerRecursively(modelInstance, LayerPreviewOnly);
        
        modelInstance.SetActive(true);

        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer && renderer.material)
            {
                // create a new material instance to avoid modifying the shared material
                renderer.material = new Material(renderer.material);
                renderer.material.mainTexture = texture;
                Debug.Log($"Applied texture {texture.name} to {renderer.gameObject.name}");
            }
        }

        yield return null;
        
        Camera previewCamera = camera;
        
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
        yield return CapturePreview(previewRenderTexture, targetUI);
        yield return DestroyModelInstanceAfterRender(modelInstance);
    }

    private RenderTexture ConvertTextureToRenderTexture(Texture2D texture)
    {
        RenderTexture renderTexture = new RenderTexture(texture.width, texture.height, 16, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, renderTexture);
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
            // add general prohibition sign
        }
        if (noneImage.TryGetComponent<Button>(out var noneButton))
        {
            noneButton.onClick.AddListener(() => StartCoroutine(OnItemSelected(null)));
        }

        foreach (GameObject item in itemAvatar.items) {
            GameObject newItem = Instantiate(itemPrefab, contentPanel);

            TMP_Text itemText = newItem.GetComponentInChildren<TMP_Text>();
            if (itemText != null)
            {
                itemText.text = item.name;
            }


            Image itemImage = newItem.GetComponentInChildren<Image>();
            if (itemImage != null)
            {
                yield return GeneratePreview(item, itemImage, itemCamera, "Preview_Item");
            }
            
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
            // add general prohibition sign
        }
        if (noneImage.TryGetComponent<Button>(out var noneButton))
        {
            noneButton.onClick.AddListener(() => StartCoroutine(OnHatSelected(null)));
        }
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

            Image itemImage = newItem.GetComponentInChildren<Image>();
            if (itemImage != null)
            {
                yield return GeneratePreview(hat, itemImage, hatCamera, "Preview_Hat");
            } else {
                Debug.Log("itemImage is none");
            }

            
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
        
        int LayerPreviewOnly = LayerMask.NameToLayer(layer);

        SetLayerRecursively(accessoryInstance, LayerPreviewOnly);
        
        accessoryInstance.SetActive(true);

        accessoryInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        accessoryInstance.transform.localScale = Vector3.one;

        yield return null;
        

        // this section calculates bounds of the accessory and scales it to fit
        // the preview
        Bounds bounds = CalculateRendererBounds(accessoryInstance);
        
        if (bounds.size == Vector3.zero)
        {
            Debug.LogWarning($"No valid bounds found for {accessory.name}, using default.");
            bounds.size = new Vector3(0.2f, 0.2f, 0.2f);
            bounds.center = accessoryInstance.transform.position;
        }
        var distanceToCamera = Vector3.Distance(camera.transform.position,
            previewPlaceholder.transform.position);
        var viewportHeight = 2.0f * distanceToCamera
            * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var largestDimension = Mathf.Max(bounds.size.x, bounds.size.y,
                                         bounds.size.z);
        var marginFactor = 0.6f;
        var scaleFactor = viewportHeight * marginFactor / largestDimension;
        accessoryInstance.transform.localScale = Vector3.one * scaleFactor;
        
        yield return null;

        bounds = CalculateRendererBounds(accessoryInstance);
        var offset = bounds.center - previewPlaceholder.transform.position;
        var cameraForwardDistance = Vector3.Dot(bounds.size,
                                                camera.transform.forward)
                                    * 0.5f;

        // safe distance for camera to view the item to avoid clipping
        var safeDistance = camera.nearClipPlane + cameraForwardDistance + 0.05f;

        var cameraPosition = camera.transform.position;
        var cameraForward = camera.transform.forward;


        var newPosition = previewPlaceholder.transform.position - offset;
        var currentDistance = Vector3.Dot(newPosition - cameraPosition, cameraForward);

        if (currentDistance < safeDistance)
        {
            newPosition += cameraForward * (safeDistance - currentDistance);
        }
        
        accessoryInstance.transform.position = newPosition;

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

        yield return CapturePreview(previewRenderTexture, targetUI);
        yield return DestroyModelInstanceAfterRender(accessoryInstance);
    }
    private IEnumerator CapturePreview(RenderTexture renderTexture, Image targetUI)
    {
        yield return new WaitForEndOfFrame();

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

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // Calculate size of GameObject
    private Bounds CalculateRendererBounds(GameObject obj)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            
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
