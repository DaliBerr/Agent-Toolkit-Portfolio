using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vocalith.RuntimeAutomation.UGUI
{
    public static class RuntimeAutomationUguiSnapshot
    {
        public static JArray CaptureControls(int maximumCount = 500)
        {
            int limit = Mathf.Clamp(maximumCount, 1, 2000);
            Selectable[] selectables = UnityEngine.Object.FindObjectsByType<Selectable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
            var results = new JArray();
            foreach (Selectable selectable in selectables.Take(limit))
            {
                if (selectable == null) continue;
                results.Add(CaptureSelectable(selectable));
            }

            return results;
        }

        public static JArray CaptureTexts(int maximumCount = 1000)
        {
            int limit = Mathf.Clamp(maximumCount, 1, 4000);
            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
            var results = new JArray();
            foreach (TMP_Text text in texts.Take(limit))
            {
                if (text == null) continue;
                results.Add(CaptureText(text));
            }

            return results;
        }

        public static JObject CaptureSelectable(Selectable selectable)
        {
            RectTransform rect = selectable.transform as RectTransform;
            var json = new JObject
            {
                ["id"] = BuildPath(selectable.transform),
                ["name"] = selectable.name,
                ["type"] = selectable.GetType().Name,
                ["active"] = selectable.gameObject.activeInHierarchy,
                ["enabled"] = selectable.enabled,
                ["interactable"] = selectable.IsInteractable(),
                ["navigation_mode"] = selectable.navigation.mode.ToString(),
                ["rect"] = CaptureRect(rect),
            };
            TMP_Text label = selectable.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (label != null) json["text"] = label.text;
            if (selectable is Toggle toggle) json["is_on"] = toggle.isOn;
            if (selectable is Slider slider)
            {
                json["value"] = slider.value;
                json["minimum"] = slider.minValue;
                json["maximum"] = slider.maxValue;
            }
            if (selectable is TMP_InputField input) json["value"] = input.text;
            json["center_blocked"] = IsCenterBlocked(selectable, rect);
            return json;
        }

        public static JObject CaptureText(TMP_Text text)
        {
            RectTransform rect = text.rectTransform;
            Vector2 preferred = text.GetPreferredValues(text.text, rect.rect.width, 0f);
            return new JObject
            {
                ["id"] = BuildPath(text.transform),
                ["name"] = text.name,
                ["text"] = text.text,
                ["active"] = text.gameObject.activeInHierarchy,
                ["enabled"] = text.enabled,
                ["font"] = text.font?.name,
                ["font_size"] = text.fontSize,
                ["color"] = ColorJson(text.color),
                ["rect"] = CaptureRect(rect),
                ["preferred_width"] = preferred.x,
                ["preferred_height"] = preferred.y,
                ["overflow_mode"] = text.overflowMode.ToString(),
                ["is_overflowing"] = preferred.x > rect.rect.width + 0.5f || preferred.y > rect.rect.height + 0.5f,
            };
        }

        public static JObject CaptureRect(RectTransform rect)
        {
            if (rect == null) return new JObject();
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return new JObject
            {
                ["x"] = lowerLeft.x,
                ["y"] = lowerLeft.y,
                ["width"] = upperRight.x - lowerLeft.x,
                ["height"] = upperRight.y - lowerLeft.y,
                ["anchor_min"] = VectorJson(rect.anchorMin),
                ["anchor_max"] = VectorJson(rect.anchorMax),
                ["pivot"] = VectorJson(rect.pivot),
            };
        }

        private static bool IsCenterBlocked(Selectable selectable, RectTransform rect)
        {
            if (rect == null || EventSystem.current == null || !selectable.gameObject.activeInHierarchy) return false;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, centerWorld),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            if (hits.Count == 0) return true;
            Transform hit = hits[0].gameObject.transform;
            return hit != selectable.transform && !hit.IsChildOf(selectable.transform) && !selectable.transform.IsChildOf(hit);
        }

        private static string BuildPath(Transform transform)
        {
            var parts = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private static JObject VectorJson(Vector2 value)
        {
            return new JObject { ["x"] = value.x, ["y"] = value.y };
        }

        private static JObject ColorJson(Color value)
        {
            return new JObject
            {
                ["r"] = value.r,
                ["g"] = value.g,
                ["b"] = value.b,
                ["a"] = value.a,
            };
        }
    }
}
