namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Different voice chat modes.
    /// </summary>
    public enum VoiceChatModes
    {
        /// <summary>
        /// Voice chat is disabled.
        /// </summary>
        Off = 0,

        /// <summary>
        /// A push-to-talk input must be held down to speak.
        /// </summary>
        PushToTalk = 1,

        /// <summary>
        /// A push-to-talk input will toggle voice chat on and off.
        /// </summary>
        PushToTalkToggle = 2,

        /// <summary>
        /// Speaking freely will trigger transmission of voice when input audio is loud enough
        /// and supressed when below the volume threshold.
        /// </summary>
        VoiceActivated = 3,

        /// <summary>
        /// The user speaks and a translation service translates their speech into the language of each peer.
        /// </summary>
        AutoTranslated = 4,
    }
}
