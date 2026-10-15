using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 可互动的场景物品基类。色块占位期只需要"走近 + 按键 → 触发"。
    ///
    /// 注意：Unity 要求 MonoBehaviour 所在文件名与类名一致，
    /// 所以这个基类单独放在 RoomInteractable.cs 里
    ///（原来三个类挤在 RoomInteractables.cs，导致 MonoScript 资产
    /// 映射不到具体类，AddComponent 出来的组件序列化后会变成丢失脚本）。
    /// </summary>
    public abstract class RoomInteractable : MonoBehaviour
    {
        [Tooltip("交互距离（只看水平方向，主角走到物品所在的列附近即可）")]
        public float interactRange = 1.4f;

        public RoomPlayer player;

        /// <summary>是否还能交互。被拿走的物品要能主动失效。</summary>
        public bool canInteract = true;

        /// <summary>
        /// 主角是否在交互范围内。
        ///
        /// 关键：只比较<b>水平 X 距离</b>，不比较 Y。
        /// 因为这个 3/4 视角里，桌面上的家具 Y 值在 2.7 左右，
        /// 而主角站在地面上 Y 最多只能走到 1.56 —— 这是固定存在的纵向差，
        /// 用二维距离判定的话无论怎么走近都够不到，E 永远没反应。
        /// </summary>
        public bool PlayerInRange
        {
            get
            {
                if (!canInteract || player == null) return false;
                return HorizontalDistance(player.transform.position, transform.position) < interactRange;
            }
        }

        /// <summary>只算 X 方向的距离（该视角下 X 就是房间左右）。</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x);
        }

        protected virtual void Update()
        {
            if (PlayerInRange && Input.GetKeyDown(KeyCode.E))
                Interact();
        }

        /// <summary>执行互动。</summary>
        public abstract void Interact();
    }
}
