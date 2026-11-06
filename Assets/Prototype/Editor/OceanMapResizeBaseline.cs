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
        public float appliedScale = 1f;
        public Matrix4x4 mapMatrix;
        public Entry[] entries = Array.Empty<Entry>();

        [Serializable]
        public sealed class Path
        {
            public Vector2[] points;
        }

        [Serializable]
        public sealed class Polygon
        {
            public string id;
            public Vector2 offset;
            public Path[] paths;
        }

        [Serializable]
        public sealed class Box
        {
            public string id;
            public Vector2 offset;
            public Vector2 size;
        }

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
            public Matrix4x4 worldMatrix;
            public bool movePosition;
            public Shape shape;
            public Polygon[] polygons = Array.Empty<Polygon>();
            public Box[] boxes = Array.Empty<Box>();
            public string rendererId;
            public Sprite originalSprite;
            public SpriteDrawMode rendererDrawMode;
            public Vector2 rendererSize;
            public Vector2 visualSize;
        }
    }
}
