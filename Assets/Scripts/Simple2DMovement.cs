using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 主角移动（占位）。输入统一走 InputManager（策划案 1.7），
    /// 本脚本不直接读 Input；对话/UI 期间由上下文栈自动锁住。
    /// </summary>
    public class Simple2DMovement : MonoBehaviour
    {
        public float moveSpeed = 5f;

        void Update()
        {
            Vector2 move = InputManager.Instance != null ? InputManager.Instance.Move : Vector2.zero;
            Vector3 movement = new Vector3(move.x, move.y, 0f).normalized;
            transform.position += movement * moveSpeed * Time.deltaTime;
        }
    }
}
