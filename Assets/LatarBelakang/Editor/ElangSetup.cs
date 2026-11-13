using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LatarBelakang.EditorTools
{
    /// <summary>
    /// Import BUR_Elang, buat prefab + material, lalu pasang 12 elang yang
    /// berputar di langit. Animasi sayap dibuat prosedural oleh ElangFlight.
    /// Menu: Tools > Latar Belakang > 10. Pasang Elang di Langit
    /// </summary>
    public static class ElangSetup
    {
        const string ROOT = "Assets/LatarBelakang";
        const string MODELS = ROOT + "/Models/Characters";
        const string TEX = MODELS + "/Textures";
        const string MATS = MODELS + "/Materials";
        const string PREFABS = ROOT + "/Prefabs/Characters";

        const string FBX = MODELS + "/BUR_Elang.fbx";
        const string TEXTURE = TEX + "/T_Elang_Atlas_512.png";
        const string MATERIAL = MATS + "/M_Elang_Atlas_Unity.mat";
        const string PREFAB = PREFABS + "/BUR_Elang.prefab";
        const string SKY_ROOT = "Elang_Langit";

        const string SOURCE_DIR = @"C:\TUGAS-TUGAS\TUGAS SEMESTER 5\tugas\Asset\BUR_Elang";

        struct EagleSpot
        {
            public Vector2 center;
            public float radius;
            public float altitude;
            public float speed;
            public float flapSpeed;
            public float flapIntensity;
            public float bank;
            public float bob;
            public bool clockwise;

            public EagleSpot(Vector2 c, float r, float a, float s, float fs, float fi, float b, float bobA, bool cw)
            {
                center = c;
                radius = r;
                altitude = a;
                speed = s;
                flapSpeed = fs;
                flapIntensity = fi;
                bank = b;
                bob = bobA;
                clockwise = cw;
            }
        }

        static readonly EagleSpot[] Spots =
        {
            new EagleSpot(new Vector2(0f, -100f),    55f, 50f, 11f, 1.8f, 1.00f, 18f, 0.8f, true),
            new EagleSpot(new Vector2(-70f, -150f),  45f, 38f,  9f, 1.6f, 0.90f, 16f, 0.6f, false),
            new EagleSpot(new Vector2(80f, -60f),    60f, 60f, 12f, 2.0f, 1.00f, 20f, 1.0f, true),
            new EagleSpot(new Vector2(-120f, -20f),  40f, 32f,  8f, 1.4f, 0.45f, 14f, 0.5f, false),
            new EagleSpot(new Vector2(130f, -180f),  70f, 68f, 13f, 1.9f, 1.00f, 21f, 1.1f, true),
            new EagleSpot(new Vector2(0f, -230f),    85f, 72f, 14f, 1.7f, 0.90f, 19f, 1.2f, false),
            new EagleSpot(new Vector2(-40f, 30f),    50f, 42f, 10f, 1.5f, 0.70f, 16f, 0.7f, true),
            new EagleSpot(new Vector2(70f, -240f),   38f, 28f,  8f, 2.2f, 1.00f, 15f, 0.4f, false),
            new EagleSpot(new Vector2(170f, -90f),   65f, 58f, 12f, 1.8f, 1.00f, 20f, 0.9f, true),
            new EagleSpot(new Vector2(-170f, -110f), 55f, 48f, 10f, 1.6f, 0.80f, 17f, 0.7f, false),
            new EagleSpot(new Vector2(30f, -280f),   90f, 78f, 14f, 1.5f, 0.55f, 22f, 1.3f, true),
            new EagleSpot(new Vector2(-110f, -250f), 45f, 36f,  9f, 2.0f, 1.00f, 15f, 0.5f, false),
        };

        [MenuItem("Tools/Latar Belakang/10. Pasang Elang di Langit", false, 10)]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "SampleScene")
            {
                Debug.LogWarning("[Elang] Buka SampleScene dulu sebelum menjalankan menu ini.");
                return;
            }

            EnsureSourceAssets();
            ConfigureImporter();
            var material = CreateMaterial();
            var prefab = CreatePrefab(material);
            if (prefab == null) return;

            var old = GameObject.Find(SKY_ROOT);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(SKY_ROOT);
            Physics.SyncTransforms();

            int placed = 0;
            for (int i = 0; i < Spots.Length; i++)
            {
                var spot = Spots[i];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                go.name = "Elang_" + (i + 1).ToString("00");
                go.transform.position = new Vector3(spot.center.x, 0f, spot.center.y);
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 2f;

                var flight = go.GetComponent<ElangFlight>();
                if (flight != null)
                {
                    flight.center = new Vector3(spot.center.x, 0f, spot.center.y);
                    flight.radius = spot.radius;
                    flight.altitude = spot.altitude;
                    flight.speed = spot.speed;
                    flight.clockwise = spot.clockwise;
                    flight.startAngle = Random.Range(0f, Mathf.PI * 2f);
                    flight.bankAngle = spot.bank;
                    flight.bobAmplitude = spot.bob;
                    flight.bobSpeed = Random.Range(0.35f, 0.8f);
                    flight.flapSpeed = spot.flapSpeed;
                    flight.flapIntensity = spot.flapIntensity;
                    flight.flapPhase = Random.Range(0f, 1f);
                }

                placed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Elang] " + placed + " elang dipasang di langit (skala 2x).");
        }

        static void EnsureSourceAssets()
        {
            if (!File.Exists(FBX) && File.Exists(SOURCE_DIR + "/BUR_Elang.fbx"))
            {
                Directory.CreateDirectory(MODELS);
                File.Copy(SOURCE_DIR + "/BUR_Elang.fbx", FBX, true);
            }

            if (!File.Exists(TEXTURE) && File.Exists(SOURCE_DIR + "/Textures/T_Elang_Atlas_512.png"))
            {
                Directory.CreateDirectory(TEX);
                File.Copy(SOURCE_DIR + "/Textures/T_Elang_Atlas_512.png", TEXTURE, true);
            }

            if (!File.Exists(FBX))
            {
                Debug.LogError("[Elang] FBX tidak ditemukan: " + FBX);
                return;
            }

            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(FBX, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(TEXTURE, ImportAssetOptions.ForceSynchronousImport);
        }

        static void ConfigureImporter()
        {
            var mi = AssetImporter.GetAtPath(FBX) as ModelImporter;
            if (mi == null)
            {
                Debug.LogError("[Elang] ModelImporter tidak ditemukan untuk " + FBX);
                return;
            }

            mi.globalScale = 1f;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.isReadable = false;
            mi.optimizeGameObjects = false;
            mi.SaveAndReimport();
            AssetDatabase.ImportAsset(FBX, ImportAssetOptions.ForceSynchronousImport);
        }

        static Material CreateMaterial()
        {
            EnsureFolder(MATS);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, MATERIAL);
            }

            mat.shader = Shader.Find("Standard");
            mat.color = Color.white;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE);
            if (tex != null) mat.mainTexture = tex;
            mat.SetFloat("_Glossiness", 0.22f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject CreatePrefab(Material material)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
            if (src == null)
            {
                Debug.LogError("[Elang] Gagal load FBX: " + FBX);
                return null;
            }

            EnsureFolder(PREFABS);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = "BUR_Elang";
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            if (PrefabUtility.IsPartOfPrefabInstance(inst))
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Kita memakai rotasi bone langsung; Animator/Animation tidak diperlukan.
            foreach (var a in inst.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(a);
            foreach (var a in inst.GetComponentsInChildren<Animation>(true))
                Object.DestroyImmediate(a);

            if (material != null)
            {
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    r.sharedMaterials = mats;
                }
            }

            if (inst.GetComponent<ElangFlight>() == null)
                inst.AddComponent<ElangFlight>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, PREFAB);
            Object.DestroyImmediate(inst);
            return prefab;
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
