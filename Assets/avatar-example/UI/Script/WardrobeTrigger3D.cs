using UnityEngine;
using Ubiq.Avatars;
using Avatar = Ubiq.Avatars.Avatar;
using Ubiq.Messaging;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;

public class WardrobeTrigger3D : MonoBehaviour
{
    [Header("Main Camera")]
    public Camera mainCamera;         // Assign in Inspector (attached to avatar/player)

    [Header("Wardrobe Menu")]
    public GameObject menuPanel;      // Assign in Inspector (wardrobe UI panel)

    [Header("Camera Offset Settings")]
    public Vector3 positionOffset = new Vector3(0.15f, -0.05f, 1.8f); // Offset to position the camera behind avatar
    public Vector3 rotationOffset = new Vector3(1f, -0.1f, 0.4f); // Offset to position the camera behind avatar
    public float transitionSpeed = 2.0f; // Speed of smooth camera transition

    private AvatarManager avatarManager;
    private Avatar myAvatar;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    private bool isTransitioning = false;

    private void Start()
    {
        // Find AvatarManager dynamically
        avatarManager = NetworkScene.Find(this).GetComponentInChildren<AvatarManager>();

        if (avatarManager == null)
        {
            Debug.LogError("No AvatarManager found in scene!");
            return;
        }

        // Listen to avatar creation event
        avatarManager.OnAvatarCreated.AddListener(OnAvatarCreated);

        // Disable menu initially
        if (menuPanel != null) menuPanel.SetActive(false);

        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        interactable.selectEntered.AddListener(Interactable_SelectEntered);
    }

    private void OnDestroy()
    {
        if (interactable)
        {
            interactable.selectEntered.RemoveListener(Interactable_SelectEntered);
        }
    }

    // Called when local avatar is created
    void OnAvatarCreated(Avatar avatar)
    {
        if (avatar.IsLocal)
        {
            myAvatar = avatar;
            Debug.Log("Local Avatar found: " + avatar.name);
        }
    }

    // Detect interaction with this object (must have Collider attached)
    private void Interactable_SelectEntered(SelectEnterEventArgs arg0)
    {
        if (myAvatar == null || isTransitioning)
        {
            Debug.LogWarning("Avatar not ready or transition in progress");
            return;
        }

        Debug.Log("Clicked on: " + gameObject.name);

        // Toggle wardrobe menu
        if (menuPanel != null)
        {
            bool menuActive = !menuPanel.activeSelf; 
            menuPanel.SetActive(menuActive);
            Debug.Log("Menu Status when Click: " + menuActive);
        }
    }
}
