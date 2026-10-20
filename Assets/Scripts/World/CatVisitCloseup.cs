using System.Collections;
using UnityEngine;
using RoomDemo.World;

namespace RoomDemo
{
    /// <summary>
    /// 画板特写中的"猫路过"随机事件：特写期间，猫会随机时间突然出现，
    /// 遮挡某块画板的一部分，停留一小会儿后离开，再等随机间隔后重现。
    ///
    /// 用法：挂在 PaintingBoards（画板组）上，并填好 cat 与 boards 引用。
    /// 通过订阅 DrawingBoardInteract 的 Entered/ExitedPaintingMode 事件驱动，
    /// 不直接持有画板核心域的具体类引用（符合 R1 事件解耦）。
    /// </summary>
    public class CatVisitCloseup : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("猫物体（含 SpriteRenderer）。")]
        public Transform cat;
        [Tooltip("三块画板（用于随机选遮挡位置）。")]
        public Transform[] boards;

        [Header("出现节奏")]
        [Tooltip("两次出现之间的最小间隔（秒）。")]
        public float minInterval = 6f;
        [Tooltip("两次出现之间的最大间隔（秒）。")]
        public float maxInterval = 14f;
        [Tooltip("每次停留的最小时长（秒）。")]
        public float minStay = 1.2f;
        [Tooltip("每次停留的最大时长（秒）。")]
        public float maxStay = 3.5f;

        [Header("表现")]
        [Tooltip("猫遮挡画板时使用的 sortingOrder（需高于画板）。")]
        public int overlaySortingOrder = 15200;
        [Tooltip("猫出现位置相对画板中心的随机横向偏移范围。")]
        public float randomXRange = 0.6f;
        [Tooltip("猫出现位置相对画板中心的纵向偏移（正值往上）。")]
        public float yOffset = 0.4f;

        bool _inCloseup;
        bool _catShowing;
        float _nextVisitTime;
        Transform _catOriginalParent;
        int _catOriginalOrder = 10;
        Vector3 _catOriginalLocalPos;
        SpriteRenderer _catSr;
        Coroutine _visitRoutine;

        void OnEnable()
        {
            // 幂等订阅画板特写的进出事件
            var db = FindObjectOfType<DrawingBoardInteract>();
            if (db != null)
            {
                db.EnteredPaintingMode -= OnEnter;
                db.EnteredPaintingMode += OnEnter;
                db.ExitedPaintingMode -= OnExit;
                db.ExitedPaintingMode += OnExit;
            }
        }

        void OnDisable()
        {
            var db = FindObjectOfType<DrawingBoardInteract>();
            if (db != null)
            {
                db.EnteredPaintingMode -= OnEnter;
                db.ExitedPaintingMode -= OnExit;
            }
        }

        void OnEnter()
        {
            if (cat == null) return;
            _inCloseup = true;

            // 记下猫的原始状态，把猫挪到场景根，避免随 BedRoom 一起被隐藏
            _catOriginalParent = cat.parent;
            _catOriginalLocalPos = cat.localPosition;
            _catSr = cat.GetComponent<SpriteRenderer>();
            if (_catSr != null) _catOriginalOrder = _catSr.sortingOrder;

            // 先隐藏再挪，避免挪动时闪现一帧
            cat.gameObject.SetActive(false);
            cat.SetParent(null, true);

            // 特写期间冻结猫自己的游走/打乱逻辑，由本组件独占控制位置
            var controller = cat.GetComponent<CatController>();
            if (controller != null) controller.enabled = false;

            _catShowing = false;
            _nextVisitTime = Time.time + Random.Range(minInterval, maxInterval);
        }

        void OnExit()
        {
            _inCloseup = false;
            _catShowing = false;
            if (_visitRoutine != null) { StopCoroutine(_visitRoutine); _visitRoutine = null; }

            if (cat != null)
            {
                // 还原猫：回到原父级、原位置、原排序、恢复显示
                cat.gameObject.SetActive(true);
                if (_catOriginalParent != null)
                {
                    cat.SetParent(_catOriginalParent, false);
                    cat.localPosition = _catOriginalLocalPos;
                }
                if (_catSr != null) _catSr.sortingOrder = _catOriginalOrder;

                // 恢复猫自己的游走逻辑
                var controller = cat.GetComponent<CatController>();
                if (controller != null) controller.enabled = true;
            }
        }

        void Update()
        {
            if (!_inCloseup || cat == null) return;
            if (_catShowing) return; // 已在显示中，等 Stay 协程结束

            if (Time.time >= _nextVisitTime)
            {
                StartVisit();
            }
        }

        void StartVisit()
        {
            _catShowing = true;
            var target = PickBoard();
            if (target != null)
            {
                cat.position = target.position + new Vector3(
                    Random.Range(-randomXRange, randomXRange), yOffset, 0f);
            }
            if (_catSr != null) _catSr.sortingOrder = overlaySortingOrder;
            cat.gameObject.SetActive(true);

            _visitRoutine = StartCoroutine(StayThenLeave());
        }

        Transform PickBoard()
        {
            if (boards == null || boards.Length == 0) return cat != null ? cat : null;
            return boards[Random.Range(0, boards.Length)];
        }

        IEnumerator StayThenLeave()
        {
            float stay = Random.Range(minStay, maxStay);
            yield return new WaitForSeconds(stay);

            // 离开：隐藏猫，安排下一次出现
            cat.gameObject.SetActive(false);
            _catShowing = false;
            _nextVisitTime = Time.time + Random.Range(minInterval, maxInterval);
            _visitRoutine = null;
        }
    }
}
