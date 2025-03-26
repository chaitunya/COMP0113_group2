using System;
using Ubiq.Avatars;
using UnityEngine.Events;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;
using Ubiq.Rooms;
using Ubiq.Messaging;
using System.Collections.Generic;


/// <summary>
/// This class sets the avatar to use a specific texture. It also handles
/// syncing the currently active texture over the network using properties.
/// </summary>
public class TexturedAvatar : MonoBehaviour
{
    public AvatarTextureCatalogue Textures;
    public bool RandomTextureOnSpawn;
    public bool SaveTextureSetting;

    [Serializable]
    public class TextureEvent : UnityEvent<Texture2D> { }
    // public TextureEvent OnTextureChanged;

    private Avatar avatar;
    // private string uuid;
    private RoomClient roomClient;

    // private Texture2D cached; // Cache for GetTexture. Do not do anything else with this; use the uuid

    // Event to notify body part texture change
    public UnityEvent<BodyPartTextureChange> OnBodyPartTextureChanged = new UnityEvent<BodyPartTextureChange>();

    // Store texture UUIDs per body part for saving/loading
    private Dictionary<BodyPart, string> textureUuids = new Dictionary<BodyPart, string>();


    private void Start()
    {
        roomClient = NetworkScene.Find(this).GetComponentInChildren<RoomClient>();
        
        avatar = GetComponent<Avatar>();

        if (SaveTextureSetting)
        {
            LoadSettings();
        }

        if (avatar.IsLocal && RandomTextureOnSpawn && textureUuids.Count == 0)
        {
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                SetTexture(Textures.Get(UnityEngine.Random.Range(0, Textures.Count)), part);
                // SetTexture("1", part);
                Debug.Log("Param1: " + Textures.Get(UnityEngine.Random.Range(0, Textures.Count)));
            }
        }

        
        roomClient.OnPeerUpdated.AddListener(RoomClient_OnPeerUpdated);
    }

    private void OnDestroy()
    {
        // Cleanup the event for new properties so it does not get called after
        // we have been destroyed.
        if (roomClient)
        {
            roomClient.OnPeerUpdated.RemoveListener(RoomClient_OnPeerUpdated);
        }
    }

    void RoomClient_OnPeerUpdated(IPeer peer)
    {
        if (peer != avatar.Peer)
        {
            // The peer who is being updated is not our peer, so we can safely
            // ignore this event.
            return;
        }
        
        // SetTexture(peer["ubiq.avatar.texture.uuid"]);
        foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
        {
            string key = $"ubiq.avatar.texture.{part.ToString().ToLower()}.uuid";
            
            string uuid = peer[key]; // Get the value for the corresponding key

            if (!string.IsNullOrEmpty(uuid))
            {
                Texture2D texture = Textures.Get(uuid);
                // If a valid UUID is found, set the texture for the corresponding body part
                SetTexture(texture, part);
            }
        }
    }

    /// <summary>
    /// Try to set the Texture by reference to a Texture in the Catalogue. If the Texture is not in the
    /// catalogue then this method has no effect, as Texture2Ds cannot be streamed yet.
    /// </summary>
    // public void SetTexture(Texture2D texture)
    // {
    //     SetTexture(Textures.Get(texture));
    // }

    // public void SetTexture(string uuid)
    // {
    //     if(String.IsNullOrWhiteSpace(uuid))
    //     {
    //         return;
    //     }

    //     if (this.uuid != uuid)
    //     {
    //         var texture = Textures.Get(uuid);
    //         this.uuid = uuid;
    //         this.cached = texture;

    //         OnTextureChanged.Invoke(texture);

    //         if(avatar.IsLocal)
    //         {
    //             roomClient.Me["ubiq.avatar.texture.uuid"] = this.uuid;
    //         }

    //         if (avatar.IsLocal && SaveTextureSetting)
    //         {
    //             SaveSettings();
    //         }
    //     }
    // }
    public void SetTexture(Texture2D texture, BodyPart bodyPart)
    {
        if (texture == null) return;

        // Get the UUID associated with the texture, assuming `Textures.Get` can handle this
        string uuid = Textures.Get(texture); // This assumes you have a way to get UUID from Texture2D

        if (string.IsNullOrWhiteSpace(uuid)) return;

        // Save UUID for body part
        textureUuids[bodyPart] = uuid;

        // Fire event to notify avatar to update this part
        OnBodyPartTextureChanged.Invoke(new BodyPartTextureChange(bodyPart, texture));
        Debug.Log($"Texture set for {bodyPart}: {uuid}");
        
        // Update shared roomClient properties if local
        if (avatar.IsLocal)
        {
            roomClient.Me[$"ubiq.avatar.texture.{bodyPart.ToString().ToLower()}.uuid"] = uuid;
            Debug.Log($"Updated texture for {bodyPart} in roomClient properties.");
        }

        // Save settings if enabled
        if (avatar.IsLocal && SaveTextureSetting)
        {
            SaveSettings();
        }
    }


    // private void SaveSettings()
    // {
    //     PlayerPrefs.SetString("ubiq.avatar.texture.uuid", uuid);
    // }
    private void SaveSettings()
    {
        foreach (var entry in textureUuids)
        {
            PlayerPrefs.SetString($"avatar.texture.{entry.Key}", entry.Value);
        }
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
        {
            if (PlayerPrefs.HasKey($"avatar.texture.{part}"))
            {
                string uuid = PlayerPrefs.GetString($"avatar.texture.{part}");
                Texture2D texture = Textures.Get(uuid);
                SetTexture(texture, part); // Will apply and sync
            }
        }
    }

    public void ClearSettings()
    {
        foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
        {
            PlayerPrefs.DeleteKey($"avatar.texture.{part}");
        }
        PlayerPrefs.Save();
    }

    // public Texture2D GetTexture()
    // {
    //     return cached;
    // }
}
