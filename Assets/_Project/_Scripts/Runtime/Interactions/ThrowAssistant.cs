using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace IE.VRShowcase.Interactions
{
    /// <summary>
    /// Nudges a thrown object onto a <see cref="ThrowAssistTarget"/> when the player's throw was
    /// already close to a throw that would have landed. The player's throw *speed* is kept exactly
    /// as thrown; only the direction is rotated, and only when it is within
    /// <see cref="maxAssistAngle"/> of a solution. Throw wide and nothing happens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This lives on the interactable, not on the XR rig. XRI does have a built-in assist hook
    /// (<c>IXRAimAssist</c>), but <see cref="XRGrabInteractable"/> looks for it with
    /// <c>GetComponentInParent</c> on the <em>interactor</em>, which means it has to sit on the
    /// rig. Doing it here keeps the rig free of assist components.
    /// </para>
    /// <para>
    /// Timing matters. <see cref="XRGrabInteractable"/> does not apply the throw velocity when
    /// <c>selectExited</c> fires - it defers it to LateUpdate. So the velocity is not readable in
    /// the release handler. Instead the release arms this component and the correction happens on
    /// the next physics step, where the velocity is the release velocity and gravity has not been
    /// integrated into it yet.
    /// </para>
    /// <para>
    /// The solver assumes plain ballistic motion, so the Rigidbody must have gravity on and
    /// <c>linearDamping</c> at 0. With drag the projectile falls short of the solution.
    /// </para>
    /// </remarks>
    [AddComponentMenu("IE Showcase/Interactions/Throw Assistant")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ThrowAssistant : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("How far off the ideal throw the player may be and still be corrected. " +
            "Throws further off than this are left completely alone.")]
        [Range(0f, 90f)]
        float m_MaxAssistAngle = 14f;

        [SerializeField]
        [Tooltip("Releases slower than this are treated as putting the object down, not throwing it.")]
        float m_MinThrowSpeed = 1.2f;

        [SerializeField]
        [Tooltip("Physics steps to wait for a throw velocity to appear before giving up on a release.")]
        int m_MaxStepsToWaitForThrow = 6;

        [SerializeField]
        [Tooltip("Log each release and whether it was assisted. Useful while tuning the angle.")]
        bool m_LogAssistDecisions;

        Rigidbody m_Rigidbody;
        XRGrabInteractable m_Interactable;
        bool m_AwaitingThrow;
        int m_StepsWaited;

        /// <summary>How far off the ideal throw the player may be and still be corrected, in degrees.</summary>
        public float maxAssistAngle
        {
            get => m_MaxAssistAngle;
            set => m_MaxAssistAngle = Mathf.Clamp(value, 0f, 90f);
        }

        /// <summary>Releases slower than this are not treated as throws.</summary>
        public float minThrowSpeed
        {
            get => m_MinThrowSpeed;
            set => m_MinThrowSpeed = Mathf.Max(0f, value);
        }

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Interactable = GetComponent<XRGrabInteractable>();
        }

        void OnEnable()
        {
            m_Interactable.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            m_Interactable.selectExited.RemoveListener(OnReleased);
            m_AwaitingThrow = false;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            // A second hand may still be holding it; that is not a throw yet.
            if (m_Interactable.isSelected)
                return;

            m_AwaitingThrow = true;
            m_StepsWaited = 0;
        }

        void FixedUpdate()
        {
            if (!m_AwaitingThrow)
                return;

            var velocity = m_Rigidbody.linearVelocity;
            var speed = velocity.magnitude;

            if (speed < m_MinThrowSpeed)
            {
                // The throw velocity lands in LateUpdate, so it may not be here on the first step.
                if (++m_StepsWaited > m_MaxStepsToWaitForThrow)
                    m_AwaitingThrow = false;

                return;
            }

            m_AwaitingThrow = false;
            TryAssistThrow(velocity, speed);
        }

        void TryAssistThrow(Vector3 velocity, float speed)
        {
            var throwDirection = velocity / speed;
            var origin = m_Rigidbody.position;

            var target = ThrowAssistTarget.FindBestTarget(origin, throwDirection);
            if (target == null)
            {
                Log("no enabled target");
                return;
            }

            if (!TrySolveLaunchDirection(origin, target.aimPoint.position, speed, throwDirection,
                    out var idealDirection, target.minDescentAngle))
            {
                Log($"no usable arc at {speed:F2} m/s (too slow, or no arc arrives steeply enough)");
                return;
            }

            var errorAngle = Vector3.Angle(throwDirection, idealDirection);
            if (errorAngle > m_MaxAssistAngle)
            {
                Log($"off by {errorAngle:F1}deg, tolerance {m_MaxAssistAngle:F1}deg - left alone");
                return;
            }

            // Speed is preserved exactly; only the direction is corrected.
            m_Rigidbody.linearVelocity = idealDirection * speed;
            Log($"assisted: off by {errorAngle:F1}deg at {speed:F2} m/s -> {target.name}");
        }

        /// <summary>
        /// Solves for a launch direction that lands a ballistic projectile fired from
        /// <paramref name="from"/> at <paramref name="speed"/> onto <paramref name="to"/>.
        /// </summary>
        /// <param name="from">Launch position.</param>
        /// <param name="to">Target position.</param>
        /// <param name="speed">Launch speed, which the solution preserves.</param>
        /// <param name="preferredDirection">
        /// The direction actually thrown. Two arcs reach any reachable target - a flat one and a
        /// lobbed one - and this picks whichever is closer to how the player threw.
        /// </param>
        /// <param name="launchDirection">The solved unit direction.</param>
        /// <param name="minDescentAngle">
        /// Minimum angle below horizontal the projectile must be falling at when it arrives, in
        /// degrees. Use this for targets the projectile has to drop into, where an almost level
        /// arrival would clip the near edge. 0 accepts any arc.
        /// </param>
        /// <returns>
        /// <see langword="false"/> when the target cannot be reached at this speed, when no arc
        /// arrives steeply enough, or when the target is directly above or below the launch point,
        /// where there is no unique solution.
        /// </returns>
        public static bool TrySolveLaunchDirection(Vector3 from, Vector3 to, float speed,
            Vector3 preferredDirection, out Vector3 launchDirection, float minDescentAngle = 0f)
        {
            launchDirection = preferredDirection;

            var gravity = Physics.gravity;
            var gravityMagnitude = gravity.magnitude;
            if (gravityMagnitude < 1e-4f || speed < 1e-4f)
                return false;

            var up = -gravity / gravityMagnitude;
            var delta = to - from;

            // Split the offset into "along gravity" and "across gravity" parts, so this works
            // even if the project uses a non-standard gravity direction.
            var height = Vector3.Dot(delta, up);
            var horizontalOffset = delta - up * height;
            var distance = horizontalOffset.magnitude;
            if (distance < 1e-3f)
                return false;

            var horizontalDirection = horizontalOffset / distance;

            // With t = tan(theta):
            //   height = distance * t - k * (1 + t^2),  k = g * distance^2 / (2 * speed^2)
            // which rearranges to the quadratic  k*t^2 - distance*t + (height + k) = 0.
            var k = gravityMagnitude * distance * distance / (2f * speed * speed);
            var discriminant = distance * distance - 4f * k * (height + k);
            if (discriminant < 0f)
                return false;

            var root = Mathf.Sqrt(discriminant);
            var flatTangent = (distance - root) / (2f * k);
            var lobbedTangent = (distance + root) / (2f * k);

            var flat = DirectionFromTangent(horizontalDirection, up, flatTangent);
            var lobbed = DirectionFromTangent(horizontalDirection, up, lobbedTangent);

            // Both arcs pass through the target, but they arrive at very different angles, and for
            // anything the projectile must drop *into* that decides whether it actually goes in.
            // A shallow arrival reaches the rim from almost level and clips its near edge, even
            // though the arc technically passes through the centre. So when the target asks for a
            // minimum descent, keep only the arcs that satisfy it.
            if (minDescentAngle > 0f)
            {
                var flatOk = DescentAngleOnArrival(flatTangent, distance, speed, gravityMagnitude)
                    >= minDescentAngle;
                var lobbedOk = DescentAngleOnArrival(lobbedTangent, distance, speed, gravityMagnitude)
                    >= minDescentAngle;

                if (flatOk && !lobbedOk)
                {
                    launchDirection = flat;
                    return true;
                }

                if (lobbedOk && !flatOk)
                {
                    launchDirection = lobbed;
                    return true;
                }

                // Neither arc comes down steeply enough to drop in at this speed.
                if (!flatOk)
                    return false;
            }

            launchDirection = Vector3.Angle(preferredDirection, flat) <=
                              Vector3.Angle(preferredDirection, lobbed)
                ? flat
                : lobbed;

            return true;
        }

        /// <summary>
        /// The angle below horizontal at which a projectile launched at this angle arrives after
        /// covering <paramref name="distance"/> horizontally. Negative while still climbing.
        /// </summary>
        static float DescentAngleOnArrival(float tangent, float distance, float speed,
            float gravityMagnitude)
        {
            var angle = Mathf.Atan(tangent);
            var horizontalSpeed = speed * Mathf.Cos(angle);
            if (horizontalSpeed < 1e-5f)
                return 90f;

            var flightTime = distance / horizontalSpeed;
            var verticalSpeedOnArrival = speed * Mathf.Sin(angle) - gravityMagnitude * flightTime;
            return Mathf.Atan2(-verticalSpeedOnArrival, horizontalSpeed) * Mathf.Rad2Deg;
        }

        static Vector3 DirectionFromTangent(Vector3 horizontalDirection, Vector3 up, float tangent)
        {
            var angle = Mathf.Atan(tangent);
            return (horizontalDirection * Mathf.Cos(angle) + up * Mathf.Sin(angle)).normalized;
        }

        void Log(string message)
        {
            if (m_LogAssistDecisions)
                Debug.Log($"[{nameof(ThrowAssistant)}] {name}: {message}", this);
        }
    }
}
