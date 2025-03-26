using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class Pen : MonoBehaviour
{
    private Transform nib;
    private Material drawingMaterial;
    private GameObject currentDrawing;
    
    [SerializeField]
    private string targetMaterialName = "Mat_Drawing";
    
    [SerializeField]
    private float brushSize = 5f;
    
    [SerializeField]
    private Color brushColor = Color.black;
    
    private bool isDrawing = false;
    
    private Dictionary<Renderer, Texture2D> modifiedTextures = new Dictionary<Renderer, Texture2D>();

    private void Start()
    {
        nib = transform.Find("Grip/Nib");

        var shader = Shader.Find("Particles/Standard Unlit");
        drawingMaterial = new Material(shader);

        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.activated.AddListener(XRGrabInteractable_Activated);
        grab.deactivated.AddListener(XRGrabInteractable_Deactivated);
    }
    
    private void Update()
    {
        DrawOnSurface();
    }

    private void XRGrabInteractable_Activated(ActivateEventArgs eventArgs)
    {
        isDrawing = true;
    }

    private void XRGrabInteractable_Deactivated(DeactivateEventArgs eventArgs)
    {
        isDrawing = false;
        
        if (currentDrawing != null)
        {
            EndDrawing();
        }
    }
    
    private void DrawOnSurface()
    {
        Ray ray = new Ray(nib.position, nib.forward);
        RaycastHit raycastHit;
        
        if (Physics.Raycast(ray, out raycastHit))
        {
            Debug.Log("Hit: " + raycastHit.collider.name);
            Renderer renderer = raycastHit.collider.GetComponent<Renderer>();
            
            if (renderer != null && renderer.material.name.StartsWith(targetMaterialName))
            {
                if (currentDrawing != null)
                {
                    EndDrawing();
                }
                
                Vector2 textureCoord = raycastHit.textureCoord;
                
                Texture2D dirtMaskTexture = GetMaskTexture(renderer);
                
                int pixelX = (int)(textureCoord.x * dirtMaskTexture.width);
                int pixelY = (int)(textureCoord.y * dirtMaskTexture.height);
                
                Vector2Int paintPixelPosition = new Vector2Int(pixelX, pixelY);
                Debug.Log("UV: " + textureCoord + "; Pixels: " + paintPixelPosition);
                
                PaintAtPosition(dirtMaskTexture, paintPixelPosition);
            }
            else
            {
                if (currentDrawing == null)
                {
                    // BeginDrawing();
                }
            }
        }
        else
        {
            if (currentDrawing == null)
            {
                // BeginDrawing();
            }
        }
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
    
    private void PaintAtPosition(Texture2D dirtMaskTexture, Vector2Int centerPixel)
    {
        for (int x = -Mathf.FloorToInt(brushSize/2); x < Mathf.CeilToInt(brushSize/2); x++)
        {
            for (int y = -Mathf.FloorToInt(brushSize/2); y < Mathf.CeilToInt(brushSize/2); y++)
            {
                int drawX = centerPixel.x + x;
                int drawY = centerPixel.y + y;
                
                if (drawX >= 0 && drawX < dirtMaskTexture.width && 
                    drawY >= 0 && drawY < dirtMaskTexture.height)
                {
                    float distSquared = x*x + y*y;
                    if (distSquared <= (brushSize/2)*(brushSize/2))
                    {
                        dirtMaskTexture.SetPixel(drawX, drawY, brushColor);
                    }
                }
            }
        }
        dirtMaskTexture.Apply();
    }
    
    private void BeginDrawing()
    {
        currentDrawing = new GameObject("Drawing");
        var trail = currentDrawing.AddComponent<TrailRenderer>();
        trail.time = Mathf.Infinity;
        trail.material = drawingMaterial;
        trail.startWidth = .05f;
        trail.endWidth = .05f;
        trail.minVertexDistance = .02f;

        currentDrawing.transform.parent = nib.transform;
        currentDrawing.transform.localPosition = Vector3.zero;
        currentDrawing.transform.localRotation = Quaternion.identity;
    }

    private void EndDrawing()
    {
        currentDrawing.transform.parent = null;
        currentDrawing.GetComponent<TrailRenderer>().emitting = false;
        currentDrawing = null;
    }
}
