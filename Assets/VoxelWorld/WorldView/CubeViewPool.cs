using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelWorld.World
{
    /// <summary>
    /// Object pool for <see cref="CubeView"/> instances. Prewarms a batch up front so
    /// world streaming doesn't allocate while the player walks; grows on demand and
    /// destroys returns above the configured cap.
    /// </summary>
    public sealed class CubeViewPool
    {
        private readonly Stack<CubeView> _free = new Stack<CubeView>();
        private readonly Func<CubeView> _factory;
        private readonly int _maxPooled;

        public int PooledCount => _free.Count;

        public CubeViewPool(Transform poolRoot, int blockLayer, bool castShadows, int prewarm, int maxPooled)
        {
            _factory = () => CubeView.Create(poolRoot, blockLayer, castShadows);
            _maxPooled = maxPooled;
            for (var i = 0; i < prewarm; i++)
            {
                _free.Push(_factory());
            }
        }

        public CubeView Get()
        {
            return _free.Count > 0 ? _free.Pop() : _factory();
        }

        public void Return(CubeView view)
        {
            if (view == null)
            {
                return;
            }

            if (_free.Count >= _maxPooled)
            {
                UnityEngine.Object.Destroy(view.gameObject);
                return;
            }
            
            view.gameObject.SetActive(false);
            _free.Push(view);
        }
    }
}
