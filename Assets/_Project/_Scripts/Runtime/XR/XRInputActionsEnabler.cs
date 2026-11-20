using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IE.VRShowcase.XR
{
    /// <summary>
    /// Enables every <see cref="InputActionAsset"/> the XR rig depends on while this component is active.
    /// Input actions are disabled by default, so without this the Tracked Pose Drivers and the
    /// interactors' select/activate readers would never receive input.
    /// </summary>
    /// <remarks>
    /// This is the project-local equivalent of the <c>InputActionManager</c> that ships inside the
    /// XR Interaction Toolkit "Starter Assets" sample. It lives here so the rig prefab does not
    /// depend on that sample being imported.
    /// </remarks>
    [AddComponentMenu("IE Showcase/XR/XR Input Actions Enabler")]
    [DisallowMultipleComponent]
    public class XRInputActionsEnabler : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Action assets enabled on this component's OnEnable and disabled again on OnDisable.")]
        List<InputActionAsset> m_ActionAssets = new List<InputActionAsset>();

        /// <summary>The action assets this component enables.</summary>
        public List<InputActionAsset> actionAssets
        {
            get => m_ActionAssets;
            set => m_ActionAssets = value;
        }

        protected void OnEnable()
        {
            foreach (var asset in m_ActionAssets)
            {
                if (asset != null)
                    asset.Enable();
            }
        }

        protected void OnDisable()
        {
            foreach (var asset in m_ActionAssets)
            {
                if (asset != null)
                    asset.Disable();
            }
        }
    }
}
