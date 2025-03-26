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

public class AvatarMirroring : MonoBehaviour
{
    public Plane plane;

    public enum Plane
    {
        XY,
        YZ
    }

    public Transform wardrobeMirrorPosition;
    private bool wardrobeOpen = false;
    private AvatarManager avatarManager;
    private RoomClient roomClient;
    private List<Renderer> renderers = new();
    private Transform _transform;
    private Dictionary<Material, Material> materials = new();
    public Vector3 mirrorOffset = new Vector3(0f, 0f, 2f);  // Default offset for general objects
    public Vector3 headMirrorOffset = new Vector3(0f, 0f, 0f);  // Custom offset for the head
    public Vector3 bodyMirrorOffset = new Vector3(0f, 0f, 0f);  // Custom offset for the body
    public Vector3 leftHandMirrorOffset = new Vector3(0f, 0f, 0f);  // Custom offset for the head
    public Vector3 rightHandMirrorOffset = new Vector3(0f, 0f, 0f);  // Custom offset for the body

    private void Awake()
    {
        _transform = transform;
    }

    private Dictionary<Transform, Vector3> fixedMirrorPositions = new();
    private Dictionary<Transform, Quaternion> fixedMirrorRotations = new();

    private void Start()
    {
        avatarManager = NetworkScene.Find(this).GetComponentInChildren<AvatarManager>();
        roomClient = NetworkScene.Find(this).GetComponentInChildren<RoomClient>();

        // Find the avatar corresponding to the local player (roomClient.Me)
        var avatar = avatarManager.FindAvatar(roomClient.Me);

        if (avatar != null)
        {
            // Find the body parts of the avatar
            Transform body = avatar.transform.Find("Body/Floating_Torso_A");
            Transform head = avatar.transform.Find("Body/Floating_Head");
            Transform leftHand = avatar.transform.Find("Body/Floating_LeftHand_A");
            Transform rightHand = avatar.transform.Find("Body/Floating_RightHand_A");

            if (head != null && body != null && leftHand != null && rightHand != null)
            {
                // Set positions and rotations of body parts to zero initially
                head.localPosition = Vector3.zero;
                head.localRotation = Quaternion.identity;
                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
                leftHand.localPosition = Vector3.zero;
                leftHand.localRotation = Quaternion.identity;
                rightHand.localPosition = Vector3.zero;
                rightHand.localRotation = Quaternion.identity;
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var material in materials.Values)
        {
            Destroy(material);
        }
        materials = null;
    }

    public void SetWardrobeMirror(bool open)
    {
        wardrobeOpen = open;

        if (open && wardrobeMirrorPosition != null)
        {
            // Move mirrored avatars to fixed position and store them
            for (int ai = 0; ai < avatarManager.transform.childCount; ai++)
            {
                var avatar = avatarManager.transform.GetChild(ai);

                avatar.position = fixedMirrorPositions[avatar];
                avatar.rotation = fixedMirrorRotations[avatar];
            }
        }
    }

    private void Update()
    {
        if (wardrobeOpen)
        {
            return; // Prevent mirrored avatars from moving once placed
        }

        UpdatePlane(out var scaleMultiplier, out var eulerMultiplier);

        Vector3 fixedWardrobePosition = wardrobeMirrorPosition.position;

        for (int ai = 0; ai < avatarManager.transform.childCount; ai++)
        {
            var avatar = avatarManager.transform.GetChild(ai);

            // Find the body parts of the avatar
            Transform body = avatar.transform.Find("Body/Floating_Torso_A");
            Transform head = avatar.transform.Find("Body/Floating_Head");
            Transform leftHand = avatar.transform.Find("Body/Floating_LeftHand_A");
            Transform rightHand = avatar.transform.Find("Body/Floating_RightHand_A");

            if (head != null && body != null && leftHand != null && rightHand != null)
            {
                // Apply fixed position and rotation logic to body parts separately
                head.localPosition = Vector3.zero;
                head.localRotation = Quaternion.identity;
                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
                leftHand.localPosition = Vector3.zero;
                leftHand.localRotation = Quaternion.identity;
                rightHand.localPosition = Vector3.zero;
                rightHand.localRotation = Quaternion.identity;

                renderers.Clear();
                avatar.GetComponentsInChildren(includeInactive: false, renderers);

                // Mirror the head with the custom head mirror offset
                for (int i = 0; i < renderers.Count; i++)
                {
                    var renderer = renderers[i];
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!filter) continue;

                    var rendererTransform = renderer.GetComponent<Transform>();

                    if (rendererTransform == head)
                    {
                        var mat = renderer.localToWorldMatrix;
                        mat = Matrix4x4.Translate(-rendererTransform.position) * mat;
                        mat = Matrix4x4.Rotate(Quaternion.Inverse(rendererTransform.rotation)) * mat;
                        mat = Matrix4x4.Scale(scaleMultiplier) * mat;

                        var eul = rendererTransform.rotation.eulerAngles;
                        eul.Scale(eulerMultiplier);
                        mat = Matrix4x4.Rotate(Quaternion.Euler(eul)) * mat;
                        mat = Matrix4x4.Translate(rendererTransform.position) * mat;

                        // Set the position based on wardrobeMirrorPosition plus the custom head mirror offset
                        Vector3 mirroredPosition = fixedWardrobePosition + mirrorOffset +headMirrorOffset;
                        mat = Matrix4x4.Translate(mirroredPosition - rendererTransform.localPosition) * mat;

                        // Create a new material if needed to prevent conflicts
                        if (!materials.TryGetValue(renderer.sharedMaterial, out var material))
                        {
                            material = new Material(renderer.sharedMaterial); // Create a copy of the material
                            materials[renderer.sharedMaterial] = material;
                        }

                        // Draw the mesh with the adjusted material and matrix
                        Graphics.DrawMesh(
                            mesh: filter.mesh,
                            matrix: mat,
                            material: material,
                            layer: gameObject.layer);
                    }
                }

                // Mirror the body with the custom body mirror offset
                for (int i = 0; i < renderers.Count; i++)
                {
                    var renderer = renderers[i];
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!filter) continue;

                    var rendererTransform = renderer.GetComponent<Transform>();

                    if (rendererTransform == body)
                    {
                        var mat = renderer.localToWorldMatrix;
                        mat = Matrix4x4.Translate(-rendererTransform.position) * mat;
                        mat = Matrix4x4.Rotate(Quaternion.Inverse(rendererTransform.rotation)) * mat;
                        mat = Matrix4x4.Scale(scaleMultiplier) * mat;

                        var eul = rendererTransform.rotation.eulerAngles;
                        eul.Scale(eulerMultiplier);
                        mat = Matrix4x4.Rotate(Quaternion.Euler(eul)) * mat;
                        mat = Matrix4x4.Translate(rendererTransform.position) * mat;

                        // Set the position based on wardrobeMirrorPosition plus the custom body mirror offset
                        Vector3 mirroredPosition = fixedWardrobePosition + mirrorOffset + bodyMirrorOffset;
                        mat = Matrix4x4.Translate(mirroredPosition - rendererTransform.localPosition) * mat;

                        // Create a new material if needed to prevent conflicts
                        if (!materials.TryGetValue(renderer.sharedMaterial, out var material))
                        {
                            material = new Material(renderer.sharedMaterial); // Create a copy of the material
                            materials[renderer.sharedMaterial] = material;
                        }

                        // Draw the mesh with the adjusted material and matrix
                        Graphics.DrawMesh(
                            mesh: filter.mesh,
                            matrix: mat,
                            material: material,
                            layer: gameObject.layer);
                    }
                }

                // Mirror the left hand in a similar way
                for (int i = 0; i < renderers.Count; i++)
                {
                    var renderer = renderers[i];
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!filter) continue;

                    var rendererTransform = renderer.GetComponent<Transform>();

                    if (rendererTransform == leftHand)
                    {
                        var mat = renderer.localToWorldMatrix;
                        mat = Matrix4x4.Translate(-rendererTransform.position) * mat;
                        mat = Matrix4x4.Rotate(Quaternion.Inverse(rendererTransform.rotation)) * mat;
                        mat = Matrix4x4.Scale(scaleMultiplier) * mat;

                        var eul = rendererTransform.rotation.eulerAngles;
                        eul.Scale(eulerMultiplier);
                        mat = Matrix4x4.Rotate(Quaternion.Euler(eul)) * mat;
                        mat = Matrix4x4.Translate(rendererTransform.position) * mat;

                        // Set the position based on wardrobeMirrorPosition plus the general mirror offset
                        Vector3 mirroredPosition = fixedWardrobePosition + mirrorOffset + leftHandMirrorOffset;
                        mat = Matrix4x4.Translate(mirroredPosition - rendererTransform.localPosition) * mat;

                        // Create a new material if needed to prevent conflicts
                        if (!materials.TryGetValue(renderer.sharedMaterial, out var material))
                        {
                            material = new Material(renderer.sharedMaterial); // Create a copy of the material
                            materials[renderer.sharedMaterial] = material;
                        }

                        // Draw the mesh with the adjusted material and matrix
                        Graphics.DrawMesh(
                            mesh: filter.mesh,
                            matrix: mat,
                            material: material,
                            layer: gameObject.layer);
                    }
                }

                // Mirror the right hand in a similar way
                for (int i = 0; i < renderers.Count; i++)
                {
                    var renderer = renderers[i];
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!filter) continue;

                    var rendererTransform = renderer.GetComponent<Transform>();

                    if (rendererTransform == rightHand)
                    {
                        var mat = renderer.localToWorldMatrix;
                        mat = Matrix4x4.Translate(-rendererTransform.position) * mat;
                        mat = Matrix4x4.Rotate(Quaternion.Inverse(rendererTransform.rotation)) * mat;
                        mat = Matrix4x4.Scale(scaleMultiplier) * mat;

                        var eul = rendererTransform.rotation.eulerAngles;
                        eul.Scale(eulerMultiplier);
                        mat = Matrix4x4.Rotate(Quaternion.Euler(eul)) * mat;
                        mat = Matrix4x4.Translate(rendererTransform.position) * mat;

                        // Set the position based on wardrobeMirrorPosition plus the general mirror offset
                        Vector3 mirroredPosition = fixedWardrobePosition + mirrorOffset + rightHandMirrorOffset;
                        mat = Matrix4x4.Translate(mirroredPosition - rendererTransform.localPosition) * mat;

                        // Create a new material if needed to prevent conflicts
                        if (!materials.TryGetValue(renderer.sharedMaterial, out var material))
                        {
                            material = new Material(renderer.sharedMaterial); // Create a copy of the material
                            materials[renderer.sharedMaterial] = material;
                        }

                        // Draw the mesh with the adjusted material and matrix
                        Graphics.DrawMesh(
                            mesh: filter.mesh,
                            matrix: mat,
                            material: material,
                            layer: gameObject.layer);
                    }
                }
            }
        }
    }

    private void UpdatePlane(out Vector3 scaleMultiplier, out Vector3 eulerMultiplier)
    {
        switch (plane)
        {
            case Plane.XY:
                scaleMultiplier = new Vector3(1, 1, -1);
                eulerMultiplier = new Vector3(1, 1, -1);
                break;

            case Plane.YZ:
                scaleMultiplier = new Vector3(-1, 1, 1);
                eulerMultiplier = new Vector3(-1, 1, 1);
                break;

            default:
                scaleMultiplier = Vector3.one;
                eulerMultiplier = Vector3.one;
                break;
        }
    }
}
