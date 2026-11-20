using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace IE.VRShowcase.XR
{
    /// <summary>
    /// The project's direct (near-field) grab interactor. Extends <see cref="XRDirectInteractor"/>
    /// so showcase-wide hand behaviour lives in one place.
    /// </summary>
    /// <remarks>
    /// Current behaviour: the hand visual is hidden while this interactor is holding something, so
    /// the static hand never clips through the grabbed object. Because the hands are not posed,
    /// hiding reads better than leaving an open hand wrapped around a held object.
    /// </remarks>
    [AddComponentMenu("IE Showcase/XR/Showcase Direct Interactor")]
    public class ShowcaseDirectInteractor : XRDirectInteractor
    {
        /// <summary>Name of the child looked up when <see cref="m_HandVisual"/> is not assigned.</summary>
        const string k_DefaultHandVisualName = "Hand Visual";

        [Header("Showcase Hand")]
        [SerializeField]
        [Tooltip("Hand visual to hide while holding something. If left empty, a child named "
            + "\"Hand Visual\" is used.")]
        GameObject m_HandVisual;

        [SerializeField]
        [Tooltip("Hide the hand visual while this interactor is holding something.")]
        bool m_HideHandOnSelect = true;

        /// <summary>The hand visual this interactor shows and hides.</summary>
        public GameObject handVisual
        {
            get => m_HandVisual;
            set
            {
                m_HandVisual = value;
                RefreshHandVisibility();
            }
        }

        /// <summary>Whether the hand visual is hidden while something is held.</summary>
        public bool hideHandOnSelect
        {
            get => m_HideHandOnSelect;
            set
            {
                m_HideHandOnSelect = value;
                RefreshHandVisibility();
            }
        }

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();

            if (m_HandVisual == null)
            {
                var found = transform.Find(k_DefaultHandVisualName);
                if (found != null)
                    m_HandVisual = found.gameObject;
            }
        }

        /// <inheritdoc />
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            RefreshHandVisibility();
        }

        /// <inheritdoc />
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            RefreshHandVisibility();
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            base.OnDisable();

            // Never leave the hand hidden if the interactor goes away mid-grab.
            SetHandVisible(true);
        }

        void RefreshHandVisibility()
        {
            SetHandVisible(!(m_HideHandOnSelect && hasSelection));
        }

        void SetHandVisible(bool visible)
        {
            if (m_HandVisual != null && m_HandVisual.activeSelf != visible)
                m_HandVisual.SetActive(visible);
        }
    }
}
