using System.Collections;
using UnityEngine;

namespace NullFrontier.Combat
{
    public class TacticalDash : Ability
    {
        public const string Id = "tactical_dash";
        public override string AbilityId => Id;

        [SerializeField] private float dashDistance = 6f;
        [SerializeField] private float dashDuration = 0.15f;

        private CharacterController _controller;
        private Coroutine _dash;

        private void Awake()
        {
            _controller = GetComponentInParent<CharacterController>();
        }

        public override bool Activate()
        {
            if (_controller == null) return false;
            if (_dash != null) return false;
            if (!base.Activate()) return false;

            // Third-person: the character root already faces the current move direction.
            _dash = StartCoroutine(DashRoutine(transform.forward));
            return true;
        }

        private IEnumerator DashRoutine(Vector3 direction)
        {
            float speed = dashDistance / Mathf.Max(0.01f, dashDuration);
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                if (_controller != null)
                    _controller.Move(direction * (speed * Time.deltaTime));
                elapsed += Time.deltaTime;
                yield return null;
            }
            _dash = null;
        }
    }
}
