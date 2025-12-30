using UnityEngine;

namespace RuneRelic.UI
{
    public static class MainMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindObjectOfType<MainMenu>() != null)
            {
                return;
            }

            var canvas = GameObject.Find("MainMenuCanvas");
            if (canvas == null)
            {
                return;
            }

            canvas.AddComponent<MainMenu>();
        }
    }
}
