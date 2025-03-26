using System;
using Ubiq.Avatars;
using UnityEngine.Events;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;
using Ubiq.Rooms;
using Ubiq.Messaging;
using System.Collections.Generic;


public class TestTexture : MonoBehaviour
{
    public AvatarTextureCatalogue Textures;
    public TexturedAvatar texturedAvatar;  // Assign your TexturedAvatar here
    public string testTextureUuid;  // Set a valid UUID of a texture

    // Example to trigger texture change via a key press
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))  // Press 'T' to test
        {
            Texture2D texture = Textures.Get(testTextureUuid);
            
            // Check if the texture is valid
            if (texture != null)
            {
                // Apply the texture to the body part (e.g., Head)
                texturedAvatar.SetTexture(texture, BodyPart.Head);
            }
            else
            {
                Debug.LogError("Texture not found for UUID: " + testTextureUuid);
            }
        }
    }
}

