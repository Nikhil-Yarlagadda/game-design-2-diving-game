using UnityEngine;
using UnityEngine.UI;

namespace DivingPrototype
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadarRings : Graphic
    {
        public static float Radius(Vector2 size, int band) => Mathf.Min(size.x, size.y) * (.10f + .14f * band);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 96;
            for (int band = 0; band < 3; band++)
            {
                float radius = Radius(rectTransform.rect.size, band);
                for (int segment = 0; segment < segments; segment++)
                {
                    float a = segment * Mathf.PI * 2f / segments;
                    float b = (segment + 1) * Mathf.PI * 2f / segments;
                    Vector2 start = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 end = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    int vertex = mesh.currentVertCount;
                    mesh.AddVert(start * (radius - .6f), color, Vector2.zero);
                    mesh.AddVert(start * (radius + .6f), color, Vector2.zero);
                    mesh.AddVert(end * (radius + .6f), color, Vector2.zero);
                    mesh.AddVert(end * (radius - .6f), color, Vector2.zero);
                    mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
                    mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
                }
            }
        }
    }
}
