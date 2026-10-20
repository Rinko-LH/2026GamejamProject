using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo
{
    /// <summary>与 Canvas 一起缩放的白色圆角倒三角，无贴图依赖。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RoundedDialogueTriangle : MaskableGraphic
    {
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var corners = new[]
            {
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.center.x, rect.yMin)
            };
            vh.AddVert(rect.center, color, Vector2.zero);
            const int segments = 8;
            for (int i = 0; i < 3; i++)
            {
                var corner = corners[i];
                var start = Vector2.Lerp(corner, corners[(i + 2) % 3], 0.20f);
                var end = Vector2.Lerp(corner, corners[(i + 1) % 3], 0.20f);
                for (int j = 0; j <= segments; j++)
                {
                    float t = j / (float)segments;
                    var point = (1f - t) * (1f - t) * start + 2f * (1f - t) * t * corner + t * t * end;
                    vh.AddVert(point, color, Vector2.zero);
                }
            }
            int count = 3 * (segments + 1);
            for (int i = 0; i < count; i++) vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
        }
    }
}
