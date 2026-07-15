using UnityEngine;

namespace Botte.UI
{
    /// <summary>
    /// Locks a <see cref="RectTransform"/> to the exact layout it has in the editor (the state
    /// it is in right before entering Play mode). The authored anchored position, size, anchors,
    /// pivot, rotation, scale and local position are captured once and then continuously
    /// re-applied, so no runtime code (or layout system) can move, resize or otherwise alter the
    /// element's transform.
    ///
    /// This is attached to the card description panels: their transform must never change at
    /// runtime, while their child labels (name / cost / effect) remain freely editable.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockRectTransform : MonoBehaviour
    {
        private RectTransform _rt;
        private bool _captured;

        private Vector2 _anchoredPosition;
        private Vector2 _sizeDelta;
        private Vector2 _anchorMin;
        private Vector2 _anchorMax;
        private Vector2 _pivot;
        private Vector3 _localScale;
        private Quaternion _localRotation;
        private Vector3 _localPosition;

        private void Awake() => Capture();

        private void OnEnable() => Capture();

        // Snapshots the authored transform once. Runs on Awake for objects active at startup, or
        // on the first OnEnable for panels that start hidden (activation never alters the transform,
        // so the values captured are always the editor-authored ones).
        private void Capture()
        {
            if (_captured) return;
            _rt = transform as RectTransform;
            if (_rt == null) return;

            _anchoredPosition = _rt.anchoredPosition;
            _sizeDelta = _rt.sizeDelta;
            _anchorMin = _rt.anchorMin;
            _anchorMax = _rt.anchorMax;
            _pivot = _rt.pivot;
            _localScale = _rt.localScale;
            _localRotation = _rt.localRotation;
            _localPosition = _rt.localPosition;
            _captured = true;
        }

        // Re-applies the snapshot after all other Update logic each frame, reverting any change
        // that another script or a layout component may have made to the transform.
        private void LateUpdate()
        {
            if (!_captured || _rt == null) return;

            if (_rt.anchorMin != _anchorMin) _rt.anchorMin = _anchorMin;
            if (_rt.anchorMax != _anchorMax) _rt.anchorMax = _anchorMax;
            if (_rt.pivot != _pivot) _rt.pivot = _pivot;
            if (_rt.sizeDelta != _sizeDelta) _rt.sizeDelta = _sizeDelta;
            if (_rt.anchoredPosition != _anchoredPosition) _rt.anchoredPosition = _anchoredPosition;
            if (_rt.localScale != _localScale) _rt.localScale = _localScale;
            if (_rt.localRotation != _localRotation) _rt.localRotation = _localRotation;
            if (_rt.localPosition != _localPosition) _rt.localPosition = _localPosition;
        }
    }
}
