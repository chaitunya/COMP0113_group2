using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using Ubiq.Messaging;
using System;
using System.Linq;

public class Pen : MonoBehaviour
{
    private Transform nib;
    private Transform eraser;

    private NetworkContext context;
    [SerializeField] private string objectId = "pen-" + Guid.NewGuid().ToString();
    private bool hasOwnership = false;

    [SerializeField]
    private string targetMaterialName = "Mat_Drawing";

    float brushSize = 75f;
    float brushRed = 0.5f;
    float brushGreen = 0.5f;
    float brushBlue = 0.5f;

    private string myAvatarId = null;
    private const string myAvatarPrefix = "My Avatar #";
    private const string remoteAvatarPrefix = "Remote Avatar #";
    private const string avatarManagerPath = "Ubiq Network Scene (Demo)/Avatar Manager";

    public Color brushColor
    {
        get { return new Color(brushRed, brushGreen, brushBlue, 1.0f); }
    }

    public Color clearColor
    {
        get { return new Color(1.0f, 1.0f, 1.0f, 0.0f); }
    }

    private bool isDrawing = false;
    private Dictionary<Renderer, Texture2D> modifiedTextures = new Dictionary<Renderer, Texture2D>();

    [SerializeField] private MeshRenderer penBodyRenderer;

    [Serializable]
    private struct DrawMessage
    {
        public string rendererPath;
        public Vector2Int pixelPosition;
        public float red;
        public float green;
        public float blue;
        public float alpha;
        public float size;
        public string messageType;  // "draw"
    }

    [Serializable]
    private struct BrushSettingsMessage
    {
        public float red;
        public float green;
        public float blue;
        public float size;
        public string messageType;  // "brush"
    }

    [Serializable]
    private struct TransformMessage
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool isDrawing;
        public string messageType;  // "transform"

        public TransformMessage(Transform transform, bool isDrawing)
        {
            position = transform.position;
            rotation = transform.rotation;
            this.isDrawing = isDrawing;
            messageType = "transform";
        }
    }

    [Serializable]
    private struct OwnershipMessage
    {
        public string ownerId;
        public bool takeOwnership;
        public string messageType;  // "ownership"
    }

    [Serializable]
    private struct MessageBase
    {
        public string messageType;
    }

    private string ownerId;

    private void Awake()
    {
        if (nib == null)
            nib = transform.Find("Grip/Nib");
        if (eraser == null)
            eraser = transform.Find("Grip/Eraser");
    }

    private void Start()
    {
        context = NetworkScene.Register(this);
        ownerId = SystemInfo.deviceUniqueIdentifier + "-" + UnityEngine.Random.Range(0, 99999);
        hasOwnership = true;
        UpdateVisuals();
    }

    private void Update()
    {
        if (hasOwnership && isDrawing)
        {
            DrawOnSurface();
        }
    }

    private void FixedUpdate()
    {
        context.SendJson(new TransformMessage(transform, isDrawing));
    }

    public void XRGrabInteractable_Activated(ActivateEventArgs eventArgs)
    {
        isDrawing = true;
        TakeOwnership();
    }

    public void XRGrabInteractable_Deactivated(DeactivateEventArgs eventArgs)
    {
        isDrawing = false;
    }

    private void DrawOnSurface()
    {
        Ray ray = new Ray(nib.position, nib.up);
        RaycastHit raycastHit;

        Ray eraserRay = new Ray(eraser.position, -eraser.up);
        RaycastHit eraserRaycastHit;

        float maxDistance = 0.2f;
        float eraserMaxDistance = 1.0f;

        if (Physics.Raycast(ray, out raycastHit, maxDistance))
        {
            Renderer renderer = raycastHit.collider.GetComponent<Renderer>();

            if (renderer != null && renderer.sharedMaterial.name.StartsWith(targetMaterialName))
            {

                Vector2 textureCoord = raycastHit.textureCoord;
                Texture2D dirtMaskTexture = GetMaskTexture(renderer);

                int pixelX = (int)(textureCoord.x * dirtMaskTexture.width);
                int pixelY = (int)(textureCoord.y * dirtMaskTexture.height);

                Vector2Int paintPixelPosition = new Vector2Int(pixelX, pixelY);

                PaintAtPosition(dirtMaskTexture, paintPixelPosition, brushColor);
                SendDrawMessage(renderer, paintPixelPosition, brushColor, brushSize);
            }
        }

        if (Physics.Raycast(eraserRay, out eraserRaycastHit, eraserMaxDistance))
        {
            Renderer renderer = eraserRaycastHit.collider.GetComponent<Renderer>();

            if (renderer != null && renderer.material.name.StartsWith(targetMaterialName))
            {
                Vector2 textureCoord = eraserRaycastHit.textureCoord;
                Texture2D dirtMaskTexture = GetMaskTexture(renderer);

                int pixelX = (int)(textureCoord.x * dirtMaskTexture.width);
                int pixelY = (int)(textureCoord.y * dirtMaskTexture.height);

                Vector2Int paintPixelPosition = new Vector2Int(pixelX, pixelY);

                PaintAtPosition(dirtMaskTexture, paintPixelPosition, clearColor);
                SendDrawMessage(renderer, paintPixelPosition, clearColor, brushSize);
            }
        }
    }

    private void SendDrawMessage(Renderer renderer, Vector2Int pixelPosition, Color color, float size)
    {
        string rendererPath = GetFullPath(renderer.gameObject);
        context.SendJson(new DrawMessage
        {
            rendererPath = rendererPath,
            pixelPosition = pixelPosition,
            red = color.r,
            green = color.g,
            blue = color.b,
            alpha = color.a,
            size = size,
            messageType = "draw"
        });
    }

    private string GetFullPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private string FindMyAvatarId()
    {
        GameObject avatarManager = GameObject.Find(avatarManagerPath);

        foreach (Transform child in avatarManager.transform)
        {
            if (child.name.StartsWith(myAvatarPrefix))
            {
                return child.name.Substring(myAvatarPrefix.Length);
            }
        }

        return null;
    }

    private Texture2D GetMaskTexture(Renderer renderer)
    {
        Texture2D dirtMaskTexture;
        if (modifiedTextures.TryGetValue(renderer, out dirtMaskTexture))
        {
            return dirtMaskTexture;
        }

        Material material = renderer.material;
        Texture originalTexture = material.GetTexture("_DirtMaskTexture");

        if (originalTexture != null)
        {
            dirtMaskTexture = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGBA32, false);

            RenderTexture tempRT = RenderTexture.GetTemporary(
                originalTexture.width,
                originalTexture.height,
                0,
                RenderTextureFormat.ARGB32);

            Graphics.Blit(originalTexture, tempRT);
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            dirtMaskTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            dirtMaskTexture.Apply();

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(tempRT);
        }

        material.SetTexture("_DirtMaskTexture", dirtMaskTexture);
        modifiedTextures[renderer] = dirtMaskTexture;

        return dirtMaskTexture;
    }

    private void PaintAtPosition(Texture2D dirtMaskTexture, Vector2Int centerPixel, Color colour)
    {
        for (int x = -Mathf.FloorToInt(brushSize / 2); x < Mathf.CeilToInt(brushSize / 2); x++)
        {
            for (int y = -Mathf.FloorToInt(brushSize / 2); y < Mathf.CeilToInt(brushSize / 2); y++)
            {
                int drawX = centerPixel.x + x;
                int drawY = centerPixel.y + y;

                if (drawX >= 0 && drawX < dirtMaskTexture.width &&
                    drawY >= 0 && drawY < dirtMaskTexture.height)
                {
                    float distSquared = x * x + y * y;
                    if (distSquared <= (brushSize / 2) * (brushSize / 2))
                    {
                        dirtMaskTexture.SetPixel(drawX, drawY, colour);
                    }
                }
            }
        }
        dirtMaskTexture.Apply();
    }

    public void SetBrushSize(float size)
    {
        brushSize = size;
        SendBrushSettingsUpdate();
    }

    public void SetBrushRed(float red)
    {
        brushRed = Mathf.Clamp01(red);
        SendBrushSettingsUpdate();
    }

    public void SetBrushGreen(float green)
    {
        brushGreen = Mathf.Clamp01(green);
        SendBrushSettingsUpdate();
    }

    public void SetBrushBlue(float blue)
    {
        brushBlue = Mathf.Clamp01(blue);
        SendBrushSettingsUpdate();
    }

    private void SendBrushSettingsUpdate()
    {
        context.SendJson(new BrushSettingsMessage
        {
            red = brushRed,
            green = brushGreen,
            blue = brushBlue,
            size = brushSize,
            messageType = "brush"
        });
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {

        MessageBase baseMsg;
        try
        {
            baseMsg = message.FromJson<MessageBase>();
        }
        catch
        {
            return;
        }

        switch (baseMsg.messageType)
        {
            case "transform":
                HandleTransformMessage(message);
                break;

            case "draw":
                HandleDrawMessage(message);
                break;

            case "brush":
                HandleBrushSettingsMessage(message);
                break;

            case "ownership":
                HandleOwnershipMessage(message);
                break;

            default:
                break;
        }
    }

    private void HandleTransformMessage(ReferenceCountedSceneGraphMessage message)
    {
        var transformMsg = message.FromJson<TransformMessage>();
        transform.position = transformMsg.position;
        transform.rotation = transformMsg.rotation;
    }

    private void HandleDrawMessage(ReferenceCountedSceneGraphMessage message)
    {
        var drawMsg = message.FromJson<DrawMessage>();
        GameObject targetObj = FindGameObjectByPath(drawMsg.rendererPath);

        if (targetObj != null)
        {
            Renderer renderer = targetObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                ApplyDrawMessage(renderer, drawMsg);
            }
        }
    }

    public static GameObject[] GetDontDestroyOnLoadObjects()
    {
        GameObject temp = null;
        try
        {
            temp = new GameObject();
            UnityEngine.Object.DontDestroyOnLoad(temp);
            UnityEngine.SceneManagement.Scene dontDestroyOnLoad = temp.scene;
            UnityEngine.Object.DestroyImmediate(temp);
            temp = null;

            return dontDestroyOnLoad.GetRootGameObjects();
        }
        finally
        {
            if (temp != null)
                UnityEngine.Object.DestroyImmediate(temp);
        }
    }

    private GameObject FindGameObjectByPath(string path)
    {
        string translatedPath = TranslateAvatarPath(path);

        if (translatedPath != path)
        {
            path = translatedPath;
        }
        string[] pathParts = path.Split('/');

        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        GameObject[] dontDestroyOnLoadObjects = GetDontDestroyOnLoadObjects();
        rootObjects = rootObjects.Concat(dontDestroyOnLoadObjects).ToArray();

        Transform current = null;
        foreach (GameObject root in rootObjects)
        {
            if (root.name == pathParts[0])
            {
                current = root.transform;
                break;
            }
        }

        if (current == null)
        {
            return null;
        }

        for (int i = 1; i < pathParts.Length; i++)
        {
            Transform child = current.Find(pathParts[i]);
            if (child == null)
            {
                int childCount = current.childCount;
                string[] childNames = new string[childCount];
                for (int c = 0; c < childCount; c++)
                {
                    childNames[c] = current.GetChild(c).name;
                }

                return null;
            }

            current = child;
        }

        return current.gameObject;
    }

    private string TranslateAvatarPath(string path)
    {
        myAvatarId = FindMyAvatarId();

        if (path.Contains(myAvatarPrefix))
        {
            int startIndex = path.IndexOf(myAvatarPrefix);
            int idStartIndex = startIndex + myAvatarPrefix.Length;
            int endIndex = path.IndexOf('/', idStartIndex);
            string avatarId = endIndex > idStartIndex ?
                              path.Substring(idStartIndex, endIndex - idStartIndex) :
                              path.Substring(idStartIndex);

            if (avatarId != myAvatarId)
            {
                return path.Replace(myAvatarPrefix + avatarId, remoteAvatarPrefix + avatarId);
            }
        }
        else if (path.Contains(remoteAvatarPrefix))
        {
            int startIndex = path.IndexOf(remoteAvatarPrefix);
            int idStartIndex = startIndex + remoteAvatarPrefix.Length;
            int endIndex = path.IndexOf('/', idStartIndex);
            string avatarId = endIndex > idStartIndex ?
                              path.Substring(idStartIndex, endIndex - idStartIndex) :
                              path.Substring(idStartIndex);

            if (avatarId == myAvatarId)
            {
                return path.Replace(remoteAvatarPrefix + avatarId, myAvatarPrefix + avatarId);
            }
        }

        return path;
    }

    private void HandleBrushSettingsMessage(ReferenceCountedSceneGraphMessage message)
    {
        var brushMsg = message.FromJson<BrushSettingsMessage>();
        brushRed = brushMsg.red;
        brushGreen = brushMsg.green;
        brushBlue = brushMsg.blue;
        brushSize = brushMsg.size;

        UpdateVisuals();
    }

    private void HandleOwnershipMessage(ReferenceCountedSceneGraphMessage message)
    {
        var ownerMsg = message.FromJson<OwnershipMessage>();
        ownerId = ownerMsg.ownerId;

        if (ownerMsg.takeOwnership && ownerId != GetLocalPlayerId())
        {
            hasOwnership = false;
            UpdateVisuals();
        }
    }

    private void ApplyDrawMessage(Renderer renderer, DrawMessage drawMsg)
    {
        if (renderer != null)
        {
            Texture2D dirtMaskTexture = GetMaskTexture(renderer);
            Color color = new Color(drawMsg.red, drawMsg.green, drawMsg.blue, drawMsg.alpha);
            Vector2Int pixelPosition = drawMsg.pixelPosition;

            float originalBrushSize = brushSize;
            brushSize = drawMsg.size;
            PaintAtPosition(dirtMaskTexture, pixelPosition, color);
            brushSize = originalBrushSize;
        }
    }

    private void TakeOwnership()
    {
        string localPlayerId = GetLocalPlayerId();

        if (ownerId != localPlayerId)
        {
            ownerId = localPlayerId;
            hasOwnership = true;
            UpdateVisuals();
            context.SendJson(new OwnershipMessage
            {
                ownerId = localPlayerId,
                takeOwnership = true,
                messageType = "ownership"
            });
        }
    }

    private string GetLocalPlayerId()
    {
        return SystemInfo.deviceUniqueIdentifier + "-" + UnityEngine.Random.Range(0, 99999);
    }

    private void UpdateVisuals()
    {
        if (penBodyRenderer != null)
        {
            Material mat = penBodyRenderer.material;

            if (!mat.name.EndsWith("(Instance)"))
            {
                mat = new Material(mat);
                penBodyRenderer.material = mat;
            }

            Color baseColor = brushColor;
            mat.color = hasOwnership ?
                new Color(baseColor.r, baseColor.g, baseColor.b, 1.0f) :
                new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, 0.7f);
        }
    }
}