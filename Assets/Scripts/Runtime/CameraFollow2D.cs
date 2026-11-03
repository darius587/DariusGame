using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target = null;
        [SerializeField] private Vector2 offset = new Vector2(2, 1.5f);
        [SerializeField] private Vector2 minimum = new Vector2(8, 2.5f);
        [SerializeField] private Vector2 maximum = new Vector2(31, 3.5f);
        [SerializeField] private Vector2 horizontalWorldBounds = new Vector2(-1, 40);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;
        private Vector3 velocity;
        private Camera view;
        private void Awake() => view = GetComponent<Camera>();
        private void LateUpdate()
        {
            if (target == null) return;
            float halfWidth = view.orthographicSize * view.aspect;
            float left = Mathf.Max(minimum.x, horizontalWorldBounds.x + halfWidth);
            float right = Mathf.Min(maximum.x, horizontalWorldBounds.y - halfWidth);
            if (right < left) right = left;
            Vector3 desired = new Vector3(
                Mathf.Clamp(target.position.x + offset.x, left, right),
                Mathf.Clamp(target.position.y + offset.y, minimum.y, maximum.y), -10);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
