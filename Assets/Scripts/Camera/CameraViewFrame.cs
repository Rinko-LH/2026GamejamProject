using UnityEngine;

namespace RoomDemo
{
    /// <summary>在场景视图中绘制正交机位的视野边框。</summary>
    [DisallowMultipleComponent]
    public sealed class CameraViewFrame : MonoBehaviour
    {
        [Header("视野设置")]
        [Tooltip("视野的半高，单位为世界单位，对应正交相机的 Orthographic Size。完整高度为此值的两倍。")]
        [Min(0.001f)] public float orthographicSize = 10f;

        [Tooltip("视野的宽高比。未指定参考相机时使用，默认是 16:9。")]
        [Min(0.001f)] public float aspectRatio = 16f / 9f;

        [Tooltip("可选的参考相机。指定后仅使用其宽高比；视野半高仍使用上方配置，不会修改或移动相机。")]
        public Camera referenceCamera;

        [Tooltip("场景视图中视野边框的颜色。需要开启 Gizmos 才能看到。")]
        public Color frameColor = Color.green;

        void OnValidate()
        {
            orthographicSize = Mathf.Max(0.001f, orthographicSize);
            aspectRatio = Mathf.Max(0.001f, aspectRatio);
        }

        void OnDrawGizmos()
        {
            float height = Mathf.Max(0.001f, orthographicSize) * 2f;
            float aspect = referenceCamera != null ? referenceCamera.aspect : aspectRatio;
            float width = height * Mathf.Max(0.001f, aspect);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            try
            {
                // 使用单位缩放，避免父物体或自身缩放影响视野尺寸。
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.color = frameColor;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, height, 0f));
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }
    }
}
