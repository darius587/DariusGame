using System;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class CoinCollectible : MonoBehaviour
    {
        private bool collected;
        private LevelSession session;
        private Vector3 origin;
        public event Action Collected;
        private void Start()
        {
            session = FindAnyObjectByType<LevelSession>();
            origin = transform.position;
        }

        private void Update()
        {
            float offset = Mathf.Round(Mathf.Sin(Time.time * 3 + origin.x) * 2) / 16f;
            transform.position = origin + Vector3.up * offset;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || other.GetComponentInParent<PlayerFoundation>() == null) return;
            collected = true;
            if (session != null) session.CollectCoin();
            Collected?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
