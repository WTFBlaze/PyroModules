using MelonLoader;
using System;
using System.Collections;
using System.Collections.Concurrent;
using UnhollowerBaseLib.Attributes;
using UnityEngine;

namespace JoinNotifier
{
    public class HudHandler : MonoBehaviour
    {
        public HudHandler(IntPtr id) : base(id) { }
        public ConcurrentQueue<HudQueueItem> queue = new ConcurrentQueue<HudQueueItem>();
        private bool _currentlyRunningAlert;
        
        public void Update()
        {
            if (_currentlyRunningAlert || RoomManager.field_Internal_Static_ApiWorld_0 == null || 
                RoomManager.field_Internal_Static_ApiWorldInstance_0 == null) return;
            queue.TryDequeue(out var item);
            if (item == null) return;   
            MelonCoroutines.Start(ProcessAlert(3.5f, item));
        }

        public void Clear()
        {
            while (!queue.IsEmpty) 
            {
                queue.TryDequeue(out var _);
            } 
        }

        [HideFromIl2Cpp]
        private IEnumerator ProcessAlert(float duration, HudQueueItem item)
        {
            _currentlyRunningAlert = true;
            Main._hudImg.sprite = item.type == HudType.Join ? Main._joinSprite : Main._leftSprite;
            Main._hudTxt.text = item.username;
            Main._hudImg.gameObject.SetActive(true);
            Main._hudTxt.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(duration);
            Main._hudImg.gameObject.SetActive(false);
            Main._hudTxt.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(1f);
            _currentlyRunningAlert = false;
        }
    }

    public class HudQueueItem
    {
        public HudType type;
        public string username;
    }

    public enum HudType
    {
        Join,
        Leave
    }
}
