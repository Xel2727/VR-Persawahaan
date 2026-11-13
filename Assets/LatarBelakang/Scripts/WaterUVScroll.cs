using System.Collections.Generic;
using UnityEngine;

namespace LatarBelakang
{
    /// <summary>
    /// Menggulir UV material air/foam (animasi shader Blender tidak ikut ke Unity).
    /// Membuat salinan material saat runtime supaya asset .mat tidak ikut berubah.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterUVScroll : MonoBehaviour
    {
        public Vector2 scrollSpeed = new Vector2(0.015f, 0.06f);
        public string[] nameKeywords = { "Water", "Foam", "Air" };

        readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        readonly List<Material> _runtimeMats = new List<Material>();
        Vector2 _offset;

        void Start()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var m = shared[i];
                    if (m == null || !IsWater(m.name)) continue;

                    if (!_copies.TryGetValue(m, out var copy))
                    {
                        copy = new Material(m) { name = m.name + " (Runtime)" };
                        _copies[m] = copy;
                        _runtimeMats.Add(copy);
                    }
                    shared[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = shared;
            }
        }

        bool IsWater(string matName)
        {
            if (string.IsNullOrEmpty(matName)) return false;
            foreach (var k in nameKeywords)
                if (!string.IsNullOrEmpty(k) && matName.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        void Update()
        {
            _offset += scrollSpeed * Time.deltaTime;
            _offset.x = Mathf.Repeat(_offset.x, 1f);
            _offset.y = Mathf.Repeat(_offset.y, 1f);
            for (int i = 0; i < _runtimeMats.Count; i++)
                _runtimeMats[i].mainTextureOffset = _offset;
        }
    }
}
