using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class MovingPlatform : MonoBehaviour
    {
        [SerializeField] private Vector2 start = Vector2.zero;
        [SerializeField] private Vector2 end = Vector2.right * 3;
        [SerializeField, Min(0.1f)] private float speed = 1.4f;
        private Rigidbody2D body;
        private float distance;
        private float progress;
        public Vector2 StepVelocity { get; private set; }
        private void Awake() { body = GetComponent<Rigidbody2D>(); distance = Vector2.Distance(start, end); }
        private void FixedUpdate()
        {
            if (distance < 0.001f) { StepVelocity = Vector2.zero; return; }
            progress += speed * Time.fixedDeltaTime;
            Vector2 target = Vector2.Lerp(start, end, Mathf.PingPong(progress, distance) / distance);
            StepVelocity = (target - body.position) / Time.fixedDeltaTime;
            body.MovePosition(target);
        }
    }
}
