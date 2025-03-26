using System;
using System.Collections.Generic;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Ubiq.Rooms;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Avatar = Ubiq.Avatars.Avatar;

public class ItemAvatar : MonoBehaviour
{
    public GameObject[] items;

    private Avatar avatar;
    private RoomClient roomClient;

    [Serializable]
    private struct SerializableItem
    {
        public int index;
    }

    private string lastItem;

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

    public void SetItem(int index) {
        if (!avatar.IsLocal)
        {
            return;
        }
        if (index < -1)
        {
            Debug.Log("Unrecognized index");
            return;
        }

        var serializedItem = JsonUtility.ToJson(new SerializableItem
        {
            index = index
        });
        ProcessItem(serializedItem);
        roomClient.Me["item-avatar"] = serializedItem;
    }

    public void SetItem(GameObject item) {
        if (item == null) {
            SetItem(-1);
        } else {
            var index = Array.IndexOf(items, item);
            SetItem(index);
        }
    }

    private void RoomClient_OnPeerUpdated(IPeer peer)
    {
        if (peer != avatar.Peer)
        {
            // The peer who is being updated is not our peer, so we can safely
            // ignore this event.
            return;
        }
        ProcessItem(peer["item-avatar"]);
    }

    private void ProcessItem(string serializedItem)
    {
        if (String.IsNullOrEmpty(serializedItem) || serializedItem == lastItem) {
            return;
        }
        var index = JsonUtility.FromJson<SerializableItem>(serializedItem).index;
        if (index < -1 || index >= items.Length)
        {
            Debug.LogWarning("Unrecognized item received as property.");
            return;
        }
        // Deactivate other item and activate current
        var transform = FindItemTransform(avatar.transform);
        if (transform) {
            foreach (Transform child in transform) {
                Destroy(child.gameObject);
            }
            // if index is -1, just clear out
            if (index != -1) {
                Debug.Log("Adding item");
                Debug.Log($"Item index {index}");
                var itemPrefab = items[index];
                GameObject item = Instantiate(itemPrefab,
                    transform);
                item.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                item.transform.localScale = Vector3.one;
            }
        } else {
            Debug.LogWarning("Could not find transform");
        }
        lastItem = serializedItem;
    }
    private Transform FindItemTransform(Transform avatarRoot)
    {
        Transform body = avatarRoot.Find("Body");
        if (body != null)
        {
            Transform torso = body.Find("Floating_Torso_A");
            if (torso != null)
            {
                Transform bag = torso.Find("Bag");
                if (bag != null)
                    return bag;
                else
                    Debug.Log("Could not find bag");
            } else {
                Debug.Log("Could not find torso");
            }
        } else {
            Debug.Log("Could not find body");
        }
        
        Debug.LogWarning("Could not find Bag transform in avatar hierarchy");
        return null;
    }
}
