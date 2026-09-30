using System;
using System.IO;
using UnityEngine;

namespace VoxelWorld.Persistence
{
    /// <summary>Raw JSON storage boundary (kept injectable so tests can use memory storage).</summary>
    public interface ISaveStorage
    {
        bool Exists { get; }

        bool TryRead(out string json);

        void Write(string json);

        void Delete();
    }

    /// <summary>Stores one save slot as a JSON file in Application.persistentDataPath.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _path;

        public FileSaveStorage(string fileName)
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public string FilePath => _path;
        public bool Exists => File.Exists(_path);

        public bool TryRead(out string json)
        {
            json = null;
            try
            {
                if (!File.Exists(_path))
                {
                    return false;
                }

                json = File.ReadAllText(_path);
                return !string.IsNullOrEmpty(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoxelSandbox] Failed reading save '{_path}': {e.Message}");
                return false;
            }
        }

        public void Write(string json)
        {
            File.WriteAllText(_path, json);
        }

        public void Delete()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
