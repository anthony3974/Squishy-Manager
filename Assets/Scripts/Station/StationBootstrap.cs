using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace DeepStation
{
    /// <summary>
    /// Spawns the station when the first scene loads, so pressing Play in any scene
    /// runs the prototype with no scene setup.
    /// </summary>
    public static class StationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<Station>() != null) return;

            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(2, 5, 12, 255);
            }

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var go = new GameObject("Deep-Sea Station");
            go.AddComponent<Station>();
            go.AddComponent<StationUI>();
        }
    }
}
