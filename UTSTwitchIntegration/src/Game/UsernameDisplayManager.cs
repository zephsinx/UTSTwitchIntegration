#nullable disable
using System.Collections.Generic;
using UnityEngine;
using Il2CppTMPro;
using Il2CppGame.Customers;
using ModLogger = UTSTwitchIntegration.Utils.Logger;

namespace UTSTwitchIntegration.Game
{
    /// <summary>
    /// Manager for creating and configuring username displays
    /// </summary>
    public static class UsernameDisplayManager
    {
        private const float CANVAS_SCALE = 0.01f;
        private const float Y_OFFSET = 2.2f;
        private const float CANVAS_WIDTH = 200f;
        private const float CANVAS_HEIGHT = 50f;
        private const float TEXT_WIDTH = 200f;
        private const float TEXT_HEIGHT = 50f;
        private const int FONT_SIZE = 20;
        private const int SORTING_ORDER = 100;

        /// <summary>
        /// Cached font asset for fallback (Atma-Regular SDF)
        /// </summary>
        private static TMP_FontAsset _cachedAtmaFont = null;

        /// <summary>
        /// Track all created displays for cleanup
        /// </summary>
        private static readonly List<GameObject> _activeDisplays = new List<GameObject>();
        private static readonly object _displaysLock = new object();

        /// <summary>
        /// Create a new canvas GameObject for username display
        /// </summary>
        /// <param name="customer">Customer to attach canvas to</param>
        /// <param name="username">Twitch username for naming</param>
        /// <returns>Created canvas GameObject, or null if creation failed</returns>
        private static GameObject CreateNewCanvas(CustomerController customer, string username)
        {
            GameObject canvasGO = new GameObject($"TwitchUsernameCanvas_{username}");
            canvasGO.transform.SetParent(customer.transform, false);
            canvasGO.transform.localPosition = new Vector3(0, Y_OFFSET, 0);
            canvasGO.SetActive(true);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SORTING_ORDER;

            // Scale canvas for readable text at world space distance
            canvasGO.transform.localScale = new Vector3(CANVAS_SCALE, CANVAS_SCALE, CANVAS_SCALE);

            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT);
            }

            return canvasGO;
        }

        /// <summary>
        /// Create a username display above a customer NPC
        /// </summary>
        /// <param name="customer">Customer to attach display to</param>
        /// <param name="username">Twitch username to display</param>
        /// <returns>Created canvas GameObject, or null if creation failed</returns>
        public static GameObject CreateDisplay(CustomerController customer, string username)
        {
            if (customer == null)
            {
                ModLogger.Warning("Cannot create username display: customer is null");
                return null;
            }

            if (customer.transform == null)
            {
                ModLogger.Warning("Cannot create username display: customer.transform is null");
                return null;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                ModLogger.Warning("Cannot create username display: username is null or empty");
                return null;
            }

            try
            {
                if (customer.gameObject == null || !customer.gameObject.activeInHierarchy)
                {
                    ModLogger.Warning($"Cannot create display for '{username}': customer GameObject is inactive");
                    return null;
                }

                if (Camera.main == null)
                {
                    ModLogger.Warning($"Cannot create display for '{username}': Camera.main is null");
                    return null;
                }

                GameObject canvasGO = CreateNewCanvas(customer, username);
                if (canvasGO == null)
                {
                    ModLogger.Warning($"Failed to create canvas for '{username}'");
                    return null;
                }

                Canvas canvas = canvasGO.GetComponent<Canvas>();

                GameObject textGO = new GameObject($"UsernameText_{username}");

                RectTransform textRect = textGO.GetComponent<RectTransform>();
                if (textRect != null)
                {
                    textRect.sizeDelta = new Vector2(TEXT_WIDTH, TEXT_HEIGHT);
                    textRect.anchoredPosition = Vector2.zero;
                    textRect.anchorMin = new Vector2(0.5f, 0.5f);
                    textRect.anchorMax = new Vector2(0.5f, 0.5f);
                    textRect.pivot = new Vector2(0.5f, 0.5f);
                }

                TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
                text.text = username;
                text.fontSize = FONT_SIZE;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.enableWordWrapping = false;

                if (customer._debugText != null && customer._debugText.font != null)
                {
                    text.font = customer._debugText.font;
                }
                else
                {
                    TMP_FontAsset atmaFont = GetAtmaRegularFont();
                    if (atmaFont != null)
                    {
                        text.font = atmaFont;
                    }
                    else
                    {
                        ModLogger.Warning($"Could not find Atma-Regular SDF font - username '{username}' may not render correctly");
                    }
                }

                UsernameDisplayUpdater updater = canvasGO.AddComponent<UsernameDisplayUpdater>();
                updater.Initialize(customer, Camera.main);

                textGO.transform.SetParent(canvasGO.transform, false);

                lock (_displaysLock)
                {
                    _activeDisplays.Add(canvasGO);
                }

                ModLogger.Debug($"Created username display for '{username}' on Customer ID={customer.CustomerId}");

                return canvasGO;
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Failed to create username display for '{username}': {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Cleanup all active username displays
        /// </summary>
        public static void CleanupAllDisplays()
        {
            try
            {
                lock (_displaysLock)
                {
                    int count = 0;
                    foreach (GameObject display in _activeDisplays)
                    {
                        if (display != null)
                        {
                            try
                            {
                                Object.Destroy(display);
                                count++;
                            }
                            catch (System.Exception ex)
                            {
                                ModLogger.Debug($"Error destroying display GameObject: {ex.Message}");
                            }
                        }
                    }

                    _activeDisplays.Clear();
                    ModLogger.Debug($"Cleaned up {count} username displays");
                }

                _cachedAtmaFont = null;
                ModLogger.Info("Username display manager cleanup completed");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error during username display cleanup: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Get the Atma-Regular SDF font asset from Resources
        /// </summary>
        /// <returns>Atma-Regular SDF font asset, or null if not found</returns>
        private static TMP_FontAsset GetAtmaRegularFont()
        {
            if (_cachedAtmaFont != null)
            {
                return _cachedAtmaFont;
            }

            try
            {
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<TMP_FontAsset> allFontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (TMP_FontAsset font in allFontAssets)
                {
                    if (font != null && font.name == "Atma-Regular SDF")
                    {
                        _cachedAtmaFont = font;
                        ModLogger.Debug("Found and cached Atma-Regular SDF font");
                        return _cachedAtmaFont;
                    }
                }

                ModLogger.Warning("Atma-Regular SDF font not found in Resources");
                return null;
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error searching for Atma-Regular SDF font: {ex.Message}");
                return null;
            }
        }
    }
}

