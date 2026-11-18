using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepStation
{
    /// <summary>
    /// Fits the camera to the station's width, scrolls it vertically by dragging (with a little
    /// fling) or with the mouse wheel, and forwards taps to StationView.
    /// </summary>
    [RequireComponent(typeof(StationView))]
    public class StationCamera : MonoBehaviour
    {
        Camera cam;
        StationView view;
        bool pressing, dragging;
        Vector2 downPosition, lastPosition;
        float velocity;

        void Start()
        {
            view = GetComponent<StationView>();
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(0f, 0f, -10f);
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(2, 6, 16, 255);

            FitWidth();
            // Start with the top floor just under the HUD bar.
            float viewTop = StationLayout.RoomHeight + 1.2f + cam.orthographicSize * 2f * 0.1f;
            SetY(viewTop - cam.orthographicSize);
        }

        void FitWidth()
        {
            cam.orthographicSize = Mathf.Max(StationLayout.ViewWidth * 0.5f / Mathf.Max(cam.aspect, 0.01f), 6f);
        }

        void SetY(float y)
        {
            float size = cam.orthographicSize;
            float max = StationLayout.SurfaceY + 0.6f - size;
            float min = StationLayout.DeepY + size;
            var p = cam.transform.position;
            cam.transform.position = new Vector3(0f, Mathf.Clamp(y, min, Mathf.Max(min, max)), p.z);
        }

        void Update()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            float y = cam.transform.position.y;

            var pointer = Pointer.current;
            if (pointer != null)
            {
                Vector2 position = pointer.position.ReadValue();
                if (pointer.press.wasPressedThisFrame)
                {
                    pressing = true;
                    dragging = false;
                    downPosition = lastPosition = position;
                    velocity = 0f;
                }
                else if (pressing && pointer.press.isPressed)
                {
                    float threshold = (Screen.dpi > 0f ? Screen.dpi : 160f) * 0.08f;
                    if (!dragging && (position - downPosition).sqrMagnitude > threshold * threshold)
                        dragging = true;
                    if (dragging)
                    {
                        float worldPerPixel = cam.orthographicSize * 2f / Screen.height;
                        float dy = (position.y - lastPosition.y) * worldPerPixel;
                        SetY(y - dy);
                        velocity = dt > 0f ? -dy / dt : 0f;
                    }
                    lastPosition = position;
                }

                if (pressing && pointer.press.wasReleasedThisFrame)
                {
                    pressing = false;
                    if (!dragging)
                        view.HandleTap(cam.ScreenToWorldPoint(lastPosition));
                }
            }

            if (!pressing && Mathf.Abs(velocity) > 0.05f)
            {
                SetY(cam.transform.position.y + velocity * dt);
                velocity *= Mathf.Exp(-5f * dt);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    SetY(cam.transform.position.y + Mathf.Sign(scroll) * 1.2f);
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float before = cam.orthographicSize;
            FitWidth();
            if (!Mathf.Approximately(before, cam.orthographicSize))
                SetY(cam.transform.position.y);
        }
    }
}
