using UnityEngine;

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

            var go = new GameObject("Deep-Sea Station");
            go.AddComponent<Station>();
            go.AddComponent<StationView>();
            go.AddComponent<StationCamera>();
            go.AddComponent<StationHud>();
            go.AddComponent<BuildMenu>();
        }
    }
}
