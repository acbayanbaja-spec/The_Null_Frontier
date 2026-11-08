using System.Collections.Generic;
using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// Generic component pool for projectiles, hit effects, tracers, etc.
    /// Pre-warms on Start to avoid runtime allocation spikes.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [SerializeField] private Component prefab;
        [SerializeField] private int prewarmCount = 16;

        private readonly Queue<Component> _available = new();

        private void Start()
        {
            for (int i = 0; i < prewarmCount; i++)
            {
                var instance = Instantiate(prefab, transform);
                instance.gameObject.SetActive(false);
                _available.Enqueue(instance);
            }
        }

        public T Get<T>() where T : Component
        {
            Component item = _available.Count > 0 ? _available.Dequeue() : Instantiate(prefab, transform);
            item.gameObject.SetActive(true);
            return (T)item;
        }

        public void Release(Component item)
        {
            item.gameObject.SetActive(false);
            item.transform.SetParent(transform, false);
            _available.Enqueue(item);
        }
    }
}
