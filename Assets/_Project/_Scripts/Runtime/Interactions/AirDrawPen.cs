using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace IE.VRShowcase.Interactions
{
    /// <summary>
    /// Draws a mid-air trail while the held pencil is activated. Each activate/deactivate cycle
    /// produces one <see cref="LineRenderer"/> stroke that stays in the world.
    /// </summary>
    /// <remarks>
    /// Drive this with the interactor's Activate input (the trigger): grab the pencil to hold it,
    /// then pull the trigger to draw.
    /// </remarks>
    [AddComponentMenu("IE Showcase/Interactions/Air Draw Pen")]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class AirDrawPen : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Point the line is drawn from. Defaults to this transform.")]
        Transform m_Tip;

        [SerializeField]
        [Tooltip("Material applied to each stroke.")]
        Material m_LineMaterial;

        [SerializeField]
        [Tooltip("Stroke thickness in metres.")]
        float m_LineWidth = 0.008f;

        [SerializeField]
        [Tooltip("The tip must travel at least this far before a new point is recorded.")]
        float m_MinPointDistance = 0.006f;

        [SerializeField]
        [Tooltip("Optional parent for finished strokes, to keep the hierarchy tidy.")]
        Transform m_StrokeParent;

        XRGrabInteractable m_Interactable;
        LineRenderer m_CurrentStroke;
        Vector3 m_LastPoint;
        int m_StrokeCount;

        /// <summary>Whether a stroke is currently being drawn.</summary>
        public bool isDrawing => m_CurrentStroke != null;

        void Awake()
        {
            m_Interactable = GetComponent<XRGrabInteractable>();

            if (m_Tip == null)
                m_Tip = transform;
        }

        void OnEnable()
        {
            m_Interactable.activated.AddListener(OnActivated);
            m_Interactable.deactivated.AddListener(OnDeactivated);
            m_Interactable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            m_Interactable.activated.RemoveListener(OnActivated);
            m_Interactable.deactivated.RemoveListener(OnDeactivated);
            m_Interactable.selectExited.RemoveListener(OnSelectExited);

            EndStroke();
        }

        void LateUpdate()
        {
            if (m_CurrentStroke == null)
                return;

            var point = m_Tip.position;
            if (Vector3.Distance(point, m_LastPoint) < m_MinPointDistance)
                return;

            m_CurrentStroke.positionCount++;
            m_CurrentStroke.SetPosition(m_CurrentStroke.positionCount - 1, point);
            m_LastPoint = point;
        }

        void OnActivated(ActivateEventArgs args) => BeginStroke();

        void OnDeactivated(DeactivateEventArgs args) => EndStroke();

        // Dropping the pencil mid-stroke must not leave a stroke anchored to nothing.
        void OnSelectExited(SelectExitEventArgs args) => EndStroke();

        void BeginStroke()
        {
            EndStroke();

            var strokeGo = new GameObject($"Air Stroke {++m_StrokeCount:000}");
            if (m_StrokeParent != null)
                strokeGo.transform.SetParent(m_StrokeParent, true);

            m_CurrentStroke = strokeGo.AddComponent<LineRenderer>();
            m_CurrentStroke.useWorldSpace = true;
            m_CurrentStroke.widthMultiplier = m_LineWidth;
            m_CurrentStroke.numCapVertices = 4;
            m_CurrentStroke.numCornerVertices = 4;
            m_CurrentStroke.alignment = LineAlignment.View;
            m_CurrentStroke.textureMode = LineTextureMode.Tile;

            if (m_LineMaterial != null)
                m_CurrentStroke.sharedMaterial = m_LineMaterial;

            m_LastPoint = m_Tip.position;
            m_CurrentStroke.positionCount = 1;
            m_CurrentStroke.SetPosition(0, m_LastPoint);
        }

        void EndStroke()
        {
            if (m_CurrentStroke == null)
                return;

            // A tap that never moved leaves a degenerate one-point line; drop it.
            if (m_CurrentStroke.positionCount < 2)
            {
                m_StrokeCount--;
                Destroy(m_CurrentStroke.gameObject);
            }

            m_CurrentStroke = null;
        }
    }
}
