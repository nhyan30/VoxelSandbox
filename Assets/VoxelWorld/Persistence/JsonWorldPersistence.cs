using System;
using UnityEngine;
using VoxelWorld.Core.Persistence;

namespace VoxelWorld.Persistence
{
    /// <summary>
    /// JsonUtility-backed implementation of <see cref="IWorldPersistence"/>. The payload
    /// is intentionally sparse: the world is regenerated from the stored seed and only
    /// player modifications are replayed on top, so saves stay small even for big worlds.
    /// </summary>
    public sealed class JsonWorldPersistence : IWorldPersistence
    {
        private readonly ISaveStorage _storage;

        public JsonWorldPersistence(ISaveStorage storage)
        {
            _storage = storage;
        }

        public bool HasSave => _storage.Exists;

        public void Save(WorldSaveData data)
        {
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            _storage.Write(JsonUtility.ToJson(data, prettyPrint: false));
        }

        public bool TryLoad(out WorldSaveData data)
        {
            data = null;
            if (!_storage.TryRead(out var json))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<WorldSaveData>(json);
                if (data == null || data.version > 1)
                {
                    Debug.LogWarning("[VoxelSandbox] Save file was written by an incompatible version.");
                    data = null;
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoxelSandbox] Failed to parse save file: {e.Message}");
                data = null;
                return false;
            }
        }

        public void Delete()
        {
            _storage.Delete();
        }
    }
}
