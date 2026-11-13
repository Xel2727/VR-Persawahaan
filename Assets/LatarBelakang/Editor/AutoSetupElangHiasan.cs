using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LatarBelakang.EditorTools
{
    /// <summary>
    /// Menjalankan pemasangan elang + hiasan perumahan satu kali setelah
    /// script selesai dikompilasi, supaya scene langsung terisi tanpa klik menu.
    /// Menu 10/11 tetap bisa dipakai untuk memasang ulang.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoSetupElangHiasan
    {
        const string KEY = "RTRM.ElangHiasanSetupDone";
        const string SCENE = "SampleScene";

        static AutoSetupElangHiasan()
        {
            EditorApplication.delayCall += RunOnce;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.name == SCENE) EditorApplication.delayCall += RunOnce;
        }

        static void RunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunOnce;
                return;
            }

            if (EditorPrefs.GetBool(KEY, false)) return;
            if (SceneManager.GetActiveScene().name != SCENE) return;

            bool hasEagles = GameObject.Find("Elang_Langit") != null;
            bool hasDecor = GameObject.Find("Hiasan_Perumahan") != null;

            if (hasEagles && hasDecor)
            {
                EditorPrefs.SetBool(KEY, true);
                return;
            }

            try
            {
                if (!hasEagles) ElangSetup.Setup();
                if (!hasDecor) PerumahanHiasanSetup.Setup();
                Debug.Log("[AutoSetup] Elang & hiasan perumahan selesai dipasang.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AutoSetup] Gagal memasang elang/hiasan: " + e);
            }
            finally
            {
                EditorPrefs.SetBool(KEY, true);
            }
        }
    }
}
