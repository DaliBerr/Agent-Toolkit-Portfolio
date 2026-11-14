using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vocalith.RuntimeAutomation
{
    [DisallowMultipleComponent]
    public sealed class RuntimeAutomationIdentity : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private string semanticKind = "object";
        [SerializeField] private List<string> tags = new();

        public string StableId => stableId;
        public string SemanticKind => string.IsNullOrWhiteSpace(semanticKind) ? "object" : semanticKind;
        public IReadOnlyList<string> Tags => tags;

        public bool TryGetStableId(out string id)
        {
            id = stableId?.Trim();
            return !string.IsNullOrWhiteSpace(id);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stableId = stableId?.Trim();
            semanticKind = semanticKind?.Trim();
            for (int index = tags.Count - 1; index >= 0; index--)
            {
                string value = tags[index]?.Trim();
                if (string.IsNullOrWhiteSpace(value)) tags.RemoveAt(index);
                else tags[index] = value;
            }
        }
#endif
    }
}
