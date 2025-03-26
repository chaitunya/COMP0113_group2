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
using UnityEngine.Rendering;

public class AvatarMirroring : MonoBehaviour
{
    public enum MirrorPlane
    {
        XY,
        YZ
    }

    [Header("Mirror Configuration")]
    public MirrorPlane plane = MirrorPlane.XY;
    public Transform wardrobeMirrorPosition;
    
    [SerializeField] private bool enableMirroring = true;
    private bool wardrobeOpen = false;

    [Header("Mirror Offset")]
    public Vector3 mirrorPositionOffset = new Vector3(0, 0, 0);
    public Vector3 mirrorRotationOffset = new Vector3(0, 0, 0);

    private AvatarManager avatarManager;
    private RoomClient roomClient;
    private Transform _transform;

    // Material cache to prevent excessive material creation
    private Vector3 originalAvatarPosition;
    private Quaternion originalAvatarRotation;
    
    private Dictionary<Material, Material> mirrorMaterials = new();
    private List<Renderer> renderers = new();

    private void Awake()
    {
        _transform = transform;
    }

    private void Start()
    {
        avatarManager = NetworkScene.Find(this).GetComponentInChildren<AvatarManager>();
        roomClient = NetworkScene.Find(this).GetComponentInChildren<RoomClient>();
    }

    private void OnDestroy()
    {
        // Clean up dynamically created materials
        foreach (var material in mirrorMaterials.Values)
        {
            Destroy(material);
        }
        mirrorMaterials.Clear();
    }

    // Method to dynamically set the mirror position offset
    public void SetMirrorPositionOffset(Vector3 offset)
    {
        mirrorPositionOffset = offset;
    }

    // Method to dynamically set the mirror rotation offset
    public void SetMirrorRotationOffset(Vector3 rotationOffset)
    {
        mirrorRotationOffset = rotationOffset;
    }

    public void SetWardrobeMirror(bool open)
    {
        wardrobeOpen = open;
        enableMirroring = open;

        // Get the local player's avatar
        var myAvatar = avatarManager.FindAvatar(roomClient.Me);
        if (myAvatar == null) return;

        if (open && wardrobeMirrorPosition != null)
        {
            // Store original avatar position and rotation
            originalAvatarPosition = myAvatar.transform.position;
            originalAvatarRotation = myAvatar.transform.rotation;

            // Position the avatar at the wardrobe mirror position
            myAvatar.transform.position = wardrobeMirrorPosition.position;
            myAvatar.transform.rotation = wardrobeMirrorPosition.rotation;
        }
        else
        {
            // Restore original avatar position and rotation when wardrobe closes
            myAvatar.transform.position = originalAvatarPosition;
            myAvatar.transform.rotation = originalAvatarRotation;
        }
    }

    private void Update()
    {
        if (!enableMirroring) return;

        // Get the local player's avatar
        var myAvatar = avatarManager.FindAvatar(roomClient.Me);
        if (myAvatar == null) return;

        // Prepare for mirroring calculations
        Vector3 scaleMultiplier;
        Vector3 eulerMultiplier;
        Matrix4x4 mirrorMatrix = CalculateMirrorMatrix(out scaleMultiplier, out eulerMultiplier);

        // Clear and get all renderers from the avatar
        renderers.Clear();
        myAvatar.GetComponentsInChildren(includeInactive: false, renderers);

        // Mirror each renderer
        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            var filter = renderer.GetComponent<MeshFilter>();
            var rendererTransform = renderer.transform;

            if (!filter) continue;

            // Create mirrored material
            if (!mirrorMaterials.TryGetValue(renderer.sharedMaterial, out var material))
            {
                material = new Material(renderer.sharedMaterial);
                mirrorMaterials.Add(renderer.sharedMaterial, material);
                
                // Ensure both sides of the mesh are visible
                material.SetFloat("_Cull", (int)CullMode.Off);
            }

            // Calculate mirror transformation
            Matrix4x4 mat = CalculateRendererMirrorMatrix(renderer, mirrorMatrix, 
                                                          scaleMultiplier, 
                                                          eulerMultiplier);

            // Apply the custom position offset to the matrix
            mat = Matrix4x4.Translate(mirrorPositionOffset) * mat;

            // Apply the custom rotation offset
            Quaternion rotationOffset = Quaternion.Euler(mirrorRotationOffset);
            mat = Matrix4x4.Rotate(rotationOffset) * mat;

            // Render the mirrored mesh
            Graphics.DrawMesh(
                mesh: filter.mesh,
                matrix: mat,
                material: material,
                layer: gameObject.layer
            );
        }
    }

    private Matrix4x4 CalculateMirrorMatrix(out Vector3 scaleMultiplier, out Vector3 eulerMultiplier)
    {
        switch (plane)
        {
            case MirrorPlane.XY:
                scaleMultiplier = new Vector3(1, 1, -1);
                eulerMultiplier = new Vector3(-1, -1, 1);
                _transform.rotation = Quaternion.Euler(0, 0, 0);
                break;
            case MirrorPlane.YZ:
                scaleMultiplier = new Vector3(-1, 1, 1);
                eulerMultiplier = new Vector3(1, -1, 1);
                _transform.rotation = Quaternion.Euler(0, 90, 0);
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(plane), plane, null);
        }

        return Matrix4x4.TRS(_transform.position, _transform.rotation, Vector3.one);
    }

    private Matrix4x4 CalculateRendererMirrorMatrix(Renderer renderer, 
                                                    Matrix4x4 mirrorMatrix, 
                                                    Vector3 scaleMultiplier, 
                                                    Vector3 eulerMultiplier)
    {
        var rendererTransform = renderer.transform;
        var mat = renderer.localToWorldMatrix;

        // Translate to origin
        mat = Matrix4x4.Translate(-rendererTransform.position) * mat;
        
        // Align with the plane 
        mat = Matrix4x4.Rotate(Quaternion.Inverse(rendererTransform.rotation)) * mat;
        
        // 'Reflect' in the plane
        mat = Matrix4x4.Scale(scaleMultiplier) * mat;
        
        // Return renderer to position, inverting required angles
        var eul = rendererTransform.rotation.eulerAngles;
        eul.Scale(eulerMultiplier);
        mat = Matrix4x4.Rotate(Quaternion.Euler(eul)) * mat;
        mat = Matrix4x4.Translate(rendererTransform.position) * mat;
        
        // Project renderer onto mirror
        var mirrorToRenderer = rendererTransform.position - _transform.position;
        var closestPointOnMirror = _transform.position + _transform.right * 
            Vector3.Dot(mirrorToRenderer, _transform.right);
        
        var toMirror = closestPointOnMirror - rendererTransform.position;
        toMirror.y = 0;
        mat = Matrix4x4.Translate(toMirror * 2) * mat;

        return mat;
    }
}