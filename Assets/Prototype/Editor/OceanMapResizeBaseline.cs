using System;
using UnityEngine;

namespace DivingPrototype
{
    // Temporary editor session data, never written as an asset or scene component.
    public sealed class OceanMapResizeBaseline : ScriptableObject
    {
        public string sceneGuid;
        public ulong sceneHandle;
        public string mapId;
        public string submarineId;
        public Vector3 anchor;
        public Vector2 dimensions;
        public Vector2 spawnMin, spawnMax;
        public float appliedScale = 1f;
        public Matrix4x4 mapMatrix;
        public Entry[] entries = Array.Empty<Entry>();

        public enum Shape { Object, Polygon, Boundary, Seabed, GridX, GridY }

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string parentId;
            public string name;
            public string componentIds;
            public Vector3 position;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public bool movePosition;
            public Shape shape;
        }
    }
}
