using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LatarBelakang.EditorTools
{
    /// <summary>
    /// Import + penempatan aset tambahan (petani, kerbau, orangan, ayam).
    /// Menu: Tools > Latar Belakang > 7. Pasang Aset Tambahan
    /// </summary>
    public static class AsetTambahanSetup
    {
        const string ROOT = "Assets/LatarBelakang";
        const string MODELS = ROOT + "/Models/Characters";
        const string TEX = MODELS + "/Textures";
        const string MATS = MODELS + "/Materials";
        const string PREFABS = ROOT + "/Prefabs/Characters";

        [MenuItem("Tools/Latar Belakang/7. Pasang Aset Tambahan", false, 7)]
        public static void SetupAll()
        {
            ConfigureImporters();
            var mats = CreateMaterials();
            CreatePrefabs(mats);
            PlaceInstances();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[LatarBelakang] Aset tambahan selesai dipasang.");
        }

        // ---------------------------------------------------------------- import

        static void ConfigureImporters()
        {
            if (!Directory.Exists(MODELS)) return;
            foreach (var file in Directory.GetFiles(MODELS, "*.fbx"))
            {
                string path = file.Replace('\\', '/');
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                bool prop = name.StartsWith("ORS_");

                mi.globalScale = 1f;
                mi.animationType = prop ? ModelImporterAnimationType.None : ModelImporterAnimationType.Legacy;
                mi.importAnimation = !prop;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.importCameras = false;
                mi.importLights = false;
                mi.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- materials

        static Dictionary<string, Material> CreateMaterials()
        {
            EnsureFolder(MATS);
            var result = new Dictionary<string, Material>();
            result["KRB_Kerbau"] = MakeAtlas("M_Kerbau_Atlas_Unity", "T_Kerbau_Atlas_512");
            result["AYM_Ayam"] = MakeAtlas("M_Ayam_Atlas_Unity", "T_Ayam_Atlas_512");
            result["ORS_A"] = MakeAtlas("M_Orangan_Caping_Unity", "T_Orangan_Caping_Atlas_256");
            result["ORS_B"] = MakeAtlas("M_Orangan_Ikat_Unity", "T_Orangan_Ikat_Atlas_256");
            return result;
        }

        static Material MakeAtlas(string matName, string textureName)
        {
            string matPath = MATS + "/" + matName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = Shader.Find("Standard");
            mat.color = Color.white;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + "/" + textureName + ".png");
            if (tex != null) mat.mainTexture = tex;
            mat.SetFloat("_Glossiness", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- prefabs

        static void CreatePrefabs(Dictionary<string, Material> mats)
        {
            EnsureFolder(PREFABS);

            for (int i = 1; i <= 16; i++)
            {
                string id = "Pekerja_" + i.ToString("00");
                BuildPrefab(id, id, null, 2f, 1.0f, 6f, 2f, 5f, 1.0f, true);
            }

            BuildPrefab("KRB_Kerbau", "KRB_Kerbau", mats["KRB_Kerbau"], 2f, 0.45f, 8f, 3f, 7f, 0.8f, true);
            BuildPrefab("AYM_Ayam", "AYM_Ayam", mats["AYM_Ayam"], 2f, 0.5f, 3f, 1f, 4f, 0.8f, true);

            BuildOrangan(mats);
        }

        static GameObject BuildPrefab(string model, string prefabName, Material mat, float scale,
            float speed, float radius, float minPause, float maxPause, float maxStep, bool wander)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(MODELS + "/" + model + ".fbx");
            if (src == null)
            {
                Debug.LogWarning("[LatarBelakang] Model tidak ditemukan: " + model);
                return null;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = prefabName;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * scale;
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (mat != null) AssignMaterial(inst, mat);

            var anim = inst.GetComponentInChildren<Animation>();
            if (anim != null) anim.playAutomatically = false;

            if (wander)
            {
                var w = inst.AddComponent<ActorWander>();
                w.walkSpeed = speed;
                w.wanderRadius = radius;
                w.minPause = minPause;
                w.maxPause = maxPause;
                w.maxStep = maxStep;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, PREFABS + "/" + prefabName + ".prefab");
            Object.DestroyImmediate(inst);
            return prefab;
        }

        static void BuildOrangan(Dictionary<string, Material> mats)
        {
            BuildOranganVariant("ORS_Orangan_A", "ORS_Orangan_B", mats["ORS_A"]);
            BuildOranganVariant("ORS_Orangan_B", "ORS_Orangan_A", mats["ORS_B"]);
        }

        static void BuildOranganVariant(string keep, string remove, Material mat)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(MODELS + "/ORS_Orangan.fbx");
            if (src == null) return;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = keep;
            inst.transform.localScale = Vector3.one * 2f;
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var stack = new List<Transform>();
            foreach (Transform child in inst.transform) stack.Add(child);
            while (stack.Count > 0)
            {
                var t = stack[0];
                stack.RemoveAt(0);
                if (t.name == remove)
                {
                    Object.DestroyImmediate(t.gameObject);
                    continue;
                }
                foreach (Transform c in t) stack.Add(c);
            }

            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = mat;

            PrefabUtility.SaveAsPrefabAsset(inst, PREFABS + "/" + keep + ".prefab");
            Object.DestroyImmediate(inst);
        }

        static void AssignMaterial(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        // ---------------------------------------------------------------- placement

        static void PlaceInstances()
        {
            var old = GameObject.Find("AsetTambahan");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("AsetTambahan");
            Physics.SyncTransforms();

            Vector2[] workers =
            {
                new Vector2(-140f, -90f), new Vector2(-110f, -30f), new Vector2(-80f, 50f),
                new Vector2(-40f, 0f), new Vector2(0f, -70f), new Vector2(30f, 60f),
                new Vector2(70f, -20f), new Vector2(100f, 40f), new Vector2(-140f, 60f),
                new Vector2(-60f, -80f), new Vector2(20f, 10f), new Vector2(60f, 90f),
                new Vector2(120f, -60f), new Vector2(150f, 20f), new Vector2(-20f, 90f),
                new Vector2(120f, 80f)
            };

            for (int i = 0; i < workers.Length; i++)
            {
                string id = "Pekerja_" + (i + 1).ToString("00");
                Place(PREFABS + "/" + id + ".prefab", workers[i], root.transform);
            }

            Vector2[] kerbau = { new Vector2(10f, 30f), new Vector2(-90f, 30f), new Vector2(80f, -40f) };
            for (int i = 0; i < kerbau.Length; i++)
                Place(PREFABS + "/KRB_Kerbau.prefab", kerbau[i], root.transform);

            Vector2[] ayam =
            {
                new Vector2(32f, -132f), new Vector2(40f, -129f), new Vector2(-9f, -133f),
                new Vector2(-16f, -130f), new Vector2(78f, -142f), new Vector2(85f, -139f),
                new Vector2(-34f, -140f), new Vector2(-27f, -137f)
            };
            for (int i = 0; i < ayam.Length; i++)
                Place(PREFABS + "/AYM_Ayam.prefab", ayam[i], root.transform);

            Place(PREFABS + "/ORS_Orangan_A.prefab", new Vector2(-30f, 20f), root.transform);
            Place(PREFABS + "/ORS_Orangan_B.prefab", new Vector2(90f, -10f), root.transform);

            // Pekerja duplikat yang berkeliaran di area perumahan.
            // Posisi sudah dicek bebas collider rumah dan berada di atas tanah.
            Vector2[] pekerjaPerumahan =
            {
                new Vector2(-120f, -170f), new Vector2(-80f, -170f), new Vector2(-40f, -170f),
                new Vector2(0f, -170f), new Vector2(40f, -170f), new Vector2(80f, -170f),
                new Vector2(120f, -170f), new Vector2(-80f, -150f)
            };
            for (int i = 0; i < pekerjaPerumahan.Length; i++)
            {
                string id = "Pekerja_" + ((i * 2 + 1) % 16 + 1).ToString("00");
                var go = Place(PREFABS + "/" + id + ".prefab", pekerjaPerumahan[i], root.transform);
                if (go == null) continue;

                var w = go.GetComponent<ActorWander>();
                if (w != null)
                {
                    w.wanderRadius = 4f;
                    w.minPause = 3f;
                    w.maxPause = 6f;
                }
            }

            // Pastikan semua karakter menempel di permukaan tanah yang aktif.
            SnapAsetTambahanToGround();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        // ---------------------------------------------------------------- ground snap

        [MenuItem("Tools/Latar Belakang/9. Snap Aset Tambahan ke Tanah", false, 9)]
        public static void SnapToGroundMenu()
        {
            int n = SnapAsetTambahanToGround();
            Debug.Log("[LatarBelakang] Snap Aset Tambahan: " + n + " objek.");
        }

        /// <summary>
        /// Menempelkan semua child AsetTambahan ke permukaan tanah (layer Ground).
        /// Dipanggil di akhir menu 4 dan menu 7 supaya karakter tidak tertimbun
        /// kalau collider sawah dibuat setelah penempatan.
        /// </summary>
        public static int SnapAsetTambahanToGround()
        {
            var root = GameObject.Find("AsetTambahan");
            if (root == null) return 0;

            Physics.SyncTransforms();
            int layer = LayerMask.NameToLayer("Ground");
            int mask = layer >= 0 ? 1 << layer : ~0;
            int snapped = 0;

            foreach (Transform c in root.transform)
            {
                RaycastHit hit;
                if (Physics.Raycast(c.position + Vector3.up * 200f, Vector3.down, out hit, 400f, mask, QueryTriggerInteraction.Ignore))
                {
                    c.position = new Vector3(c.position.x, hit.point.y + 0.02f, c.position.z);
                    snapped++;
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return snapped;
        }

        static GameObject Place(string prefabPath, Vector2 xz, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[LatarBelakang] Prefab tidak ditemukan: " + prefabPath);
                return null;
            }

            Vector3 pos;
            if (!FindGround(xz, out pos))
            {
                Debug.LogWarning("[LatarBelakang] Tanah tidak ditemukan di " + xz);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            return go;
        }

        static bool FindGround(Vector2 xz, out Vector3 pos)
        {
            int layer = LayerMask.NameToLayer("Ground");
            int mask = layer >= 0 ? 1 << layer : ~0;

            for (int ring = 0; ring < 6; ring++)
            {
                float radius = ring * 6f;
                int steps = ring == 0 ? 1 : 8;
                for (int i = 0; i < steps; i++)
                {
                    float ang = i * Mathf.PI * 2f / steps;
                    Vector3 origin = new Vector3(xz.x + Mathf.Cos(ang) * radius, 250f, xz.y + Mathf.Sin(ang) * radius);
                    RaycastHit hit;
                    if (Physics.Raycast(origin, Vector3.down, out hit, 500f, mask, QueryTriggerInteraction.Ignore))
                    {
                        pos = hit.point + Vector3.up * 0.02f;
                        return true;
                    }
                }
            }

            pos = Vector3.zero;
            return false;
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string name = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
