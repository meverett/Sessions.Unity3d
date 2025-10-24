using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using CymaticLabs.Logging;
using Newtonsoft.Json;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Manager class that configures and interfaces with xAPI services.
    /// </summary>
    public class XapiManager : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The API version to use with the client.
        /// </summary>
        [Tooltip("The API version to use with the client.")]
        public string ApiVersion = "1.0.3";

        /// <summary>
        /// The base xAPI URL.
        /// </summary>
        [Tooltip("The base xAPI URL.")]
        public string BaseURL = "http://xapi.cymaticlabs.net/data/xAPI/";

        /// <summary>
        /// The HTTP BasicAuth client token to use to authenticate the client with the API.
        /// </summary>
        [Tooltip("The HTTP BasicAuth client token to use to authenticate the client with the API.")]
        public string BasicAuth = "";

        /// <summary>
        /// The configuration of the default statement.
        /// </summary>
        [HideInInspector]
        public XapiStatement DefaultStatement;

        /// <summary>
        /// The current routing configuration to use.
        /// </summary>
        //[Tooltip("The current routing configuration to use.")]
        [HideInInspector]
        public XapiConfiguration Configuration;

        /// <summary>
        /// A list of all of the current configurations within the config.
        /// </summary>
        [HideInInspector]
        public List<XapiConfiguration> AllConfigurations;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static XapiManager Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
        }

        private void Start()
        {
            LoadConfiguration();
            CyLog.LogInfo("[xAPI] Services Started");
        }

        #endregion Init

        #region xAPI

        #region Statements

        /// <summary>
        /// Creates a new xAPI statement at the configured xAPI endpoint.
        /// </summary>
        /// <param name="actor">The statement's actor.</param>
        /// <param name="verb">The statement's verb.</param>
        /// <param name="obj">The statement's object.</param>
        /// <param name="timestamp">The statement's time stamp.</param>
        /// <returns>The created statements.</returns>
        public XapiStatement CreateStatement(string actor, string verb, string obj, DateTime? timestamp = null)
        {
            if (!this.enabled || !gameObject.activeSelf) return null;

            if (string.IsNullOrEmpty(actor)) throw new ArgumentNullException("actor");
            if (string.IsNullOrEmpty(verb)) throw new ArgumentNullException("verb");
            if (string.IsNullOrEmpty(obj)) throw new ArgumentNullException("obj");
            if (timestamp == null) timestamp = DateTime.UtcNow;

            // Create a new statement and copy values from the manager's default statement
            var ns = new XapiStatement();
            var ds = DefaultStatement;

            // Copy actor
            ns.Actor = new XapiActor();
            ns.Actor.AccountName = ds.Actor.AccountName + actor;
            ns.Actor.AccoutURL = ds.Actor.AccoutURL;
            ns.Actor.Name = ds.Actor.Name + actor;

            // Copy verb
            ns.Verb = new XapiVerb();
            ns.Verb.Id = ds.Verb.Id + verb;
            ns.Verb.Language = ds.Verb.Language;
            ns.Verb.Name = ds.Verb.Name + verb;

            // Copy object
            ns.Object = new XapiObject();
            ns.Object.Id = ds.Object.Id + obj;
            ns.Object.Language = ds.Object.Language;
            ns.Object.Name = ds.Object.Name + obj;
            ns.Object.Type = ds.Object.Type + obj;

            // Copy context
            ns.Context = new XapiContext();
            ns.Context.Language = ds.Context.Language;
            ns.Context.Platform = ds.Context.Platform;

            // Copy time stamp
            ns.Timestamp = timestamp.Value;

            // Create the statement
            CreateStatement(ns);

            return ns;
        }

        /// <summary>
        /// Creates a new xAPI statement at the configured xAPI endpoint.
        /// </summary>
        /// <param name="statement">The statement to create.</param>
        /// <returns>The created statements ID.</returns>
        public string CreateStatement(XapiStatement statement)
        {
            if (!this.enabled || !gameObject.activeSelf) return null;

            #region Validate

            if (statement == null) throw new ArgumentNullException("statement");
            if (statement.Actor == null) throw new ArgumentNullException("statement.Actor");
            if (statement.Verb == null) throw new ArgumentNullException("statement.Verb");
            if (statement.Object == null) throw new ArgumentNullException("statement.Object");
            if (statement.Context == null) throw new ArgumentNullException("statement.Context");

            if (string.IsNullOrEmpty(statement.Actor.Name) || string.IsNullOrEmpty(statement.Actor.AccountName))
                throw new ArgumentNullException("statement.Actor.Name|statement.Actor.Name");

            if (string.IsNullOrEmpty(statement.Actor.AccoutURL))
                throw new ArgumentNullException("statement.Actor.AccountUrl");

            if (string.IsNullOrEmpty(statement.Verb.Id))
                throw new ArgumentNullException("statement.Verb.Id");

            if (string.IsNullOrEmpty(statement.Verb.Language))
                throw new ArgumentNullException("statement.Verb.Language");

            if (string.IsNullOrEmpty(statement.Verb.Name))
                throw new ArgumentNullException("statement.Verb.Name");

            if (string.IsNullOrEmpty(statement.Object.Id))
                throw new ArgumentNullException("statement.Object.Id");

            if (string.IsNullOrEmpty(statement.Object.Language))
                throw new ArgumentNullException("statement.Object.Language");

            if (string.IsNullOrEmpty(statement.Object.Name))
                throw new ArgumentNullException("statement.Object.Name");

            if (string.IsNullOrEmpty(statement.Object.Type))
                throw new ArgumentNullException("statement.Object.Type");

            if (string.IsNullOrEmpty(statement.Context.Language))
                throw new ArgumentNullException("statement.Context.Language");

            if (string.IsNullOrEmpty(statement.Context.Platform))
                throw new ArgumentNullException("statement.Context.Platform");

            #endregion Validate

            // Create new ID
            statement.Id = Guid.NewGuid().ToString();

            // Add time stamp
            statement.Timestamp = DateTime.UtcNow;

            // Send request
            StartCoroutine(DoCreateStatement(statement));

            return statement.Id;
        }

        // Creates an xAPI statement at the configured endpoint
        private IEnumerator DoCreateStatement(XapiStatement statement)
        {
            if (!this.enabled || !gameObject.activeSelf) yield break;

            #region Create the JSON Request

            // Build the JSON string out of the objects
            var jsonDict = new Dictionary<string, object>()
            {
                {
                    "id", statement.Id
                },
                {
                    "actor", new Dictionary<string,object>()
                    {
                        {
                            "name", statement.Actor.Name
                        },
                        {
                            "account", new Dictionary<string, object>()
                            {
                                { "homePage", statement.Actor.AccoutURL },
                                { "name", statement.Actor.AccountName },
                            }
                        }
                    }
                },
                {
                    "verb", new Dictionary<string, object>()
                    {
                        {
                            "id", statement.Verb.Id
                        },
                        {
                            "display", new Dictionary<string, object>()
                            {
                                // TODO inject language values
                            }
                        }
                    }
                },
                {
                    "object", new Dictionary<string, object>()
                    {
                        {
                            "id", statement.Object.Id
                        },
                        {
                            "definition", new Dictionary<string, object>()
                            {
                                {
                                    "type", statement.Object.Type
                                },
                                {
                                    "name", new Dictionary<string, object>()
                                    {
                                        // TODO inject language values
                                    }
                                }
                            }
                        }
                    }
                },
                {
                    "context", new Dictionary<string, object>()
                    {
                        {
                            "platform", statement.Context.Platform
                        },
                        {
                            "language", statement.Context.Language
                        }
                    }
                },
                {
                    "timestamp", statement.Timestamp
                }
            };

            #endregion Create the JSON Request

            // Fill in more dynamic language values
            var verb = (Dictionary<string, object>)jsonDict["verb"];
            var display = (Dictionary<string, object>)verb["display"];
            display.Add(statement.Verb.Language, statement.Verb.Name);

            var obj = (Dictionary<string, object>)jsonDict["object"];
            var definition = (Dictionary<string, object>)obj["definition"];
            var name = (Dictionary<string, object>)definition["name"];
            name.Add(statement.Object.Language, statement.Object.Name);

            // Conver to JSON
            var json = JsonConvert.SerializeObject(jsonDict);
            var data = Encoding.UTF8.GetBytes(json);

            CyLog.LogInfo(json);

            var www = UnityWebRequest.Put(BaseURL + "statements?statementId=" + statement.Id, data);
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", BasicAuth);
            www.SetRequestHeader("X-Experience-API-Version", ApiVersion);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                CyLog.LogErrorFormat("[xAPI] [ERROR] creating statement: {0}", www.error);
            }
            else
            {
                CyLog.LogVerboseFormat("[xAPI] {0} {1} {2} {3}", statement.Actor.Name, statement.Verb.Name, statement.Object.Name, statement.Timestamp);
            }
        }

        #endregion Statements

        #endregion xAPI

        #region Configuration

        /// <summary>
        /// Loads the current configuration into memory.
        /// </summary>
        public void LoadConfiguration()
        {
            if (Configuration == null) return;

            ApiVersion = Configuration.ApiVersion;
            BaseURL = Configuration.BaseURL;
            BasicAuth = Configuration.BasicAuth;
            DefaultStatement = Configuration.DefaultStatement;
        }

        #endregion Configuration

        #endregion Methods
    }
}
