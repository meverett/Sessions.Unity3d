using System;
using UnityEngine;

namespace CymaticLabs.Language.Unity3d
{
    /// <summary>
    /// Information about an available language.
    /// </summary>
    public class LanguageInfo
    {
        /// <summary>
        /// The language code.
        /// </summary>
        public string Code;

        /// <summary>
        /// The human-readable language name.
        /// </summary>
        public string Label;

        /// <summary>
        /// The corresponding Unity language, if any.
        /// </summary>
        public SystemLanguage SystemLanguage = SystemLanguage.Unknown;

        /// <summary>
        /// Create an unintialized instance.
        /// </summary>
        public LanguageInfo() { }

        /// <summary>
        /// Create an instance initialized with the specified langauge properties.
        /// </summary>
        /// <param name="code">The language code (en-US, etc.)</param>
        /// <param name="label">The human-readable label for the language.</param>
        /// <param name="language">The corresponding Unity <see cref="SystemLanguage">system language</see>.</param>
        public LanguageInfo(string code, string label, SystemLanguage language = SystemLanguage.Unknown)
        {
            Code = code;
            Label = label;
            SystemLanguage = language;
        }
    }
}
