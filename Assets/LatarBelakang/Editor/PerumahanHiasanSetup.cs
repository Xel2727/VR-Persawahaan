using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LatarBelakang.EditorTools
{
    /// <summary>
    /// Menambah hiasan di area perumahan: pagar, bangku, meja, pot, jemuran,
    /// jerami, warung, lampu, bendera, gerobak, plus ayam dan pekerja.
    /// Menu: Tools > Latar Belakang > 11. Pasang Hiasan Perumahan
    /// </summary>
    public static class PerumahanHiasanSetup
    {
        const string ROOT = "Assets/LatarBelakang";
        const string MATS = ROOT + "/Materials/Hiasan";
        const string HIASAN_ROOT = "Hiasan_Perumahan";
        const string POP = "LatarBelakang/POP_RumahKampung";
        const string CHICKEN = ROOT + "/Prefabs/Characters/AYM_Ayam.prefab";
        const string WORKER_FMT = ROOT + "/Prefabs/Characters/Pekerja_{0:00}.prefab";

        static Material _kayu, _bambu, _atap, _merah, _biru, _kuning, _daun, _tanah, _lampu;

        class HouseInfo
        {
            public Transform t;
            public Vector3 center;
            public float radius;
        }

        [MenuItem("Tools/Latar Belakang/11. Pasang Hiasan Perumahan", false, 11)]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "SampleScene")
            {
                Debug.LogWarning("[Hiasan] Buka SampleScene dulu sebelum menjalankan menu ini.");
                return;
            }

            var pop = GameObject.Find(POP);
            if (pop == null)
            {
                Debug.LogWarning("[Hiasan] POP_RumahKampung tidak ditemukan. Jalankan menu 2 dulu.");
                return;
            }

            var houses = CollectHouses(pop.transform);
            if (houses.Count == 0)
            {
                Debug.LogWarning("[Hiasan] Tidak ada objek Rumah_XX di bawah POP_RumahKampung.");
                return;
            }

            CreateMaterials();

            var old = GameObject.Find(HIASAN_ROOT);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(HIASAN_ROOT);
            Physics.SyncTransforms();

            Random.InitState(20261113);

            var placedProps = new List<Vector3>();
            var placedAnimals = new List<Vector3>();
            int propCount = 0;

            for (int i = 0; i < houses.Count; i++)
            {
                var house = houses[i];
                var types = PickTypes(3);
                float baseAngle = Random.Range(0f, 360f);

                for (int j = 0; j < types.Count; j++)
                {
                    Vector3 ground;
                    float ang = baseAngle + j * (360f / types.Count) + Random.Range(-28f, 28f);
                    if (!TrySpot(house, houses, 2f, 6f, ang, placedProps, out ground)) continue;

                    CreateProp(types[j], root.transform, ground, Random.Range(0f, 360f));
                    placedProps.Add(ground);
                    propCount++;
                }
            }

            // Warung di beberapa rumah, lebih jauh dari rumah.
            int[] warungHouses = { 0, 7, 14 };
            foreach (int idx in warungHouses)
            {
                if (idx >= houses.Count) continue;
                Vector3 ground;
                float ang = Random.Range(0f, 360f);
                if (TrySpot(houses[idx], houses, 7f, 12f, ang, placedProps, out ground))
                {
                    CreateWarung(root.transform, ground, Random.Range(0f, 360f));
                    placedProps.Add(ground);
                }
            }

            int chickens = PlaceAnimals(CHICKEN, 8, root.transform, houses, placedAnimals, 2f, 4f);

            int workers = 0;
            for (int i = 0; i < 5; i++)
            {
                string path = string.Format(WORKER_FMT, (i * 3 + 1) % 16 + 1);
                if (PlaceAnimal(path, root.transform, houses, placedAnimals, 3f, 6f)) workers++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log(string.Format("[Hiasan] {0} properti, {1} ayam, {2} pekerja di {3} rumah.",
                propCount, chickens, workers, houses.Count));
        }

        // ---------------------------------------------------------------- houses

        static List<HouseInfo> CollectHouses(Transform pop)
        {
            var list = new List<HouseInfo>();
            var seen = new HashSet<string>();

            foreach (var t in pop.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Rumah_", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (t.name.EndsWith("_baked", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(t.name)) continue;

                var info = new HouseInfo { t = t };

                bool hasBounds = false;
                Bounds b = new Bounds(t.position, Vector3.zero);
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hasBounds) { b = r.bounds; hasBounds = true; }
                    else b.Encapsulate(r.bounds);
                }

                if (hasBounds)
                {
                    info.center = new Vector3(b.center.x, 0f, b.center.z);
                    info.radius = Mathf.Max(b.extents.x, b.extents.z);
                }
                else
                {
                    info.center = new Vector3(t.position.x, 0f, t.position.z);
                    info.radius = 5f;
                }

                info.radius = Mathf.Max(2f, info.radius);
                list.Add(info);
            }

            list.Sort((a, b) => string.Compare(a.t.name, b.t.name, System.StringComparison.Ordinal));
            return list;
        }

        // ---------------------------------------------------------------- placement

        static bool TrySpot(HouseInfo house, List<HouseInfo> all, float extraMin, float extraMax,
            float angleDeg, List<Vector3> placed, out Vector3 ground)
        {
            ground = Vector3.zero;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                float ang = (angleDeg + Random.Range(-35f, 35f)) * Mathf.Deg2Rad;
                float dist = house.radius + Random.Range(extraMin, extraMax);
                Vector3 c = new Vector3(house.center.x + Mathf.Cos(ang) * dist, 0f,
                                        house.center.z + Mathf.Sin(ang) * dist);

                bool blocked = false;

                foreach (var h in all)
                {
                    if (h == house) continue;
                    Vector2 d = new Vector2(c.x - h.center.x, c.z - h.center.z);
                    if (d.magnitude < h.radius + 2f) { blocked = true; break; }
                }
                if (blocked) continue;

                foreach (var p in placed)
                {
                    Vector2 d = new Vector2(c.x - p.x, c.z - p.z);
                    if (d.magnitude < 2f) { blocked = true; break; }
                }
                if (blocked) continue;

                if (!FindGround(c, out ground)) continue;
                return true;
            }

            return false;
        }

        static bool TryAnimalSpot(List<HouseInfo> houses, List<Vector3> placed,
            float extraMin, float extraMax, out Vector3 ground)
        {
            ground = Vector3.zero;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                var house = houses[Random.Range(0, houses.Count)];
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = house.radius + Random.Range(extraMin, extraMax);
                Vector3 c = new Vector3(house.center.x + Mathf.Cos(ang) * dist, 0f,
                                        house.center.z + Mathf.Sin(ang) * dist);

                bool blocked = false;

                foreach (var h in houses)
                {
                    if (h == house) continue;
                    Vector2 d = new Vector2(c.x - h.center.x, c.z - h.center.z);
                    if (d.magnitude < h.radius + 1.5f) { blocked = true; break; }
                }
                if (blocked) continue;

                foreach (var p in placed)
                {
                    Vector2 d = new Vector2(c.x - p.x, c.z - p.z);
                    if (d.magnitude < 1.5f) { blocked = true; break; }
                }
                if (blocked) continue;

                if (!FindGround(c, out ground)) continue;
                return true;
            }

            return false;
        }

        static bool FindGround(Vector3 xz, out Vector3 ground)
        {
            ground = Vector3.zero;
            int layer = LayerMask.NameToLayer("Ground");
            int mask = layer >= 0 ? 1 << layer : ~0;

            RaycastHit hit;
            if (Physics.Raycast(new Vector3(xz.x, 250f, xz.z), Vector3.down, out hit, 500f, mask, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point;
                return true;
            }
            return false;
        }

        static List<string> PickTypes(int count)
        {
            var all = new List<string>
            {
                "pagar", "bangku", "pot", "jerami", "jemuran",
                "kayu", "karung", "lampu", "bendera", "gerobak", "meja"
            };

            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                string tmp = all[i];
                all[i] = all[j];
                all[j] = tmp;
            }

            return all.GetRange(0, Mathf.Min(count, all.Count));
        }

        static int PlaceAnimals(string prefabPath, int count, Transform root,
            List<HouseInfo> houses, List<Vector3> placed, float minExtra, float maxExtra)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Hiasan] Prefab hewan tidak ditemukan: " + prefabPath);
                return 0;
            }

            int result = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 ground;
                if (!TryAnimalSpot(houses, placed, minExtra, maxExtra, out ground)) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = prefab.name + "_Hiasan_" + (i + 1);
                go.transform.position = ground;
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                var w = go.GetComponent<ActorWander>();
                if (w != null)
                {
                    w.wanderRadius = Random.Range(2f, 4f);
                    w.walkSpeed = Random.Range(0.3f, 0.5f);
                    w.minPause = Random.Range(1.5f, 3f);
                    w.maxPause = Random.Range(3f, 6f);
                }

                placed.Add(ground);
                result++;
            }

            return result;
        }

        static bool PlaceAnimal(string prefabPath, Transform root, List<HouseInfo> houses,
            List<Vector3> placed, float minExtra, float maxExtra)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;

            Vector3 ground;
            if (!TryAnimalSpot(houses, placed, minExtra, maxExtra, out ground)) return false;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = prefab.name + "_Hiasan";
            go.transform.position = ground;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var w = go.GetComponent<ActorWander>();
            if (w != null)
            {
                w.wanderRadius = Random.Range(3f, 5f);
                w.walkSpeed = Random.Range(0.7f, 1f);
                w.minPause = Random.Range(2f, 3.5f);
                w.maxPause = Random.Range(4f, 6f);
            }

            placed.Add(ground);
            return true;
        }

        // ---------------------------------------------------------------- props

        static void CreateProp(string type, Transform root, Vector3 ground, float yaw)
        {
            switch (type)
            {
                case "pagar": CreateFence(root, ground, yaw); break;
                case "bangku": CreateBench(root, ground, yaw); break;
                case "pot": CreatePots(root, ground, yaw); break;
                case "jerami": CreateHaystack(root, ground, yaw); break;
                case "jemuran": CreateLaundry(root, ground, yaw); break;
                case "kayu": CreateFirewood(root, ground, yaw); break;
                case "karung": CreateSacks(root, ground, yaw); break;
                case "lampu": CreateLamp(root, ground, yaw); break;
                case "bendera": CreateFlag(root, ground, yaw); break;
                case "gerobak": CreateCart(root, ground, yaw); break;
                case "meja": CreateTable(root, ground, yaw); break;
            }
        }

        static void CreateFence(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Pagar", ground, yaw);
            const float len = 3f;

            for (int i = 0; i < 4; i++)
                Part(p, "Tiang", PrimitiveType.Cube,
                    new Vector3(-len * 0.5f + i * (len / 3f), 0.45f, 0f),
                    new Vector3(0.08f, 0.9f, 0.08f), _kayu);

            Part(p, "RelBawah", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0f),
                new Vector3(len, 0.07f, 0.06f), _kayu);
            Part(p, "RelAtas", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f),
                new Vector3(len, 0.07f, 0.06f), _kayu);
        }

        static void CreateBench(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Bangku", ground, yaw);
            Part(p, "Dudukan", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f),
                new Vector3(1.6f, 0.08f, 0.4f), _kayu);
            Part(p, "KakiL", PrimitiveType.Cube, new Vector3(-0.65f, 0.225f, 0f),
                new Vector3(0.08f, 0.45f, 0.35f), _kayu);
            Part(p, "KakiR", PrimitiveType.Cube, new Vector3(0.65f, 0.225f, 0f),
                new Vector3(0.08f, 0.45f, 0.35f), _kayu);
        }

        static void CreateTable(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Meja", ground, yaw);
            Part(p, "Meja", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f),
                new Vector3(1.4f, 0.08f, 0.8f), _kayu);

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Part(p, "Kaki", PrimitiveType.Cube,
                        new Vector3(sx * 0.55f, 0.375f, sz * 0.3f),
                        new Vector3(0.08f, 0.75f, 0.08f), _kayu);

            Part(p, "BangkuL", PrimitiveType.Cube, new Vector3(-1.05f, 0.45f, 0f),
                new Vector3(0.4f, 0.08f, 0.8f), _kayu);
            Part(p, "BangkuR", PrimitiveType.Cube, new Vector3(1.05f, 0.45f, 0f),
                new Vector3(0.4f, 0.08f, 0.8f), _kayu);
        }

        static void CreatePots(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Pot", ground, yaw);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.6f;
                Part(p, "Pot", PrimitiveType.Cylinder, new Vector3(x, 0.22f, 0f),
                    new Vector3(0.45f, 0.22f, 0.45f), _tanah);
                Part(p, "Daun", PrimitiveType.Sphere, new Vector3(x, 0.58f, 0f),
                    new Vector3(0.5f, 0.5f, 0.5f), _daun);
            }
        }

        static void CreateHaystack(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Jerami", ground, yaw);
            Part(p, "Jerami", PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f),
                new Vector3(1.6f, 0.7f, 1.6f), _bambu);
            Part(p, "Puncak", PrimitiveType.Sphere, new Vector3(0f, 1.5f, 0f),
                new Vector3(1.8f, 0.7f, 1.8f), _bambu);
        }

        static void CreateLaundry(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Jemuran", ground, yaw);
            const float len = 3f;

            Part(p, "TiangL", PrimitiveType.Cube, new Vector3(-len * 0.5f, 1f, 0f),
                new Vector3(0.07f, 2f, 0.07f), _bambu);
            Part(p, "TiangR", PrimitiveType.Cube, new Vector3(len * 0.5f, 1f, 0f),
                new Vector3(0.07f, 2f, 0.07f), _bambu);
            Part(p, "Tali", PrimitiveType.Cube, new Vector3(0f, 1.9f, 0f),
                new Vector3(len, 0.02f, 0.02f), _bambu);

            Part(p, "Kain1", PrimitiveType.Cube, new Vector3(-0.9f, 1.55f, 0f),
                new Vector3(0.55f, 0.7f, 0.02f), _merah);
            Part(p, "Kain2", PrimitiveType.Cube, new Vector3(0f, 1.55f, 0f),
                new Vector3(0.55f, 0.7f, 0.02f), _biru);
            Part(p, "Kain3", PrimitiveType.Cube, new Vector3(0.9f, 1.55f, 0f),
                new Vector3(0.55f, 0.7f, 0.02f), _kuning);
        }

        static void CreateFirewood(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Kayu", ground, yaw);
            Quaternion lying = Quaternion.Euler(0f, 0f, 90f);

            for (int i = 0; i < 4; i++)
                Part(p, "Kayu", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.1f + i * 0.18f, (i % 2) * 0.18f),
                    new Vector3(0.12f, 0.5f, 0.12f), _kayu, lying);
        }

        static void CreateSacks(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Karung", ground, yaw);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.45f;
                Part(p, "Karung", PrimitiveType.Cylinder, new Vector3(x, 0.25f, 0f),
                    new Vector3(0.45f, 0.25f, 0.45f), _bambu);
            }
        }

        static void CreateLamp(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Lampu", ground, yaw);
            Part(p, "Tiang", PrimitiveType.Cube, new Vector3(0f, 1.1f, 0f),
                new Vector3(0.07f, 2.2f, 0.07f), _kayu);
            Part(p, "Lampu", PrimitiveType.Sphere, new Vector3(0f, 2.2f, 0f),
                new Vector3(0.35f, 0.35f, 0.35f), _lampu);
        }

        static void CreateFlag(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Bendera", ground, yaw);
            Part(p, "Tiang", PrimitiveType.Cube, new Vector3(0f, 1f, 0f),
                new Vector3(0.05f, 2f, 0.05f), _bambu);
            Part(p, "Bendera", PrimitiveType.Cube, new Vector3(0.4f, 1.7f, 0f),
                new Vector3(0.8f, 0.5f, 0.02f), _merah);
        }

        static void CreateCart(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Gerobak", ground, yaw);
            Quaternion wheel = Quaternion.Euler(0f, 0f, 90f);

            Part(p, "Badan", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f),
                new Vector3(1.6f, 0.5f, 0.9f), _kayu);
            Part(p, "RodaL", PrimitiveType.Cylinder, new Vector3(-0.85f, 0.35f, 0f),
                new Vector3(0.7f, 0.05f, 0.7f), _kayu, wheel);
            Part(p, "RodaR", PrimitiveType.Cylinder, new Vector3(0.85f, 0.35f, 0f),
                new Vector3(0.7f, 0.05f, 0.7f), _kayu, wheel);
            Part(p, "Pegangan", PrimitiveType.Cube, new Vector3(0f, 0.55f, 1f),
                new Vector3(0.08f, 0.08f, 1.2f), _kayu);
        }

        static void CreateWarung(Transform root, Vector3 ground, float yaw)
        {
            var p = NewProp(root, "Hiasan_Warung", ground, yaw);
            const float w = 3.2f;
            const float d = 2.4f;

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Part(p, "Tiang", PrimitiveType.Cube,
                        new Vector3(sx * w * 0.5f, 1.1f, sz * d * 0.5f),
                        new Vector3(0.08f, 2.2f, 0.08f), _kayu);

            Part(p, "Atap", PrimitiveType.Cube, new Vector3(0f, 2.3f, 0f),
                new Vector3(w + 0.5f, 0.12f, d + 0.5f), _atap, Quaternion.Euler(4f, 0f, 0f));
            Part(p, "Meja", PrimitiveType.Cube, new Vector3(0f, 0.4f, -0.4f),
                new Vector3(w - 0.6f, 0.8f, 0.8f), _kayu);
            Part(p, "Bangku", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.9f),
                new Vector3(w - 0.6f, 0.08f, 0.4f), _kayu);
            Part(p, "Banner", PrimitiveType.Cube, new Vector3(0f, 1.75f, -d * 0.5f + 0.05f),
                new Vector3(w - 0.6f, 0.6f, 0.02f), _merah);
        }

        static Transform NewProp(Transform root, string name, Vector3 ground, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = ground;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.transform;
        }

        static GameObject Part(Transform parent, string name, PrimitiveType type,
            Vector3 localPos, Vector3 localScale, Material material, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (localRot.HasValue) go.transform.localRotation = localRot.Value;

            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ---------------------------------------------------------------- materials

        static void CreateMaterials()
        {
            EnsureFolder(MATS);

            _kayu = MakeMaterial("M_Hiasan_Kayu", new Color(0.45f, 0.30f, 0.18f));
            _bambu = MakeMaterial("M_Hiasan_Bambu", new Color(0.72f, 0.60f, 0.35f));
            _atap = MakeMaterial("M_Hiasan_Atap", new Color(0.35f, 0.18f, 0.12f));
            _merah = MakeMaterial("M_Hiasan_Kain_Merah", new Color(0.75f, 0.12f, 0.10f));
            _biru = MakeMaterial("M_Hiasan_Kain_Biru", new Color(0.12f, 0.30f, 0.65f));
            _kuning = MakeMaterial("M_Hiasan_Kain_Kuning", new Color(0.90f, 0.72f, 0.12f));
            _daun = MakeMaterial("M_Hiasan_Daun", new Color(0.20f, 0.50f, 0.15f));
            _tanah = MakeMaterial("M_Hiasan_Tanah", new Color(0.35f, 0.22f, 0.12f));
            _lampu = MakeMaterial("M_Hiasan_Lampu", new Color(1f, 0.75f, 0.35f), true);
        }

        static Material MakeMaterial(string name, Color color, bool emission = false)
        {
            string path = MATS + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find("Standard");
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.05f);

            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
