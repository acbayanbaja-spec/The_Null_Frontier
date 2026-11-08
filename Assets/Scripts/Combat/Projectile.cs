using NullFrontier.Core;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 40f;
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private float hitRadius = 0.15f;

        public ObjectPool Pool { get; set; }
        public GameObject Owner { get; set; }

        private DamageInfo _payload;
        private Vector3 _direction;
        private bool _launched;
        private float _age;

        public void Launch(DamageInfo payload, Vector3 direction)
        {
            _payload = payload;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            _launched = true;
            _age = 0f;
            transform.rotation = Quaternion.LookRotation(_direction);
        }

        private void Update()
        {
            if (!_launched) return;

            float step = speed * Time.deltaTime;
            if (Physics.SphereCast(transform.position, hitRadius, _direction, out RaycastHit hit,
                    step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                HandleHit(hit.collider, hit.point);
            }
            else
            {
                transform.position += _direction * step;
            }

            _age += Time.deltaTime;
            if (_age >= lifetime) Despawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_launched) HandleHit(other, transform.position);
        }

        private void HandleHit(Collider collider, Vector3 point)
        {
            if (Owner != null && collider.transform.IsChildOf(Owner.transform)) return;

            var damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                _payload.Point = point;
                damageable.TakeDamage(_payload);
            }
            Despawn();
        }

        private void Despawn()
        {
            _launched = false;
            _direction = Vector3.zero;
            if (Pool != null)
            {
                Pool.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
