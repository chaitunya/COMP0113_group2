using System;
using System.Collections.Generic;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Ubiq.Rooms;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Avatar = Ubiq.Avatars.Avatar;

public class HatAvatar : MonoBehaviour
{
    public GameObject[] hats;

    private Avatar avatar;
    private RoomClient roomClient;

    [Serializable]
    private struct SerializableHat
    {
        public int index;
    }

    private string lastHat;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        avatar = GetComponentInParent<Avatar>();
        var networkScene = NetworkScene.Find(this);
        roomClient = networkScene.GetComponentInChildren<RoomClient>();
        
        // Connect up a listener for whenever any peer's properties are changed. 
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

    public void SetHat(int index) {
        if (!avatar.IsLocal)
        {
            return;
        }
        if (index < -1)
        {
            Debug.Log("Unrecognized index");
            return;
        }

        var serializedHat = JsonUtility.ToJson(new SerializableHat
        {
            index = index
        });
        ProcessHat(serializedHat);
        roomClient.Me["hat-avatar"] = serializedHat;
    }

    public void SetHat(GameObject hat) {
        var index = Array.IndexOf(hats, hat);
        SetHat(index);
    }

    private void RoomClient_OnPeerUpdated(IPeer peer)
    {
        if (peer != avatar.Peer)
        {
            // The peer who is being updated is not our peer, so we can safely
            // ignore this event.
            return;
        }
        ProcessHat(peer["hat-avatar"]);
    }

    private void ProcessHat(string serializedHat)
    {
        if (String.IsNullOrEmpty(serializedHat) || serializedHat == lastHat) {
            return;
        }
        var index = JsonUtility.FromJson<SerializableHat>(serializedHat).index;
        if (index < -1 || index >= hats.Length)
        {
            Debug.LogWarning("Unrecognized hat received as property.");
            return;
        }
        // Deactivate other hats and activate current
        var transform = FindHatTransform(avatar.transform);
        if (transform) {
            foreach (Transform child in transform) {
                Destroy(child.gameObject);
            }
            // if index is -1, just clear out
            if (index != -1) {
                Debug.Log("Adding hat");
                Debug.Log($"Hat index {index}");
                var hatPrefab = hats[index];
                GameObject hat = Instantiate(hatPrefab,
                    transform);
                hat.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                hat.transform.localScale = Vector3.one;
                if (avatar.Peer == roomClient.Me) {
                    int layer = LayerMask.NameToLayer("Hat");
                    if (layer < 0) {
                        Debug.LogWarning("Could not find layer Hat");
                    } else {
                        Debug.Log($"Setting local avatar {roomClient.Me} layer to Hat");
                        SetLayerRecursively(hat, layer);
                    }
                }
            }
        } else {
            Debug.LogWarning("Could not find transform");
        }
        lastHat = serializedHat;
    }
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null)
            return;
            
        obj.layer = newLayer;
        
        foreach (Transform child in obj.transform)
        {
            if (child == null)
                continue;
                
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
    private Transform FindHatTransform(Transform avatarRoot)
    {
        Transform body = avatarRoot.Find("Body");
        if (body != null)
        {
            Transform head = body.Find("Floating_Head");
            if (head != null)
            {
                Transform bag = head.Find("Hat");
                if (bag != null)
                    return bag;
                else
                    Debug.Log("Could not find hat");
            } else {
                Debug.Log("Could not find head");
            }
        } else {
            Debug.Log("Could not find body");
        }
        
        Debug.LogWarning("Could not find hat transform in avatar hierarchy");
        return null;
    }
}
