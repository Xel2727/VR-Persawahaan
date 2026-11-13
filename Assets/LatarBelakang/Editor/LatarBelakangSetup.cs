using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LatarBelakang.EditorTools
{
    /// <summary>
    /// Pipeline import "Latar Background fiks.blend" (Blender) -> Unity.
    /// Data material diambil dari Assets/LatarBelakang/Data/sceneinfo.json (hasil dump Blender).
    /// </summary>
    public static class LatarBelakangSetup
    {
        const string ROOT = "Assets/LatarBelakang";
        const string MODELS = ROOT + "/Models";
        const string TEXTURES = ROOT + "/Textures";
        const string MODELS_MATS = MODELS + "/Materials";
        const string MATERIALS = ROOT + "/Materials";
        const string DATA = ROOT + "/Data/sceneinfo.json";
        const string PREFABS = ROOT + "/Prefabs";
        const string COL_MODEL = MODELS + "/COL_Terrain.fbx";

        static readonly string[] ModelFiles =
        {
            "ENV_ModulSawah.fbx",
            "POP_RumahKampung.fbx",
            "SKY_Awan.fbx",
        };

        // ---------------------------------------------------------------- data classes

        [System.Serializable]
        class MatDef
        {
            public string name;
            public float[] baseColorSrgb;
            public float metallic;
            public float roughness;
            public float alpha;
            public string texture;
            public string textureFile;
            public string blendMethod;
            public bool backfaceCulling;
        }

        [System.Serializable]
        class SceneData
        {
            public MatDef[] materials;
        }

        // ---------------------------------------------------------------- menu 1

        [MenuItem("Tools/Latar Belakang/1. Import Model & Material", false, 1)]
        public static void ImportAndSetupMaterials()
        {
            ConfigureTextures();
            ConfigureModels();
            PatchMaterials();
            CreateSkyMaterial();
            AssetDatabase.SaveAssets();
            Debug.Log("[LatarBelakang] Import & material selesai.");
        }

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TEXTURES }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                var file = Path.GetFileNameWithoutExtension(path);
                bool atlas = file.StartsWith("Atlas_Rumah");
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Bilinear;
                ti.anisoLevel = 2;
                // VR: atlas rumah 2048 -> 1024 supaya hemat VRAM
                ti.maxTextureSize = atlas ? 1024 : 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            Debug.Log("[LatarBelakang] Texture settings diterapkan.");
        }

        static void ConfigureModels()
        {
            foreach (var f in ModelFiles)
            {
                var path = MODELS + "/" + f;
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null)
                {
                    Debug.LogError("[LatarBelakang] ModelImporter tidak ditemukan: " + path);
                    continue;
                }

                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.bakeAxisConversion = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importBlendShapes = false;
                mi.importVisibility = false;
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.addCollider = false;
                mi.isReadable = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.optimizeMeshPolygons = true;
                mi.optimizeMeshVertices = true;
                mi.weldVertices = true;
                mi.generateSecondaryUV = false;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importTangents = ModelImporterTangents.CalculateMikk;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.External;
                mi.SaveAndReimport();

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    var b = new Bounds();
                    bool first = true;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        if (first) { b = r.bounds; first = false; }
                        else b.Encapsulate(r.bounds);
                    }
                    Debug.Log(string.Format("[LatarBelakang] {0}: renderer={1} bounds={2} size={3}",
                        f, go.GetComponentsInChildren<Renderer>(true).Length, b.center, b.size));
                }
            }
        }

        static SceneData LoadData()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(DATA);
            if (ta == null)
            {
                Debug.LogWarning("[LatarBelakang] sceneinfo.json tidak ada, material memakai warna bawaan FBX.");
                return null;
            }
            return JsonUtility.FromJson<SceneData>(ta.text);
        }

        static string BaseName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            int space = n.LastIndexOf(' ');
            if (space > 0)
            {
                var tail = n.Substring(space + 1);
                if (tail.Length > 0 && char.IsDigit(tail[0])) return n.Substring(0, space);
            }
            int dot = n.LastIndexOf('.');
            if (dot > 0 && n.Length - dot == 4 && int.TryParse(n.Substring(dot + 1), out _)) return n.Substring(0, dot);
            return n;
        }

        static Color Linear(float[] srgb)
        {
            if (srgb == null || srgb.Length < 3) return Color.white;
            // Unity sudah mengonversi color property sRGB -> linear otomatis (proyek Linear),
            // jadi nilai sRGB dari Blender dipakai apa adanya.
            return new Color(
                Mathf.Clamp01(srgb[0]),
                Mathf.Clamp01(srgb[1]),
                Mathf.Clamp01(srgb[2]), 1f);
        }

        static void PatchMaterials()
        {
            var data = LoadData();
            var defs = new Dictionary<string, MatDef>();
            var texDefs = new Dictionary<string, MatDef>();
            if (data != null && data.materials != null)
                foreach (var d in data.materials)
                {
                    if (!string.IsNullOrEmpty(d.name) && !defs.ContainsKey(d.name))
                        defs[d.name] = d;
                    // Unity menamai material bertekstur sesuai nama file tekstur
                    if (!string.IsNullOrEmpty(d.textureFile))
                    {
                        var texKey = Path.GetFileNameWithoutExtension(d.textureFile);
                        if (!texDefs.ContainsKey(texKey)) texDefs[texKey] = d;
                    }
                }

            var standard = Shader.Find("Standard");
            int ok = 0, textured = 0, missing = 0;
            var unmatched = new List<string>();

            var searchFolders = AssetDatabase.IsValidFolder(MATERIALS)
                ? new[] { MODELS_MATS, MATERIALS }
                : new[] { MODELS_MATS };

            foreach (var guid in AssetDatabase.FindAssets("t:Material", searchFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Sky_")) continue;   // jangan sentuh material skybox
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                mat.shader = standard;

                MatDef def = null;
                if (!defs.TryGetValue(mat.name, out def))
                    texDefs.TryGetValue(mat.name, out def);
                if (def == null)
                    defs.TryGetValue(BaseName(mat.name), out def);

                if (def != null)
                {
                    bool hasTex = !string.IsNullOrEmpty(def.textureFile);
                    mat.SetColor("_Color", hasTex ? Color.white : Linear(def.baseColorSrgb));
                    mat.SetFloat("_Glossiness", Mathf.Clamp01(1f - def.roughness));
                    mat.SetFloat("_Metallic", Mathf.Clamp01(def.metallic));
                    mat.SetFloat("_GlossMapScale", 1f);
                    mat.SetFloat("_SpecularHighlights", 0f);
                    mat.SetFloat("_GlossyReflections", def.roughness < 0.6f ? 1f : 0f);

                    if (hasTex)
                    {
                        var texPath = TEXTURES + "/" + def.textureFile;
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        if (tex != null)
                        {
                            mat.SetTexture("_MainTex", tex);
                            mat.mainTextureScale = Vector2.one;
                            mat.mainTextureOffset = Vector2.zero;
                            textured++;
                        }
                        else missing++;
                    }

                    if (def.backfaceCulling) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
                    else mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    ok++;
                }
                else
                {
                    // material karakter (Skin/Shirt/...) biarkan apa adanya dari FBX
                    mat.SetFloat("_Glossiness", 0.25f);
                    mat.SetFloat("_Metallic", 0f);
                    EditorUtility.SetDirty(mat);
                    if (unmatched.Count < 40) unmatched.Add(mat.name);
                }
            }

            Debug.Log(string.Format("[LatarBelakang] Material dipatch: {0} pakai data Blender, {1} tekstur terpasang, {2} tekstur tidak ketemu.",
                ok, textured, missing));
            if (unmatched.Count > 0)
                Debug.Log("[LatarBelakang] Material tanpa data: " + string.Join(", ", unmatched));
        }

        static void CreateSkyMaterial()
        {
            EnsureFolder(MATERIALS);
            var skyShader = Shader.Find("LatarBelakang/SkyGradient");
            if (skyShader == null)
            {
                Debug.LogError("[LatarBelakang] Shader SkyGradient tidak ketemu.");
                return;
            }

            var path = MATERIALS + "/Sky_LatarBelakang.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(skyShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = skyShader;
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURES + "/T_Langit_Gradient_512.png"));
            mat.SetColor("_Tint", Color.white);
            mat.SetFloat("_Exposure", 1f);
            mat.SetFloat("_Horizon", 1.1f);
            EditorUtility.SetDirty(mat);
            Debug.Log("[LatarBelakang] Sky material dibuat: " + path);
        }

        // ---------------------------------------------------------------- menu 2

        [MenuItem("Tools/Latar Belakang/2. Pasang Di Scene", false, 2)]
        public static void PlaceInScene()
        {
            var scene = SceneManager.GetActiveScene();

            var old = GameObject.Find("LatarBelakang");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("LatarBelakang");
            var env = InstantiateModel("ENV_ModulSawah", root.transform);
            var pop = InstantiateModel("POP_RumahKampung", root.transform);
            var sky = InstantiateModel("SKY_Awan", root.transform);

            // static batching untuk environment & populasi (bukan awan)
            if (env != null) MarkStatic(env);
            if (pop != null) MarkStatic(pop);
            if (env != null) OptimizeShadows(env, 4f);
            if (pop != null) OptimizeShadows(pop, 1.5f);

            if (sky != null) AddCloudDrift(sky);
            if (env != null)
            {
                var water = env.AddComponent<WaterUVScroll>();
                water.scrollSpeed = new Vector2(0.01f, 0.05f);
            }

            SetupCamera();
            SetupLight();
            SetupSky();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[LatarBelakang] Prefab dipasang di scene " + scene.name);
        }

        static GameObject InstantiateModel(string modelName, Transform parent)
        {
            EnsureFolder(PREFABS);

            var modelPath = MODELS + "/" + modelName + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError("[LatarBelakang] Model tidak ketemu: " + modelPath);
                return null;
            }

            var prefabPath = PREFABS + "/" + modelName + ".prefab";
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);

            var src = prefab != null ? prefab : model;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            return inst;
        }

        static void MarkStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags | StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>Object kecil tidak perlu cast shadow -> memangkas draw call pass shadow (penting untuk VR).</summary>
        static void OptimizeShadows(GameObject go, float maxSize)
        {
            int off = 0, total = 0;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                total++;
                var s = r.bounds.size;
                float d = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (d < maxSize && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    off++;
                }
            }
            Debug.Log(string.Format("[LatarBelakang] Shadow off untuk {0}/{1} renderer (<{2} m) pada {3}", off, total, maxSize, go.name));
        }

        static void AddCloudDrift(GameObject skyRoot)
        {
            int n = 0;
            foreach (var r in skyRoot.GetComponentsInChildren<Renderer>(true))
            {
                var c = r.gameObject.GetComponent<CloudDrift>();
                if (c == null) c = r.gameObject.AddComponent<CloudDrift>();
                c.speed = 2.5f;
                c.wrapRadius = 2200f;
                c.direction = new Vector3(1f, 0f, 0.15f);
                n++;
            }
            Debug.Log("[LatarBelakang] CloudDrift dipasang di " + n + " awan.");
        }

        static void SetupCamera()
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                camGo.tag = "MainCamera";
            }
            var cam = camGo.GetComponent<Camera>();
            camGo.transform.position = new Vector3(405f, 245f, 435f);
            camGo.transform.rotation = Quaternion.Euler(24.37f, -139.57f, 0f);
            cam.fieldOfView = 28.3f;   // 50mm lens, sensor 36mm, aspect 10:7
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = false;      // VR: hemat bandwidth
            cam.allowMSAA = true;
        }

        static void SetupLight()
        {
            var go = GameObject.Find("Directional Light");
            if (go == null)
            {
                go = new GameObject("Directional Light");
                go.AddComponent<Light>().type = LightType.Directional;
            }
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.961f, 0.878f);
            light.intensity = 1.45f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.6f;
            go.transform.rotation = Quaternion.Euler(43.87f, -148.05f, -3.54f);
        }

        static void SetupSky()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + "/Sky_LatarBelakang.mat");
            if (mat == null) return;
            RenderSettings.skybox = mat;

            // Ambient flat: lebih stabil & murah untuk VR daripada ambient dari skybox custom
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.63f, 0.74f);
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.reflectionIntensity = 0.25f;

            // Fog tipis untuk kedalaman
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00035f;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.95f);

            DynamicGI.UpdateEnvironment();
        }

        // ---------------------------------------------------------------- menu 3

        [MenuItem("Tools/Latar Belakang/3. Screenshot Preview", false, 3)]
        public static void ScreenshotPreview()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../opencode-lb-shots"));
            Directory.CreateDirectory(dir);

            Shot(dir, "01_depan", new Vector3(405f, 245f, 435f), new Vector3(24.37f, -139.57f, 0f), 28.3f);
            Shot(dir, "02_top", new Vector3(0f, 700f, 0f), new Vector3(90f, 0f, 0f), 45f);
            Shot(dir, "03_darat", new Vector3(25.55f, 10.43f, 111.34f), new Vector3(4.99f, 143.54f, -6.96f), 59.6f);
            Shot(dir, "04_timur", new Vector3(330f, 120f, 230f), new Vector3(27.58f, -148.82f, 0f), 34.9f);
            Debug.Log("[LatarBelakang] Screenshot disimpan di " + dir);
        }

        internal static void Shot(string dir, string name, Vector3 pos, Vector3 euler, float fov)
        {
            var camGo = new GameObject("__shot_cam");
            var cam = camGo.AddComponent<Camera>();
            cam.CopyFrom(Camera.main);
            camGo.transform.position = pos;
            camGo.transform.rotation = Quaternion.Euler(euler);
            cam.fieldOfView = fov;
            cam.farClipPlane = 5000f;
            cam.nearClipPlane = 0.1f;

            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
        }

        // ---------------------------------------------------------------- menu 4 / 5

        [MenuItem("Tools/Latar Belakang/4. Pasang Collision & Play Area", false, 4)]
        public static void SetupCollision()
        {
            var root = GameObject.Find("LatarBelakang");
            if (root == null)
            {
                Debug.LogError("[LatarBelakang] Root 'LatarBelakang' tidak ada di scene. Jalankan menu 2 dulu.");
                return;
            }

            EnsureLayer("Ground", 8);
            EnsureLayer("Water", 9);
            EnsureLayer("Barrier", 10);
            int groundLayer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
            int waterLayer = Mathf.Max(0, LayerMask.NameToLayer("Water"));
            int barrierLayer = Mathf.Max(0, LayerMask.NameToLayer("Barrier"));

            // model collision: tanpa material, tanpa collider otomatis
            var mi = AssetImporter.GetAtPath(COL_MODEL) as ModelImporter;
            if (mi == null)
            {
                Debug.LogError("[LatarBelakang] " + COL_MODEL + " tidak ketemu.");
                return;
            }
            mi.globalScale = 1f;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.addCollider = false;
            mi.isReadable = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.SaveAndReimport();

            var old = root.transform.Find("Collision");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // bersihkan collider air / sisa percobaan sebelumnya di ENV
            var envRoot = root.transform.Find("ENV_ModulSawah");
            if (envRoot != null)
            {
                foreach (var wv in envRoot.GetComponentsInChildren<WaterVolume>(true)) Object.DestroyImmediate(wv);
                foreach (var mc in envRoot.GetComponentsInChildren<MeshCollider>(true)) Object.DestroyImmediate(mc);
            }

            var colRoot = new GameObject("Collision");
            colRoot.transform.SetParent(root.transform, false);

            // instantiate model collision apa adanya (jaga transform node FBX) lalu buang renderer-nya
            var colModel = AssetDatabase.LoadAssetAtPath<GameObject>(COL_MODEL);
            if (colModel == null)
            {
                Debug.LogError("[LatarBelakang] " + COL_MODEL + " gagal dimuat.");
                return;
            }

            var colInst = (GameObject)PrefabUtility.InstantiatePrefab(colModel, colRoot.transform);
            colInst.transform.localPosition = Vector3.zero;
            // rotation & scale dibiarkan seperti hasil import FBX (unit conversion 0.01 + node scale 100)
            Debug.Log(string.Format("[LatarBelakang] COL root scale={0} rot={1} pos={2}",
                colInst.transform.localScale, colInst.transform.localEulerAngles, colInst.transform.localPosition));

            Bounds bounds = new Bounds(new Vector3(-4f, 25f, -2f), new Vector3(1032f, 98f, 876f));
            bool first = true;
            int meshCount = 0;

            foreach (var r in colInst.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            foreach (var mf in colInst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var go = mf.gameObject;
                go.layer = groundLayer;

                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;

                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                var wb = mf.GetComponent<MeshRenderer>() != null ? mf.GetComponent<MeshRenderer>().bounds : new Bounds(go.transform.position, Vector3.one);
                if (first) { bounds = wb; first = false; }
                else bounds.Encapsulate(wb);
                meshCount++;
            }

            Vector3 bmin = bounds.min;
            Vector3 bmax = bounds.max;
            float cx = (bmin.x + bmax.x) * 0.5f;
            float cz = (bmin.z + bmax.z) * 0.5f;
            float w = bmax.x - bmin.x;
            float d = bmax.z - bmin.z;
            const float netY = -60f;
            float wallH = 160f;

            var net = CreateBox("SafetyNet", colRoot.transform,
                new Vector3(cx, netY, cz), new Vector3(w + 800f, 4f, d + 800f), barrierLayer);
            CreateBox("Boundary_N", colRoot.transform, new Vector3(cx, netY + wallH * 0.5f, bmax.z), new Vector3(w + 200f, wallH, 4f), barrierLayer);
            CreateBox("Boundary_S", colRoot.transform, new Vector3(cx, netY + wallH * 0.5f, bmin.z), new Vector3(w + 200f, wallH, 4f), barrierLayer);
            CreateBox("Boundary_E", colRoot.transform, new Vector3(bmax.x, netY + wallH * 0.5f, cz), new Vector3(4f, wallH, d + 200f), barrierLayer);
            CreateBox("Boundary_W", colRoot.transform, new Vector3(bmin.x, netY + wallH * 0.5f, cz), new Vector3(4f, wallH, d + 200f), barrierLayer);
            net.isTrigger = false;

            // ---- permukaan sawah/tanah (ENV) yang tidak tercakup COL_Terrain
            SetupEnvGroundColliders(colRoot.transform, groundLayer);

            // ---- dinding vertikal antar teras sawah (biar tidak masuk ke rongga bawah plat)
            SetupTerraceWalls(colRoot.transform, groundLayer);

            // ---- skirt tanah yang terlihat, menutup celah pandangan ke rongga bawah plat
            SetupTerraceSkirts(colRoot.transform);

            // ---- collider rumah kampung (biar tidak bisa ditembus)
            SetupPopulationColliders(colRoot.transform, barrierLayer);

            // ---- air: BoxCollider trigger (MeshCollider concave tidak boleh trigger)
            int waterCount = 0;
            if (envRoot != null)
            {
                foreach (var mr in envRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = mr.name.ToLowerInvariant();
                    // Sawah_Air_Petak dilewati: mesh-nya menutupi seluruh petak (bounds terlalu besar
                    // untuk trigger), jadi hanya badan air sungai/air terjun yang dijadikan volume.
                    bool isWater = n.Contains("water");
                    if (!isWater) continue;

                    var b = mr.bounds;
                    // tebal minimal 2 m untuk permukaan air yang tipis (sungai)
                    float thickness = Mathf.Max(2f, b.size.y);
                    var go = new GameObject("Water_" + mr.name);
                    go.transform.SetParent(colRoot.transform, false);
                    go.transform.position = new Vector3(b.center.x, b.center.y, b.center.z);
                    go.layer = waterLayer;

                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(b.size.x, thickness, b.size.z);
                    bc.isTrigger = true;

                    var wv = go.AddComponent<WaterVolume>();
                    wv.surfaceY = b.center.y + thickness * 0.5f;

                    waterCount++;
                }
            }

            // ---- spawn point di tanah
            Physics.SyncTransforms();
            var spawn = GameObject.Find("SpawnPoint");
            if (spawn == null) spawn = new GameObject("SpawnPoint");
            Vector3 spawnXZ = new Vector3(0f, 0f, -140f);
            Vector3 spawnPos = spawnXZ;
            int mask = 1 << groundLayer;
            if (Physics.Raycast(new Vector3(spawnXZ.x, 500f, spawnXZ.z), Vector3.down, out RaycastHit hit, 1200f, mask, QueryTriggerInteraction.Ignore))
                spawnPos = hit.point + Vector3.up * 0.05f;
            spawn.transform.position = spawnPos;

            // Collider sawah baru saja dibuat/diperbarui. Snap ulang karakter
            // AsetTambahan supaya tidak tertimbun di bawah permukaan baru.
            AsetTambahanSetup.SnapAsetTambahanToGround();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log(string.Format("[LatarBelakang] Collision dipasang: {0} mesh ({1:F0} m x {2:F0} m, y {3:F1}..{4:F1}), {5} surface air, spawn di {6}",
                meshCount, w, d, bmin.y, bmax.y, waterCount, spawnPos));
        }

        /// <summary>
        /// Plat sawah bertingkat tidak punya dinding vertikal, jadi player bisa masuk ke rongga
        /// di bawah plat lalu jatuh ke tanah dasar. Buat dinding dari heightmap 1 m pada semua
        /// step yang lebih tinggi dari stepOffset CharacterController.
        /// </summary>
        static void SetupTerraceWalls(Transform colRoot, int groundLayer)
        {
            var old = colRoot.Find("Terrace_Walls");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            const float xMin = -205f, xMax = 205f, zMin = -130f, zMax = 130f;
            const float cell = 1f;
            const float minStep = 0.45f;
            const float topPad = 0.05f;

            int nx = Mathf.RoundToInt((xMax - xMin) / cell) + 1;
            int nz = Mathf.RoundToInt((zMax - zMin) / cell) + 1;
            var heights = new float[nx, nz];
            var valid = new bool[nx, nz];

            Physics.SyncTransforms();
            int mask = 1 << groundLayer;
            for (int ix = 0; ix < nx; ix++)
            {
                float x = xMin + ix * cell;
                for (int iz = 0; iz < nz; iz++)
                {
                    float z = zMin + iz * cell;
                    if (Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out RaycastHit hit, 600f, mask, QueryTriggerInteraction.Ignore))
                    {
                        valid[ix, iz] = true;
                        heights[ix, iz] = hit.point.y;
                    }
                }
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();
            int quads = 0;

            for (int ix = 0; ix < nx; ix++)
            {
                float x = xMin + ix * cell;
                for (int iz = 0; iz < nz; iz++)
                {
                    if (!valid[ix, iz]) continue;
                    float z = zMin + iz * cell;
                    float h0 = heights[ix, iz];

                    if (ix + 1 < nx && valid[ix + 1, iz])
                    {
                        float h1 = heights[ix + 1, iz];
                        if (Mathf.Abs(h1 - h0) >= minStep)
                        {
                            float wx = x + cell * 0.5f;
                            AddWallQuad(verts, tris,
                                new Vector3(wx, 0f, z - cell * 0.5f),
                                new Vector3(wx, 0f, z + cell * 0.5f),
                                Mathf.Min(h0, h1), Mathf.Max(h0, h1) + topPad);
                            quads++;
                        }
                    }

                    if (iz + 1 < nz && valid[ix, iz + 1])
                    {
                        float h1 = heights[ix, iz + 1];
                        if (Mathf.Abs(h1 - h0) >= minStep)
                        {
                            float wz = z + cell * 0.5f;
                            AddWallQuad(verts, tris,
                                new Vector3(x - cell * 0.5f, 0f, wz),
                                new Vector3(x + cell * 0.5f, 0f, wz),
                                Mathf.Min(h0, h1), Mathf.Max(h0, h1) + topPad);
                            quads++;
                        }
                    }
                }
            }

            string meshFolder = ROOT + "/Meshes";
            string meshPath = meshFolder + "/COL_TerraceWalls.asset";
            EnsureFolder(meshFolder);
            AssetDatabase.DeleteAsset(meshPath);

            var mesh = new Mesh();
            mesh.name = "COL_TerraceWalls";
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("Terrace_Walls");
            go.layer = groundLayer;
            go.transform.SetParent(colRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            Physics.SyncTransforms();
            Debug.Log(string.Format("[LatarBelakang] Dinding teras sawah: {0} quad dari {1} titik sampel", quads, nx * nz));
        }

        static void AddWallQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, float bottom, float top)
        {
            int i = verts.Count;
            verts.Add(new Vector3(a.x, bottom, a.z));
            verts.Add(new Vector3(b.x, bottom, b.z));
            verts.Add(new Vector3(a.x, top, a.z));
            verts.Add(new Vector3(b.x, top, b.z));
            // dua sisi: collider mesh Unity satu arah, jadi segitiga dibuat bolak-balik
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i + 1); tris.Add(i + 3); tris.Add(i + 2);
        }

        /// <summary>
        /// Tutup celah pandangan ke rongga bawah plat sawah dengan "skirt" tanah:
        /// setiap tepi batas mesh permukaan padi/tanah ditarik vertikal ke bawah.
        /// Tepi diambil dari geometry asli (bukan grid), jadi tidak ada celah tipis tersisa.
        /// </summary>
        static void SetupTerraceSkirts(Transform colRoot)
        {
            var visualRoot = colRoot.parent != null ? colRoot.parent : colRoot;
            var old = visualRoot.Find("Terrace_Skirt_Visual");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            string envPrefabPath = PREFABS + "/ENV_ModulSawah.prefab";
            var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(envPrefabPath);
            if (envPrefab == null) return;

            string[] surfaces =
            {
                "Padi_Hamparan", "Pematang_Petak", "Sawah_Air_Petak", "Tanah_Teras",
                "Petak_Timur", "Lahan_Utara", "Jalur_Tanah", "Jalur_Utara",
                "Jalur_Masuk", "Jalan_Grup_1", "Jalan_Grup_2", "Jalan_Grup_3", "Jalan_Grup_4",
                "Kebun_Pekarangan", "Pekarangan", "Taman_Pekarangan"
            };

            const float baseY = -8f;
            const float topPad = 0.02f;
            const float lipWidth = 1.2f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int skirtEdges = 0;

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);

            foreach (var mr in temp.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (System.Array.IndexOf(surfaces, mr.name) < 0) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;

                var mesh = mf.sharedMesh;
                var local = mesh.vertices;
                var meshTris = mesh.triangles;
                if (local.Length == 0 || meshTris.Length < 3) continue;

                var world = new Vector3[local.Length];
                for (int i = 0; i < local.Length; i++)
                    world[i] = mr.transform.TransformPoint(local[i]);

                // weld vertex berdasarkan posisi supaya tepi yang sama tidak dihitung dobel
                var weldMap = new Dictionary<Vector3Int, int>();
                var weld = new int[local.Length];
                for (int i = 0; i < local.Length; i++)
                {
                    var key = new Vector3Int(
                        Mathf.RoundToInt(world[i].x * 100f),
                        Mathf.RoundToInt(world[i].y * 100f),
                        Mathf.RoundToInt(world[i].z * 100f));
                    int idx;
                    if (!weldMap.TryGetValue(key, out idx))
                    {
                        idx = weldMap.Count;
                        weldMap[key] = idx;
                    }
                    weld[i] = idx;
                }

                var edgeCount = new Dictionary<long, int>();
                var edgeCentroid = new Dictionary<long, Vector3>();
                for (int t = 0; t + 2 < meshTris.Length; t += 3)
                {
                    int a = weld[meshTris[t]];
                    int b = weld[meshTris[t + 1]];
                    int c = weld[meshTris[t + 2]];
                    Vector3 wa = world[meshTris[t]];
                    Vector3 wb = world[meshTris[t + 1]];
                    Vector3 wc = world[meshTris[t + 2]];
                    Vector3 centroid = (wa + wb + wc) / 3f;
                    CountEdge(edgeCount, edgeCentroid, a, b, centroid);
                    CountEdge(edgeCount, edgeCentroid, b, c, centroid);
                    CountEdge(edgeCount, edgeCentroid, c, a, centroid);
                }

                var weldPos = new Vector3[weldMap.Count];
                for (int i = 0; i < local.Length; i++) weldPos[weld[i]] = world[i];

                foreach (var kv in edgeCount)
                {
                    if (kv.Value != 1) continue; // hanya tepi batas (dipakai 1 segitiga)
                    int a = (int)(kv.Key >> 32);
                    int b = (int)(kv.Key & 0xFFFFFFFFL);
                    Vector3 pa = weldPos[a];
                    Vector3 pb = weldPos[b];
                    if (Mathf.Abs(pa.y - pb.y) > 0.3f) continue; // hanya tepi yang mendatar

                    Vector3 centroid;
                    if (!edgeCentroid.TryGetValue(kv.Key, out centroid)) centroid = (pa + pb) * 0.5f - Vector3.forward;
                    Vector3 outward = (pa + pb) * 0.5f - centroid;
                    outward.y = 0f;
                    if (outward.sqrMagnitude < 1e-6f) continue;
                    outward.Normalize();

                    // bibir horizontal: menutup retakan tipis antara dua mesh
                    Vector3 pa2 = pa + outward * lipWidth;
                    Vector3 pb2 = pb + outward * lipWidth;
                    pa2.y = pa.y;
                    pb2.y = pb.y;
                    AddLipQuad(verts, tris, pa, pb, pa2, pb2);

                    // dinding vertikal dari ujung bibir ke bawah
                    AddSkirtQuad(verts, tris, pa2, pb2, pa.y + topPad, pb.y + topPad, baseY);
                    skirtEdges++;
                }
            }

            Object.DestroyImmediate(temp);

            if (verts.Count == 0)
            {
                Debug.LogWarning("[LatarBelakang] Skirt teras: tidak ada tepi yang bisa dibuat (mesh tidak readable?).");
                return;
            }

            string meshFolder = ROOT + "/Meshes";
            EnsureFolder(meshFolder);
            string meshPath = meshFolder + "/TerraceSkirt_Visual.asset";
            AssetDatabase.DeleteAsset(meshPath);

            var skirtMesh = new Mesh();
            skirtMesh.name = "TerraceSkirt_Visual";
            skirtMesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            skirtMesh.SetVertices(verts);
            skirtMesh.SetTriangles(tris, 0);
            skirtMesh.RecalculateNormals();
            skirtMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(skirtMesh, meshPath);

            var go = new GameObject("Terrace_Skirt_Visual");
            go.transform.SetParent(visualRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = skirtMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FindSoilMaterial();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            Debug.Log(string.Format("[LatarBelakang] Skirt teras: {0} tepi, {1} vertex", skirtEdges, verts.Count));
        }

        static void CountEdge(Dictionary<long, int> dict, Dictionary<long, Vector3> centroids, int a, int b, Vector3 centroid)
        {
            if (a == b) return;
            int lo = Mathf.Min(a, b);
            int hi = Mathf.Max(a, b);
            long key = ((long)lo << 32) | (uint)hi;
            int count;
            if (dict.TryGetValue(key, out count))
            {
                dict[key] = count + 1;
            }
            else
            {
                dict[key] = 1;
                centroids[key] = centroid;
            }
        }

        static void AddLipQuad(List<Vector3> verts, List<int> tris, Vector3 pa, Vector3 pb, Vector3 pa2, Vector3 pb2)
        {
            int i = verts.Count;
            verts.Add(pa); verts.Add(pb); verts.Add(pa2); verts.Add(pb2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);

            int j = verts.Count;
            verts.Add(pa); verts.Add(pb); verts.Add(pa2); verts.Add(pb2);
            tris.Add(j); tris.Add(j + 1); tris.Add(j + 2);
            tris.Add(j + 1); tris.Add(j + 3); tris.Add(j + 2);
        }

        static void AddSkirtQuad(List<Vector3> verts, List<int> tris, Vector3 pa, Vector3 pb, float topA, float topB, float baseY)
        {
            // sisi depan
            int i = verts.Count;
            verts.Add(new Vector3(pa.x, baseY, pa.z));
            verts.Add(new Vector3(pb.x, baseY, pb.z));
            verts.Add(new Vector3(pa.x, topA, pa.z));
            verts.Add(new Vector3(pb.x, topB, pb.z));
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);

            // sisi belakang (vertex terpisah supaya normal sisi ini benar)
            int j = verts.Count;
            verts.Add(new Vector3(pa.x, baseY, pa.z));
            verts.Add(new Vector3(pb.x, baseY, pb.z));
            verts.Add(new Vector3(pa.x, topA, pa.z));
            verts.Add(new Vector3(pb.x, topB, pb.z));
            tris.Add(j); tris.Add(j + 1); tris.Add(j + 2);
            tris.Add(j + 1); tris.Add(j + 3); tris.Add(j + 2);
        }

        /// <summary>Ambil material tanah dari ENV; kalau tidak ada, buat material cadangan.</summary>
        static Material FindSoilMaterial()
        {
            var env = GameObject.Find("LatarBelakang/ENV_ModulSawah");
            if (env != null)
            {
                var preferred = new[] { "Tanah_Teras", "Pematang_Petak" };
                foreach (var name in preferred)
                {
                    foreach (var mr in env.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (mr.name == name && mr.sharedMaterial != null) return mr.sharedMaterial;
                    }
                }
            }

            string path = ROOT + "/Materials/M_TerraceWall.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureFolder(ROOT + "/Materials");
                mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.373f, 0.283f, 0.196f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        /// <summary>
        /// ENV (tanah/sawah yang terlihat) tidak ada di COL_Terrain, jadi alas padi bisa ditembus.
        /// Buat MeshCollider dari mesh permukaan ENV supaya player berjalan di permukaan yang terlihat.
        /// </summary>
        static void SetupEnvGroundColliders(Transform colRoot, int groundLayer)
        {
            var old = colRoot.Find("ENV_Ground");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            string envPrefabPath = PREFABS + "/ENV_ModulSawah.prefab";
            var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(envPrefabPath);
            if (envPrefab == null)
            {
                Debug.LogWarning("[LatarBelakang] " + envPrefabPath + " tidak ada, collider permukaan dilewati.");
                return;
            }

            string[] surfaces =
            {
                "Padi_Hamparan", "Pematang_Petak", "Tanah_Teras", "Sawah_Air_Petak",
                "Petak_Timur", "Lahan_Utara", "Jalur_Tanah", "Jalur_Utara", "Jalur_Masuk",
                "Jalan_Grup_1", "Jalan_Grup_2", "Jalan_Grup_3", "Jalan_Grup_4",
                "Kebun_Pekarangan", "Pekarangan", "Taman_Pekarangan"
            };

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
            var group = new GameObject("ENV_Ground");
            group.transform.SetParent(colRoot, false);

            int count = 0;
            foreach (var mr in temp.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (System.Array.IndexOf(surfaces, mr.name) < 0) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var go = new GameObject("COL_" + mr.name);
                go.layer = groundLayer;
                go.transform.SetParent(group.transform, false);
                go.transform.position = mr.transform.position;
                go.transform.rotation = mr.transform.rotation;
                go.transform.localScale = mr.transform.lossyScale;

                var nmf = go.AddComponent<MeshFilter>();
                nmf.sharedMesh = mf.sharedMesh;
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                count++;
            }

            Object.DestroyImmediate(temp);
            Physics.SyncTransforms();
            Debug.Log("[LatarBelakang] Collider permukaan sawah: " + count + " mesh");
        }

        /// <summary>
        /// Rumah kampung (POP) belum punya collider. Buat MeshCollider dari mesh aslinya
        /// di layer Barrier supaya player tidak bisa menembus dinding/atap rumah.
        /// </summary>
        static void SetupPopulationColliders(Transform colRoot, int barrierLayer)
        {
            var old = colRoot.Find("POP_Collision");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            string popPath = PREFABS + "/POP_RumahKampung.prefab";
            var popPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(popPath);
            if (popPrefab == null)
            {
                Debug.LogWarning("[LatarBelakang] " + popPath + " tidak ada, collider rumah dilewati.");
                return;
            }

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(popPrefab);
            var group = new GameObject("POP_Collision");
            group.transform.SetParent(colRoot, false);

            int count = 0;
            foreach (var mf in temp.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;

                var go = new GameObject("COL_" + mf.name);
                go.layer = barrierLayer;
                go.transform.SetParent(group.transform, false);
                go.transform.position = mf.transform.position;
                go.transform.rotation = mf.transform.rotation;
                go.transform.localScale = mf.transform.lossyScale;

                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                count++;
            }

            Object.DestroyImmediate(temp);

            // bangunan kecil di ENV (lumbung padi & saung)
            string envPath = PREFABS + "/ENV_ModulSawah.prefab";
            var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(envPath);
            if (envPrefab != null)
            {
                var envTemp = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
                string[] buildings = { "Lumbung", "Saung" };
                foreach (var mf in envTemp.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (System.Array.IndexOf(buildings, mf.name) < 0) continue;
                    if (mf.sharedMesh == null) continue;

                    var go = new GameObject("COL_" + mf.name);
                    go.layer = barrierLayer;
                    go.transform.SetParent(group.transform, false);
                    go.transform.position = mf.transform.position;
                    go.transform.rotation = mf.transform.rotation;
                    go.transform.localScale = mf.transform.lossyScale;

                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    count++;
                }
                Object.DestroyImmediate(envTemp);
            }

            Physics.SyncTransforms();
            Debug.Log("[LatarBelakang] Collider rumah: " + count + " mesh (layer Barrier)");
        }

        static BoxCollider CreateBox(string name, Transform parent, Vector3 center, Vector3 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.layer = layer;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.isTrigger = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return bc;
        }

        [MenuItem("Tools/Latar Belakang/5. Verifikasi Collision", false, 5)]
        public static void VerifyCollision()
        {
            int layer = LayerMask.NameToLayer("Ground");
            int mask = layer >= 0 ? 1 << layer : ~0;

            Physics.SyncTransforms();

            int total = 0, hitCount = 0;
            float minY = float.MaxValue, maxY = float.MinValue;
            var miss = new List<string>();

            for (float x = -510f; x <= 510f; x += 20f)
            {
                for (float z = -430f; z <= 430f; z += 20f)
                {
                    total++;
                    if (Physics.Raycast(new Vector3(x, 500f, z), Vector3.down, out RaycastHit h, 1200f, mask, QueryTriggerInteraction.Ignore))
                    {
                        hitCount++;
                        minY = Mathf.Min(minY, h.point.y);
                        maxY = Mathf.Max(maxY, h.point.y);
                    }
                    else if (miss.Count < 12)
                    {
                        miss.Add(string.Format("({0},{1})", x, z));
                    }
                }
            }

            Debug.Log(string.Format("[LatarBelakang] VERIFIKASI COLLISION: {0}/{1} titik ({2:F1}%) ada tanah, y {3:F1}..{4:F1}",
                hitCount, total, 100f * hitCount / Mathf.Max(1, total), minY, maxY));
            if (miss.Count > 0)
                Debug.Log("[LatarBelakang] Titik tanpa tanah: " + string.Join(" ", miss));
        }

        internal static void EnsureLayer(string name, int index)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return;
            if (index >= layers.arraySize) return;
            layers.GetArrayElementAtIndex(index).stringValue = name;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    /// <summary>Diagnostik ukuran object yang tidak wajar.</summary>
    public static class LatarBelakangDiagnostics
    {
        [MenuItem("Tools/Latar Belakang/8. Test Lighting", false, 80)]
        public static void TestLighting()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../opencode-lb-shots"));
            Directory.CreateDirectory(dir);

            var light = Object.FindObjectOfType<Light>();
            var cam = Camera.main;
            Debug.Log(string.Format("[LatarBelakang] LIGHT type={0} intensity={1} dir(world pos)= pos={2} rot={3}",
                light.type, light.intensity, light.transform.position, light.transform.eulerAngles));
            Vector3 fwd = light.transform.forward;
            Debug.Log("[LatarBelakang] LIGHT forward=" + fwd + " (sinar mengarah ke -forward)");
            Debug.Log(string.Format("[LatarBelakang] AMBIENT mode={0} color={1} intensity={2}",
                RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientIntensity));

            var shadows = light.shadows;
            var intensity = light.intensity;
            var ambient = RenderSettings.ambientLight;
            var ambInt = RenderSettings.ambientIntensity;

            LatarBelakangSetup.Shot(dir, "test_a_noshadow", cam.transform.position, cam.transform.eulerAngles, cam.fieldOfView);
            light.shadows = LightShadows.None;
            LatarBelakangSetup.Shot(dir, "test_b_noshadow", cam.transform.position, cam.transform.eulerAngles, cam.fieldOfView);

            light.intensity = 3f;
            LatarBelakangSetup.Shot(dir, "test_c_sun3", cam.transform.position, cam.transform.eulerAngles, cam.fieldOfView);

            light.intensity = 0f;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientIntensity = 1f;
            LatarBelakangSetup.Shot(dir, "test_d_ambientonly", cam.transform.position, cam.transform.eulerAngles, cam.fieldOfView);

            light.shadows = shadows;
            light.intensity = intensity;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientIntensity = ambInt;
        }

        [MenuItem("Tools/Latar Belakang/9. Diagnostik Object", false, 90)]
        public static void DiagnoseObjects()
        {
            var root = GameObject.Find("LatarBelakang");
            if (root == null) { Debug.LogWarning("[LatarBelakang] Root tidak ada."); return; }

            var list = new List<Renderer>(root.GetComponentsInChildren<Renderer>(true));
            list.Sort((a, b) => b.bounds.size.y.CompareTo(a.bounds.size.y));
            Debug.Log("[LatarBelakang] Renderer total: " + list.Count);
            for (int i = 0; i < Mathf.Min(10, list.Count); i++)
            {
                var r = list[i];
                Debug.Log(string.Format("[LatarBelakang] TINGGI {0} {1} size={2} pos={3} parent={4}",
                    i, r.name, r.bounds.size, r.transform.position, r.transform.parent ? r.transform.parent.name : "-"));
            }
        }
    }
}


