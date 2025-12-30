using UnityEngine;

namespace RuneRelic.UI.Core
{
    /// <summary>
    /// Base class for all UI screens.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Header("Screen Info")]
        [SerializeField] private string screenName;
        [SerializeField] private bool canGoBack = true;
        [SerializeField] private string backScreenName;

        protected UIManager UIManager { get; private set; }
        protected UITheme Theme => UIManager?.Theme;
        protected CanvasGroup CanvasGroup { get; private set; }

        public string ScreenName => screenName;
        public bool CanGoBack => canGoBack;
        public string BackScreenName => backScreenName;

        /// <summary>
        /// Called once when the screen is registered.
        /// </summary>
        public virtual void Initialize(UIManager manager)
        {
            UIManager = manager;
            CanvasGroup = GetComponent<CanvasGroup>();

            if (CanvasGroup == null)
            {
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            OnInitialize();
        }

        /// <summary>
        /// Override for custom initialization.
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// Called when screen becomes visible.
        /// </summary>
        public virtual void OnShow()
        {
            // Enable interaction
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
        }

        /// <summary>
        /// Called when screen is hidden.
        /// </summary>
        public virtual void OnHide()
        {
            // Disable interaction
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Handle back navigation.
        /// </summary>
        public virtual void OnBack()
        {
            if (canGoBack)
            {
                if (!string.IsNullOrEmpty(backScreenName))
                {
                    UIManager?.ShowScreen(backScreenName);
                }
                else
                {
                    UIManager?.GoBack();
                }
            }
        }

        /// <summary>
        /// Apply theme to this screen.
        /// </summary>
        public virtual void ApplyTheme(UITheme theme)
        {
            // Override in derived classes
        }
    }
}
