using UnityEngine;

namespace Iso3DBillboard
{
    /// <summary>
    /// Y 轴 Billboard：让物体只绕 Y 轴旋转，始终面向相机的水平方向。
    ///
    /// 为什么不能做全向Billboard（LookAt 相机）：
    /// 俯视角下全向 Billboard 会把立绘"躺平"，看起来像贴在地上的一张纸。
    /// 只锁 Y 轴则能保证立绘永远垂直站立 —— 这正是 2D 美术习惯的画法。
    ///
    /// 这是 2.5D 的核心组件：美术只画正面图，世界依然是真 3D。
    /// </summary>
    public class YBillboard : MonoBehaviour
    {
        /// <summary>朝向的平滑速度，0 表示瞬间转向。</summary>
        public float rotateSpeed = 12f;

        Camera _cam;
        Transform _camTransform;
        Quaternion _target;
        bool _targetValid;

        void Start()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                // 场景里可能还没标记 MainCamera，退而求其次找第一个
                foreach (var c in FindObjectsOfType<Camera>())
                {
                    _cam = c; break;
                }
            }
            _camTransform = _cam != null ? _cam.transform : null;
        }

        void LateUpdate()
        {
            if (_camTransform == null) return;

            // 只取相机在水平面上的方向，忽略俯仰角 —— 这是立绘不躺倒的关键
            Vector3 toCam = _camTransform.position - transform.position;
            toCam.y = 0f;                       // 关键：抹掉垂直分量
            if (toCam.sqrMagnitude < 0.0001f) return;
            toCam.Normalize();

            _target = Quaternion.LookRotation(toCam, Vector3.up);

            if (rotateSpeed <= 0f)
            {
                transform.rotation = _target;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, _target, rotateSpeed * Time.deltaTime);
            }
        }

        /// <summary>立即对齐（生成场景时用，避免第一帧歪着）。</summary>
        public void SnapToCamera()
        {
            if (_camTransform == null)
            {
                _cam = Camera.main;
                _camTransform = _cam != null ? _cam.transform : null;
                if (_camTransform == null) return;
            }
            Vector3 toCam = _camTransform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
    }
}
