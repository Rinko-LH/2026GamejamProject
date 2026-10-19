using System.Collections.Generic;
using RoomDemo.UI;
using UnityEngine;
namespace RoomDemo.Interaction { [RequireComponent(typeof(Collider2D))] public sealed class TeleportTrigger : MonoBehaviour {
 [Header("传送设置")][SerializeField] Transform destination; [SerializeField] string actorTag = "Player"; [SerializeField] bool ignoreActorTriggerColliders = true;
 [Header("黑屏转场")][Min(0f)][SerializeField] float fadeSeconds = 0.35f; [Min(0f)][SerializeField] float blackHoldSeconds = 0.12f;
 readonly Dictionary<int, HashSet<int>> _overlapsByActor = new Dictionary<int, HashSet<int>>();
 void Reset() { var trigger = GetComponent<Collider2D>(); if (trigger != null) trigger.isTrigger = true; }
 void OnTriggerEnter2D(Collider2D other) { if (other == null || (ignoreActorTriggerColliders && other.isTrigger)) return; Transform actor = FindActor(other.transform); if (actor == null) return; if (destination == null) { Debug.LogWarning($"[传送] {name} 尚未指定目标位置。", this); return; } int id = actor.GetInstanceID(); if (!_overlapsByActor.TryGetValue(id, out var colliders)) { colliders = new HashSet<int>(); _overlapsByActor.Add(id, colliders); } if (!colliders.Add(other.GetInstanceID()) || colliders.Count > 1) return; ScreenFadeTransition.GetOrCreate().Play(() => Teleport(actor), fadeSeconds, blackHoldSeconds); }
 void OnTriggerExit2D(Collider2D other) { if (other == null) return; Transform actor = FindActor(other.transform); if (actor == null) return; int id = actor.GetInstanceID(); if (!_overlapsByActor.TryGetValue(id, out var colliders)) return; colliders.Remove(other.GetInstanceID()); if (colliders.Count == 0) _overlapsByActor.Remove(id); }
 Transform FindActor(Transform candidate) { if (string.IsNullOrEmpty(actorTag)) return null; for (var current = candidate; current != null; current = current.parent) if (current.CompareTag(actorTag)) return current; return null; }
 void Teleport(Transform actor) { if (actor == null || destination == null) return; Vector3 pos = destination.position; var body = actor.GetComponent<Rigidbody2D>(); if (body != null) { body.velocity = Vector2.zero; body.position = new Vector2(pos.x, pos.y); } actor.position = pos; Camera sceneCamera = Camera.main; Transform sourceRoom = transform.parent; Transform targetRoom = destination.parent; if (sceneCamera != null && sourceRoom != null && targetRoom != null) { Vector3 roomOffset = targetRoom.position - sourceRoom.position; roomOffset.z = 0f; // 房间切换只平移 XY，保留相机的渲染深度。
 sceneCamera.transform.position += roomOffset; } }
} }