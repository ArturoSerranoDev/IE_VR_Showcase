using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace IE.VRShowcase.Interactions
{
    /// <summary>
    /// A button in the world that switches a <see cref="ThrowAssistTarget"/>'s assist on and off.
    /// The cap sinks while held and the indicator and label show the current state.
    /// </summary>
    /// <remarks>
    /// This is driven by select, not by a physical press. The showcase hands carry no solid
    /// colliders - only a trigger volume for grabbing - so nothing in the rig can physically push
    /// a button cap. Touch the button and squeeze grip to toggle it. Giving the rig a poke
    /// interactor, or the hands real colliders, would allow a true press instead.
    /// </remarks>
    [AddComponentMenu("IE Showcase/Interactions/Assist Toggle Button")]
    [RequireComponent(typeof(XRBaseInteractable))]
    public class AssistToggleButton : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The target whose assist this button switches.")]
        ThrowAssistTarget m_Target;

        [SerializeField]
        [Tooltip("Cap that sinks while the button is held.")]
        Transform m_Cap;

        [SerializeField]
        [Tooltip("How far the cap sinks, in metres along its local Y.")]
        float m_PressDepth = 0.015f;

        [SerializeField]
        [Tooltip("Renderer tinted to show the current state.")]
        Renderer m_IndicatorRenderer;

        [SerializeField]
        Color m_OnColor = new Color(0.25f, 0.9f, 0.35f);

        [SerializeField]
        Color m_OffColor = new Color(0.6f, 0.18f, 0.18f);

        [SerializeField]
        [Tooltip("Optional label showing the current state.")]
        TextMesh m_Label;

        XRBaseInteractable m_Interactable;
        Vector3 m_CapRestPosition;
        bool m_HasCap;

        void Awake()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();

            m_HasCap = m_Cap != null;
            if (m_HasCap)
                m_CapRestPosition = m_Cap.localPosition;
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnPressed);
            m_Interactable.selectExited.AddListener(OnReleased);
            RefreshVisuals();
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnPressed);
            m_Interactable.selectExited.RemoveListener(OnReleased);

            // Never leave the cap stuck down if the button is torn down mid-press.
            if (m_HasCap)
                m_Cap.localPosition = m_CapRestPosition;
        }

        void OnPressed(SelectEnterEventArgs args)
        {
            if (m_Target != null)
                m_Target.assistEnabled = !m_Target.assistEnabled;

            if (m_HasCap)
                m_Cap.localPosition = m_CapRestPosition + Vector3.down * m_PressDepth;

            RefreshVisuals();
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (m_HasCap)
                m_Cap.localPosition = m_CapRestPosition;
        }

        void RefreshVisuals()
        {
            var isOn = m_Target != null && m_Target.assistEnabled;

            if (m_IndicatorRenderer != null)
                m_IndicatorRenderer.material.color = isOn ? m_OnColor : m_OffColor;

            if (m_Label != null)
                m_Label.text = isOn ? "THROW ASSIST\nON" : "THROW ASSIST\nOFF";
        }
    }
}
