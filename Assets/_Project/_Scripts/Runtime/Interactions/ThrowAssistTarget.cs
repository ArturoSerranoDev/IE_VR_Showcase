using System.Collections.Generic;
using UnityEngine;

namespace IE.VRShowcase.Interactions
{
    /// <summary>
    /// A target that thrown objects may be assisted toward. This component holds only the aim
    /// point and the on/off switch - all of the trajectory maths lives on the thrown object, in
    /// <see cref="ThrowAssistant"/>, so the XR rig needs no assist components at all.
    /// </summary>
    /// <remarks>
    /// Active targets register themselves in a static list. That is what lets balls spawned at
    /// runtime find the basket without anything having to wire up a reference to them.
    /// </remarks>
    [AddComponentMenu("IE Showcase/Interactions/Throw Assist Target")]
    public class ThrowAssistTarget : MonoBehaviour
    {
        static readonly List<ThrowAssistTarget> s_ActiveTargets = new List<ThrowAssistTarget>();

        [SerializeField]
        [Tooltip("Point thrown objects are aimed at. Defaults to this transform. Put it just " +
            "inside the basket rather than on the rim, so assisted throws drop in cleanly.")]
        Transform m_AimPoint;

        [SerializeField]
        [Tooltip("Whether throws are currently assisted toward this target.")]
        bool m_AssistEnabled = true;

        [SerializeField]
        [Tooltip("How steeply the object must be falling when it arrives, in degrees below " +
            "horizontal. A basket needs a real lob - an almost level arrival passes through the " +
            "centre on paper but clips the near rim. 0 accepts any arc.")]
        [Range(0f, 85f)]
        float m_MinDescentAngle = 35f;

        /// <summary>Every enabled target in the scene.</summary>
        public static IReadOnlyList<ThrowAssistTarget> activeTargets => s_ActiveTargets;

        /// <summary>The world point throws are aimed at.</summary>
        public Transform aimPoint => m_AimPoint != null ? m_AimPoint : transform;

        /// <summary>Whether throws are currently assisted toward this target.</summary>
        public bool assistEnabled
        {
            get => m_AssistEnabled;
            set => m_AssistEnabled = value;
        }

        /// <summary>
        /// How steeply, in degrees below horizontal, an object must be falling when it reaches
        /// this target. 0 accepts any arc.
        /// </summary>
        public float minDescentAngle
        {
            get => m_MinDescentAngle;
            set => m_MinDescentAngle = Mathf.Clamp(value, 0f, 85f);
        }

        void OnEnable()
        {
            if (!s_ActiveTargets.Contains(this))
                s_ActiveTargets.Add(this);
        }

        void OnDisable()
        {
            s_ActiveTargets.Remove(this);
        }

        /// <summary>
        /// Returns the enabled target that best matches a throw, i.e. the one sitting closest to
        /// the direction the object was actually thrown in.
        /// </summary>
        /// <param name="from">Where the throw started.</param>
        /// <param name="throwDirection">The direction the object was thrown in.</param>
        /// <returns>The best target, or <see langword="null"/> when none is enabled.</returns>
        public static ThrowAssistTarget FindBestTarget(Vector3 from, Vector3 throwDirection)
        {
            ThrowAssistTarget best = null;
            var bestAngle = float.MaxValue;

            for (var i = 0; i < s_ActiveTargets.Count; i++)
            {
                var candidate = s_ActiveTargets[i];
                if (candidate == null || !candidate.assistEnabled)
                    continue;

                var toTarget = candidate.aimPoint.position - from;
                if (toTarget.sqrMagnitude < 1e-6f)
                    continue;

                var angle = Vector3.Angle(throwDirection, toTarget);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                }
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = m_AssistEnabled ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(aimPoint.position, 0.06f);
        }
    }
}
