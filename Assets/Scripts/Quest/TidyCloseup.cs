using System.Collections.Generic;
using RoomDemo.Contracts;
using RoomDemo.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RoomDemo.Quest
{
    /// <summary>鼠标整理特写：逐件归位，全部完成后结算。</summary>
    public class TidyCloseup : CloseupBase
    {
        [Tooltip("收集此容器下的 TidyItem；留空使用 Closeup Root。")]
        public Transform itemsRoot;
        readonly Dictionary<TidySpot, TidyItem.State[]> _progress = new Dictionary<TidySpot, TidyItem.State[]>();
        TidyItem[] _items;
        TidyItem _hovered, _held;
        TidySpot _activeSpot;
        InputManager _input;
        Vector3 _grabOffset, _pickupPosition;
        Quaternion _pickupRotation;
        bool _completed;
        public bool IsInTidyCloseup => InCloseup;
        protected override bool UseMouseCancel => false;

        public void Enter(TidySpot spot)
        {
            if (InCloseup || spot == null || !spot.canInteract) return;
            EnsureItems();
            if (_items.Length == 0)
            {
                Debug.LogWarning("[收拾] 请在杂物上添加 TidyItem 并绑定 Target Area。", this);
                return;
            }
            _activeSpot = spot;
            _completed = false;
            _progress.TryGetValue(spot, out var states);
            for (int i = 0; i < _items.Length; i++)
            {
                if (states != null && states.Length == _items.Length) _items[i].Restore(states[i]);
                else _items[i].ResetItem();
            }
            EnterCloseup();
        }

        void EnsureItems()
        {
            if (_items != null) return;
            var root = itemsRoot != null ? itemsRoot : (closeupRoot != null ? closeupRoot : transform);
            _items = root.GetComponentsInChildren<TidyItem>(true);
            foreach (var item in _items) item.Initialize();
        }

        protected override void FrameCamera()
        {
            base.FrameCamera();
            if (mainCamera == null || closeupRoot == null) return;
            var position = mainCamera.transform.position;
            position.z = closeupRoot.position.z + closeupCameraOffset.z;
            mainCamera.transform.position = position;
        }

        protected override void OnEntered() => SubscribePointer();

        void SubscribePointer()
        {
            if (_input != null || InputManager.Instance == null) return;
            _input = InputManager.Instance;
            _input.PrimaryPointerPressed += OnPrimary;
            _input.SecondaryPointerPressed += OnSecondary;
        }

        bool CanPlay => InCloseup && !_completed && _activeSpot != null && _activeSpot.canInteract
            && _input != null && !_input.IsBlocked
            && (Services.Flow == null || Services.Flow.State == GameFlowState.Exploring);
        bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        protected override void OnCloseupUpdate()
        {
            if (Services.Flow != null && Services.Flow.State != GameFlowState.Exploring)
            {
                ExitCloseup();
                return;
            }
            SubscribePointer();
            if (!CanPlay) { SetHovered(null); return; }
            if (_held != null)
            {
                MoveHeld();
                _held.StepRotation(Time.deltaTime);
                Physics2D.SyncTransforms();
                _held.SetHeldFeedback(_held.CanPlace());
            }
            else SetHovered(PointerOverUI ? null : PickItem());
        }

        bool PointerWorld(float z, out Vector3 position)
        {
            position = default;
            if (mainCamera == null || _input == null) return false;
            var ray = mainCamera.ScreenPointToRay(_input.PointerScreenPos);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
            if (!plane.Raycast(ray, out float distance)) return false;
            position = ray.GetPoint(distance);
            return true;
        }

        TidyItem PickItem()
        {
            Physics2D.SyncTransforms();
            TidyItem picked = null;
            foreach (var item in _items)
                if (!item.IsPlaced && item.gameObject.activeInHierarchy
                    && PointerWorld(item.transform.position.z, out var point) && item.Contains(point)
                    && (picked == null || item.DrawsAbove(picked))) picked = item;
            return picked;
        }

        void SetHovered(TidyItem next)
        {
            if (_hovered == next) return;
            if (_hovered != null) _hovered.SetHovered(false);
            _hovered = next;
            if (_hovered != null) _hovered.SetHovered(true);
        }

        void MoveHeld()
        {
            if (_held != null && PointerWorld(_held.transform.position.z, out var point))
                _held.transform.position = point + _grabOffset;
        }

        void OnPrimary()
        {
            if (!CanPlay || PointerOverUI) return;
            if (_held == null)
            {
                var picked = PickItem();
                if (picked == null || !PointerWorld(picked.transform.position.z, out var point)) return;
                SetHovered(null);
                _held = picked;
                _pickupPosition = picked.transform.localPosition;
                _pickupRotation = picked.transform.localRotation;
                _grabOffset = picked.transform.position - point;
                picked.SetHeld(true);
                return;
            }
            MoveHeld();
            Physics2D.SyncTransforms();
            if (!_held.CanPlace()) return;
            _held.Place();
            _held = null;
            foreach (var item in _items) if (!item.IsPlaced) return;
            _completed = true;
            SaveProgress();
            _activeSpot.ExecuteTidy(true);
        }

        void OnSecondary()
        {
            if (CanPlay && !PointerOverUI && _held != null) _held.BeginStraighten();
        }

        void SaveProgress()
        {
            if (_activeSpot == null || _items == null) return;
            var states = new TidyItem.State[_items.Length];
            for (int i = 0; i < _items.Length; i++) states[i] = _items[i].Capture();
            _progress[_activeSpot] = states;
        }

        void ReleasePointer()
        {
            SetHovered(null);
            if (_held != null)
            {
                _held.SetHeld(false);
                _held.transform.localPosition = _pickupPosition;
                _held.transform.localRotation = _pickupRotation;
                _held = null;
            }
            if (_input != null)
            {
                _input.PrimaryPointerPressed -= OnPrimary;
                _input.SecondaryPointerPressed -= OnSecondary;
                _input = null;
            }
        }

        protected override void OnExited()
        {
            ReleasePointer();
            SaveProgress();
            if (_activeSpot != null) _activeSpot.CancelArm();
            _activeSpot = null;
        }

        public void NotifyMessUp(TidySpot spot)
        {
            if (spot == null) return;
            _progress.Remove(spot);
            if (!InCloseup || _activeSpot != spot) return;
            ReleasePointer();
            foreach (var item in _items) item.ResetItem();
            _completed = false;
            SubscribePointer();
        }

        public override void ResetForNewRun()
        {
            if (InCloseup) ExitCloseup();
            ReleasePointer();
            _progress.Clear();
            _activeSpot = null;
            _completed = false;
            EnsureItems();
            foreach (var item in _items) item.ResetItem();
        }

        protected override void OnDestroy()
        {
            ReleasePointer();
            base.OnDestroy();
        }
    }
}

