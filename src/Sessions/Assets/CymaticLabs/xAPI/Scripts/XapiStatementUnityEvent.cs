using System;
using UnityEngine.Events;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Unity Event related to xAPI statements: actor, verb, object
    /// </summary>
    [Serializable]
    public class XapiStatementUnityEvent : UnityEvent<string, string, string> { };
}
