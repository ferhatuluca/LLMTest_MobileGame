using UnityEngine;

namespace MonstersVsZombies.Units.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollowController : MonoBehaviour
    {
        [field: SerializeField] public Vector3 Offset { get; private set; }

        public UnitController Target { get; private set; }

        private void LateUpdate()
        {
            ApplyFollowPosition();
        }

        public bool Bind(UnitController target)
        {
            if (target == null)
            {
                return false;
            }

            Target = target;
            ApplyFollowPosition();
            return true;
        }

        public void Clear()
        {
            Target = null;
        }

        internal void ConfigureOffset(Vector3 offset)
        {
            Offset = offset;
        }

        private void ApplyFollowPosition()
        {
            if (Target != null && Target.IsActive)
            {
                transform.position = Target.transform.position + Offset;
            }
        }
    }
}
