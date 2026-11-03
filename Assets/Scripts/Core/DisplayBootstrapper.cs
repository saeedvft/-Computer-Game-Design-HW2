using UnityEngine;

namespace HW2
{
    /// <summary>
    /// Borderless fullscreen sizes the window to the display but keeps rendering the backbuffer at the
    /// player's stored resolution, so a 1920x1080 desktop shows an upscaled 1024x768 or 1280x800 image
    /// and the whole scene turns soft. This keeps the fullscreen backbuffer exactly the display size,
    /// including after the player toggles fullscreen with Alt+Enter.
    /// </summary>
    public static class DisplayBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // The editor Game view reports Screen.fullScreen as true while being only a few hundred
            // pixels tall, so re-asserting the display size there would be wrong.
            if (Application.isEditor)
            {
                return;
            }

            GameObject go = new("DisplayBootstrapper");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<FullscreenResolutionWatcher>();
        }

        sealed class FullscreenResolutionWatcher : MonoBehaviour
        {
            Vector2Int applied;

            void Update()
            {
                if (!Screen.fullScreen)
                {
                    applied = Vector2Int.zero;
                    return;
                }

                DisplayInfo display = Screen.mainWindowDisplayInfo;
                if (display.width <= 0 || display.height <= 0)
                {
                    return;
                }

                Vector2Int target = new(display.width, display.height);
                if (Screen.width == target.x && Screen.height == target.y)
                {
                    applied = target;
                    return;
                }

                // A request that has not landed yet must not be re-issued every frame.
                if (applied == target)
                {
                    return;
                }

                applied = target;
                Screen.SetResolution(target.x, target.y, FullScreenMode.FullScreenWindow);
                Debug.Log($"HW2: fullscreen backbuffer set to {target.x}x{target.y}.");
            }
        }
    }
}
