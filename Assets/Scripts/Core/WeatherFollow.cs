using UnityEngine;

namespace NullFrontier.Core
{
    /// <summary>
    /// Slides a world-space weather volume along with the operative so the storm never
    /// runs out overhead. The particles themselves are simulated in world space, which is
    /// what keeps this cheap: re-centring the emitter does not drag the rain that is
    /// already falling, so the city stays wet while the player moves through it.
    /// </summary>
    public class WeatherFollow : MonoBehaviour
    {
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private Vector3 offset = new Vector3(0f, 18f, 0f);
        [SerializeField] private float recenterDistance = 4f;

        private Transform _target;
        private Vector3 _origin;

        private void Start()
        {
            _origin = transform.position;
            FindTarget();
        }

        private void LateUpdate()
        {
            if (_target == null && !FindTarget()) return;

            Vector3 want = _target.position + offset;
            want.y = _origin.y;

            if (Vector3.SqrMagnitude(XZ(want - _origin)) < recenterDistance * recenterDistance) return;
            _origin = want;
            transform.position = want;
        }

        /// <summary>The player prefab is instantiated after the scene starts, so retry.</summary>
        private bool FindTarget()
        {
            var found = GameObject.FindGameObjectWithTag(targetTag);
            if (found == null) return false;
            _target = found.transform;
            return true;
        }

        private static Vector3 XZ(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
