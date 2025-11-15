#nullable disable
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
        public UsernameDisplayUpdater(IntPtr ptr) : base(ptr) { }

        private CustomerController _customer;
        private Camera _mainCamera;
        private bool _initialized = false;

        /// <summary>
        /// Display configuration constants
        /// </summary>
        private const float Y_OFFSET = 2.2f;
        private const float MAX_DISPLAY_DISTANCE = 50f;

        public void Initialize(CustomerController customer, Camera mainCamera)
        {
            _customer = customer;
            _mainCamera = mainCamera;
            _initialized = true;

            if (_mainCamera == null)
            {
                ModLogger.Warning("UsernameDisplayUpdater initialized with null camera - billboard behavior disabled");
            }

            if (_customer == null)
            {
                ModLogger.Warning("UsernameDisplayUpdater initialized with null customer - will destroy on first update");
            }
        }

        void LateUpdate()
        {
            if (!_initialized)
            {
                ModLogger.Warning("UsernameDisplayUpdater.LateUpdate called before Initialize - destroying");
                Destroy(gameObject);
                return;
            }

            if (_customer == null || _customer.transform == null)
            {
                ModLogger.Debug("Customer destroyed - cleaning up username display");
                Destroy(gameObject);
                return;
            }

            transform.position = _customer.transform.position + Vector3.up * Y_OFFSET;

            // Billboard behavior for readability
            if (_mainCamera != null)
            {
                transform.LookAt(_mainCamera.transform);
                // TextMeshPro faces backward by default, rotate 180° on Y
                transform.Rotate(0, 180, 0);
            }

            // Distance culling for visibility
            if (_mainCamera != null)
            {
                float distance = Vector3.Distance(_mainCamera.transform.position, transform.position);
                bool isWithinDistance = distance <= MAX_DISPLAY_DISTANCE;

                bool shouldShow = isWithinDistance;

                if (gameObject.activeSelf != shouldShow)
                {
                    gameObject.SetActive(shouldShow);
                }
            }
        }

        void OnDestroy()
        {
            _customer = null;
            _mainCamera = null;
            _initialized = false;
        }
    }
}

