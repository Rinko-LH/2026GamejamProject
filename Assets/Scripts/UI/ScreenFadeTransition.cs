using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace RoomDemo.UI { public sealed class ScreenFadeTransition : MonoBehaviour {
 static ScreenFadeTransition _instance; CanvasGroup _canvasGroup; bool _isFading;
 public static ScreenFadeTransition GetOrCreate() { if (_instance != null) return _instance; _instance = FindObjectOfType<ScreenFadeTransition>(); if (_instance != null) return _instance; return new GameObject("ScreenFadeTransition", typeof(RectTransform)).AddComponent<ScreenFadeTransition>(); }
 void Awake() { if (_instance != null && _instance != this) { Destroy(gameObject); return; } _instance = this; DontDestroyOnLoad(gameObject); BuildOverlay(); }
 void OnDestroy() { if (_instance == this) _instance = null; }
 void BuildOverlay() { var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue; gameObject.AddComponent<GraphicRaycaster>(); _canvasGroup = gameObject.AddComponent<CanvasGroup>(); _canvasGroup.alpha = 0f; _canvasGroup.interactable = false; _canvasGroup.blocksRaycasts = false; var overlay = new GameObject("BlackOverlay", typeof(RectTransform), typeof(Image)); overlay.transform.SetParent(transform, false); var rect = (RectTransform)overlay.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; var image = overlay.GetComponent<Image>(); image.color = Color.black; image.raycastTarget = true; }
 public bool Play(Action whileBlack, float fadeSeconds, float blackHoldSeconds) { if (_isFading || _canvasGroup == null) return false; StartCoroutine(FadeRoutine(whileBlack, fadeSeconds, blackHoldSeconds)); return true; }
 IEnumerator FadeRoutine(Action whileBlack, float fadeSeconds, float blackHoldSeconds) { _isFading = true; _canvasGroup.blocksRaycasts = true; yield return FadeTo(1f, fadeSeconds); try { whileBlack?.Invoke(); } catch (Exception e) { Debug.LogException(e, this); } if (blackHoldSeconds > 0f) yield return new WaitForSecondsRealtime(blackHoldSeconds); yield return FadeTo(0f, fadeSeconds); _canvasGroup.blocksRaycasts = false; _isFading = false; }
 IEnumerator FadeTo(float target, float seconds) { float start = _canvasGroup.alpha; if (seconds <= 0f) { _canvasGroup.alpha = target; yield break; } float elapsed = 0f; while (elapsed < seconds) { elapsed += Time.unscaledDeltaTime; _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / seconds); yield return null; } _canvasGroup.alpha = target; }
} }