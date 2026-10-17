using RoomDemo.Contracts;
using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 房间特写基类（画板 / 收拾 / 接水共用）：进特写隐藏客厅、相机聚焦到
    /// closeupRoot，退出还原。把两处最容易踩的公共陷阱收在这里：
    ///
    /// 1) <b>输入只在进入后才订阅</b>：否则触发"进入"的那次 E 会被本管理器再处理一遍
    ///    （进来就直接执行，两段式被吞掉一半）。
    /// 2) <b>全局 CloseupActive 总闸门</b>：进特写后玩家坐标没动，房间里的 TidySpot /
    ///    画笔 / 道具本来还会响应同一个 E 事件，导致双重结算。
    ///
    /// 子类只需重写 <see cref="OnEntered"/> / <see cref="OnExited"/> /
    /// <see cref="HandleInteract"/> / <see cref="OnCloseupUpdate"/> 填自己的玩法。
    /// </summary>
    public abstract class CloseupBase : MonoBehaviour, IResettable
    {
        [Header("引用")]
        [Tooltip("客厅根节点，进特写时整体隐藏")]
        public Transform roomRoot;
        public Camera mainCamera;
        [Tooltip("特写容器（含背景等），由装配菜单生成，默认 inactive")]
        public Transform closeupRoot;

        [Header("特写相机")]
        public float closeupOrthoSize = 5f;
        public Vector3 closeupCameraOffset = new Vector3(0f, 0f, -10f);

        protected bool InCloseup { get; private set; }

        Vector3 _roomCamPos;
        float _roomCamSize;
        bool _inputSubscribed;

        protected virtual void OnDestroy()
        {
            UnsubscribeInput();
        }

        /// <summary>进入特写。</summary>
        public void EnterCloseup()
        {
            if (InCloseup) return;
            InCloseup = true;

            SaveCamera();
            SetRoomVisible(false);
            RoomInteractable.CloseupActive = true;

            if (closeupRoot != null) closeupRoot.gameObject.SetActive(true);

            FrameCamera();
            OnEntered();

            // 关键：进入后才订阅输入，避免触发"进入"的那次 E 被自己重复处理
            EnsureInputSubscribed();
        }

        /// <summary>退出特写（Esc 或游戏逻辑主动调用）。</summary>
        public void ExitCloseup()
        {
            if (!InCloseup) return;
            InCloseup = false;
            RoomInteractable.CloseupActive = false;

            SetRoomVisible(true);
            if (closeupRoot != null) closeupRoot.gameObject.SetActive(false);
            RestoreCamera();
            OnExited();
            UnsubscribeInput();
        }

        /// <summary>进入特写后（视觉初始化）。</summary>
        protected virtual void OnEntered() { }

        /// <summary>退出特写前（状态清理）。</summary>
        protected virtual void OnExited() { }

        /// <summary>特写中收到 E。</summary>
        protected virtual void HandleInteract() { }

        /// <summary>特写中的每帧更新（如按住接水）。</summary>
        protected virtual void OnCloseupUpdate() { }

        void Update()
        {
            if (InCloseup) OnCloseupUpdate();
        }

        void SaveCamera()
        {
            if (mainCamera == null) return;
            _roomCamPos = mainCamera.transform.position;
            _roomCamSize = mainCamera.orthographicSize;
        }

        void FrameCamera()
        {
            if (mainCamera == null || closeupRoot == null) return;
            mainCamera.orthographicSize = closeupOrthoSize;
            mainCamera.transform.position = new Vector3(
                closeupRoot.position.x + closeupCameraOffset.x,
                closeupRoot.position.y + closeupCameraOffset.y,
                closeupCameraOffset.z);
        }

        void RestoreCamera()
        {
            if (mainCamera == null) return;
            mainCamera.orthographicSize = _roomCamSize;
            mainCamera.transform.position = _roomCamPos;
        }

        /// <summary>
        /// 隐藏/恢复客厅。房间图收在 roomRoot 下整体开关；留空时回退按名字找
        /// RoomWorld（兼容旧装配），再找不到就静默跳过。
        /// </summary>
        void SetRoomVisible(bool visible)
        {
            Transform room = roomRoot;
            if (room == null)
            {
                var go = GameObject.Find("RoomWorld");
                if (go == null) return;
                room = go.transform;
            }
            room.gameObject.SetActive(visible);
        }

        void HandleInteractInput()
        {
            if (InCloseup) HandleInteract();
        }

        void HandleCancelInput()
        {
            if (InCloseup) ExitCloseup();
        }

        void EnsureInputSubscribed()
        {
            if (_inputSubscribed) return;
            var im = InputManager.Instance;
            if (im == null) return;
            im.Interact -= HandleInteractInput; im.Interact += HandleInteractInput;
            im.CancelPressed -= HandleCancelInput; im.CancelPressed += HandleCancelInput;
            _inputSubscribed = true;
        }

        void UnsubscribeInput()
        {
            var im = InputManager.Instance;
            if (im != null)
            {
                im.Interact -= HandleInteractInput;
                im.CancelPressed -= HandleCancelInput;
            }
            _inputSubscribed = false;
        }

        /// <summary>R 重开：不论是否正在特写，都强制复位（GameManager 统一遍历 IResettable）。</summary>
        public virtual void ResetForNewRun()
        {
            InCloseup = false;
            RoomInteractable.CloseupActive = false;
            if (closeupRoot != null) closeupRoot.gameObject.SetActive(false);
            RestoreCamera();
            UnsubscribeInput();
        }
    }
}
