using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Handles UI notifications for VR.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class SessionsNotifications : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The UI panel used to contain notification elements.
        /// </summary>
        [Tooltip("The UI panel used to contain notification elements.")]
        public GameObject NotificationsContainer;

        /// <summary>
        /// The template used to produce visual notifications.
        /// </summary>
        [Tooltip("The template used to produce visual notifications.")]
        public GameObject NotificationTemplate;

        #endregion Inspector

        #region Fields

        // A list of temporary UI notifications
        private List<Notification> notifications;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The static singleton instance of the behavior.
        /// </summary>
        public static SessionsNotifications Current { get;  private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            notifications = new List<Notification>();
            if (NotificationTemplate != null) NotificationTemplate.SetActive(false); // hide on start
        }

        #endregion Init

        #region Update

        #endregion Update

        #region Notifications

        /// <summary>
        /// Creates a visual UI notification.
        /// </summary>
        /// <param name="message">The text of the notification.</param>
        /// <param name="audioClip">The name of the registered audio clip to play during the notification.</param>
        /// <param name="duration">The duration of the notification in seconds.</param>
        /// <param name="delay">The delay in seconds before showing the notification.</param>
        public static void GlobalNotify(string message, string audioClip = null, float duration = 2, float delay = 0)
        {
            if (Current == null) return;
            Current.Notify(message, audioClip, duration, delay);
        }

        /// <summary>
        /// Creates a visual UI notification.
        /// </summary>
        /// <param name="message">The text of the notification.</param>
        /// <param name="audioClip">The name of the registered audio clip to play during the notification.</param>
        /// <param name="duration">The duration of the notification in seconds.</param>
        /// <param name="delay">The delay in seconds before showing the notification.</param>
        public void Notify(string message, string audioClip = null, float duration = 2, float delay = 0)
        {
            StartCoroutine(DoShowNotification(message, audioClip, duration, delay));
        }

        // Creates a visual notification and manages its life cycle
        private IEnumerator DoShowNotification(string message, string audioClip, float duration, float delay)
        {
            if (NotificationTemplate == null || NotificationsContainer == null) yield break;

            if (delay > 0) yield return new WaitForSeconds(delay);

            // Create a new notification for this event
            var gobj = Instantiate<GameObject>(NotificationTemplate);
            var panel = gobj.GetComponentInChildren<Image>();
            var text = gobj.GetComponentInChildren<Text>();
            var notification = new Notification() { Panel = panel, Text = text };
            notifications.Add(notification);
            text.text = message;
            panel.gameObject.SetActive(true);
            panel.transform.SetParent(NotificationsContainer.transform, false);

            // Reset position based on template
            panel.transform.position = NotificationTemplate.transform.position;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = NotificationTemplate.transform.localScale;

            // Play any desired audio clip
            if (!string.IsNullOrEmpty(audioClip))
            {
                PlayClip(audioClip);
            }

            // Setup a timer and fade it out after
            float timer = 0;

            // Wait out the timer for the duration
            while (timer < duration)
            {
                timer += Time.deltaTime;
                yield return 0;
            }

            // Fade out the element
            timer = 0;
            float fadeOutDuration = 1f;
            var ogPanelAlpha = panel.color.a;
            var ogTextAlpha = panel.color.a;
            Color pc;
            Color tc;

            while (timer < fadeOutDuration)
            {
                timer += Time.deltaTime;
                var percent = timer / fadeOutDuration;

                pc = panel.color;
                pc.a = Mathf.Lerp(ogPanelAlpha, 0, percent);
                panel.color = pc;

                tc = text.color;
                tc.a = Mathf.Lerp(ogTextAlpha, 0, percent);
                text.color = tc;

                yield return 0;
            }

            pc = panel.color;
            pc.a = 0;
            panel.color = pc;

            tc = text.color;
            tc.a = 0;
            text.color = tc;

            // Clean up notification element
            Destroy(panel.gameObject);
        }

        /// <summary>
        /// Captures the two main elements of a notification UI element.
        /// </summary>
        public class Notification
        {
            public Image Panel;
            public Text Text;
        }

        #endregion Notifications

        #region Audio

        /// <summary>
        /// Plays a registered audio clip given its name.
        /// </summary>
        /// <param name="name">The name of the audio clip to play.</param>
        /// <param name="oneShot">Whether or not to play it as a one shot.</param>
        public static void PlayClip(string name)
        {
            SessionsSound.PlayEfx(name);
        }

        #endregion Audio

        #endregion Methods
    }
}


