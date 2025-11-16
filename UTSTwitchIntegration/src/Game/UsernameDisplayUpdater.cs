using UnityEngine;
using Il2CppGame.Customers;
using System;
using ModLogger = UTSTwitchIntegration.Utils.Logger;

namespace UTSTwitchIntegration.Game
{
    /// <summary>
    /// MonoBehaviour that updates username display position and orientation each frame
    /// Handles billboard behavior (facing camera) and distance culling
    /// </summary>
    public class UsernameDisplayUpdater : MonoBehaviour
    {
        /// <summary>
        /// Constructor required for Il2CppInterop class injection
        /// </summary>
        public UsernameDisplayUpdater(IntPtr ptr) : base(ptr)
        {
        }

        private CustomerController customer;
        private Camera mainCamera;
        private bool initialized;

        /// <summary>
        /// Display configuration constants
        /// </summary>
        private const float Y_OFFSET = 2.2f;

        private const float MAX_DISPLAY_DISTANCE = 50f;

        public void Initialize(CustomerController customerController, Camera camera)
        {
            this.customer = customerController;
            this.mainCamera = camera;
            this.initialized = true;

            if (!this.mainCamera)
            {
                ModLogger.Warning("UsernameDisplayUpdater initialized with null camera - billboard behavior disabled");
            }

            if (!this.customer)
            {
                ModLogger.Warning("UsernameDisplayUpdater initialized with null customer - will destroy on first update");
            }
        }

        void LateUpdate()
        {
            if (!this.initialized || !this.customer || !this.customer.transform)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = this.customer.transform.position + Vector3.up * Y_OFFSET;

            // Billboard behavior for readability
            if (this.mainCamera)
            {
                transform.LookAt(this.mainCamera.transform);
                // TextMeshPro faces backward by default, rotate 180° on Y
                transform.Rotate(0, 180, 0);
            }

            // Distance culling for visibility
            if (!this.mainCamera)
                return;

            float distance = Vector3.Distance(this.mainCamera.transform.position, this.transform.position);
            bool isWithinDistance = distance <= MAX_DISPLAY_DISTANCE;

            if (this.gameObject.activeSelf != isWithinDistance)
            {
                this.gameObject.SetActive(isWithinDistance);
            }
        }

        void OnDestroy()
        {
            this.customer = null;
            this.mainCamera = null;
            this.initialized = false;
        }
    }
}