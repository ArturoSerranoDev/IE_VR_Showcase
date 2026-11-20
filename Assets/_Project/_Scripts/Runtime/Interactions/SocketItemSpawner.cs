using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace IE.VRShowcase.Interactions
{
    /// <summary>
    /// Keeps a socket stocked. Whenever the item sitting in the socket is taken out, a fresh
    /// instance is spawned and placed into the socket, so the socket acts as a dispenser.
    /// </summary>
    [AddComponentMenu("IE Showcase/Interactions/Socket Item Spawner")]
    [RequireComponent(typeof(XRSocketInteractor))]
    public class SocketItemSpawner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Prefab spawned into the socket. Must have an XRGrabInteractable on its root.")]
        GameObject m_ItemPrefab;

        [SerializeField]
        [Tooltip("Spawn the first item when the scene starts.")]
        bool m_SpawnOnStart = true;

        [SerializeField]
        [Tooltip("Optional parent for spawned items, to keep the hierarchy tidy.")]
        Transform m_SpawnParent;

        XRSocketInteractor m_Socket;

        void Awake()
        {
            m_Socket = GetComponent<XRSocketInteractor>();
        }

        void OnEnable()
        {
            m_Socket.selectExited.AddListener(OnSocketSelectExited);
        }

        void OnDisable()
        {
            m_Socket.selectExited.RemoveListener(OnSocketSelectExited);
        }

        void Start()
        {
            if (m_SpawnOnStart)
                StartCoroutine(SpawnIntoSocket());
        }

        void OnSocketSelectExited(SelectExitEventArgs args)
        {
            if (!isActiveAndEnabled)
                return;

            StartCoroutine(SpawnIntoSocket());
        }

        IEnumerator SpawnIntoSocket()
        {
            // The socket is still mid-release on the frame selectExited fires; inserting a
            // replacement now would be rejected, so let the interaction settle first.
            yield return null;

            if (m_ItemPrefab == null || m_Socket == null || !m_Socket.isActiveAndEnabled)
                yield break;

            // Someone may have dropped another item in before the replacement arrived.
            if (m_Socket.hasSelection)
                yield break;

            var attach = m_Socket.attachTransform != null ? m_Socket.attachTransform : m_Socket.transform;
            var instance = Instantiate(m_ItemPrefab, attach.position, attach.rotation);
            instance.name = m_ItemPrefab.name;

            if (m_SpawnParent != null)
                instance.transform.SetParent(m_SpawnParent, true);

            var interactable = instance.GetComponent<XRGrabInteractable>();
            if (interactable == null)
            {
                Debug.LogWarning($"[{nameof(SocketItemSpawner)}] '{m_ItemPrefab.name}' has no " +
                    $"{nameof(XRGrabInteractable)} on its root, so it cannot be placed in the socket.", this);
                yield break;
            }

            var manager = m_Socket.interactionManager;
            if (manager == null)
                yield break;

            manager.SelectEnter((IXRSelectInteractor)m_Socket, (IXRSelectInteractable)interactable);
        }
    }
}
