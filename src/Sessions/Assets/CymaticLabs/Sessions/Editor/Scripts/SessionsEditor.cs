using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using CymaticLabs.Protocols.Osc.Unity3d;
using CymaticLabs.Sessions.Core;
using CymaticLabs.xAPI.Unity3d;
using EditorCoroutines;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// The main custom editing window for Sessions.
    /// </summary>
    public class SessionsEditor : EditorWindow
    {
        #region Inspector

        #endregion Inspector

        #region Fields

        // Whether or not the window has been initialized after opening
        private bool isInitialized = false;
        private static bool isListening = false;

        // Whether or not we are currently monitoring network activity
        private bool isMonitoringNetwork = false;

        // The index of the selected tab
        private int tabIndex = 0;

        // A list of window tabs
        private GUIContent[] tabs;
        private GUILayoutOption[] tabOptions;

        // Used for a horizontal line break
        private GUIStyle horizontalLineStyle;

        // The current scene
        private SessionsScene scene;

        #region Scenes

        // The list of Session scenes
        private List<SessionsSceneInfo> scenesList;

        private Vector2 scenesScrollView = Vector2.zero;

        #endregion Scenes

        #region OSC

        // Scroll view used in the OSC configuration tab
        private Vector2 oscScrollViewH;
        private Vector2 oscScrollViewV;

        // Texture used to create an error hightlight in editable UI fields
        private Texture2D errorTexture;

        // The current list of OSC value maps
        private List<OscRangeMapFloat> oscMaps;

        #endregion OSC

        #region Routing

        // The current list of routing fules
        private List<SessionsRoutingRule> routingRules;

        // Scroll view used in the routing configuration tab
        private Vector2 routingScrollView;

        #endregion Routing

        #region Entities

        // The current list of network entities
        private List<SessionsNetworkEntityInfo> networkEntities;

        // Scroll view used in the entities configuration tab
        private Vector2 entitiesScrollView;

        #endregion Entities

        #region xAPI

        // The scroll view vaues for the xAPI configuration tab
        private Vector2 xapiScrollView = Vector2.zero;

        // Whether or not the xAPI "Service" foldout is open
        private bool isXapiServiceFoldoutOpen = false;

        // Whether or not the xAPI "Context" foldout is open
        private bool isXapiContextFoldoutOpen = false;

        // Whether or not the xAPI "Default Actor" foldout is open
        private bool isXapiActorFoldoutOpen = false;

        // Whether or not the xAPI "Default Verb" foldout is open
        private bool isXapiVerbFoldoutOpen = false;

        // Whether or not the xAPI "Default Object" foldout is open
        private bool isXapiObjectFoldoutOpen = false;

        // Lists of xAPI targets
        private List<XapiActor> xapiActors;
        private List<XapiVerb> xapiVerbs;
        private List<XapiObject> xapiObjects;

        #endregion xAPI

        #region Icons

        // Icon references used by the UI
        private Texture2D oscIcon;
        private Texture2D routingIcon;
        private Texture2D networkIcon;
        private Texture2D newIcon;
        private Texture2D saveIcon;
        private Texture2D exportIcon;
        private Texture2D deleteIcon;
        private Texture2D importIcon;
        private Texture2D addIcon;
        private Texture2D clearIcon;
        private Texture2D warnIcon;
        private Texture2D prefabIcon;
        private Texture2D copyIcon;
        private Texture2D pasteIcon;
        private Texture2D editIcon;
        private Texture2D learnIcon;
        private Texture2D scenesIcon;
        private Texture2D defaultScene;

        #endregion Icons

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsEditor Current { get; private set; }

        /// <summary>
        /// Gets the color for the horizontal line break.
        /// </summary>
        public Color HorizontalLineColor
        {
            get { return EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.25f) : new Color(0, 0, 0, 0.25f); }
        }

        /// <summary>
        /// The currently selected Sesssion scene information.
        /// </summary>
        public SessionsScenesConfiguration SelectedScenesConfig { get; private set; }

        /// <summary>
        /// The currently selected OSC configuration (if any).
        /// </summary>
        public SessionsOscConfiguration SelectedOscConfig { get; private set; }

        /// <summary>
        /// The currently selected routing configuration (if any).
        /// </summary>
        public SessionsRoutingConfiguration SelectedRoutingConfig { get; private set; }

        /// <summary>
        /// The editor window clipboard for routing configurations.
        /// </summary>
        public static SessionsRoutingConfiguration RoutingClipboard { get; private set; }

        /// <summary>
        /// The currently selected entities configuration (if any).
        /// </summary>
        public SessionsEntitiesConfiguration SelectedEntitiesConfig { get; private set; }

        /// <summary>
        /// The currently selected xAPI configuration (if any).
        /// </summary>
        public XapiConfiguration SelectedXapiConfig { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void OnEnable()
        {
            // Required to restore state when exiting play mode
            if (!isListening)
            {
                EditorApplication.playModeStateChanged += (state) =>
                {
                    // Unity Editor drops references so we need to reinitialized to rebuild them when exiting play mode
                    if (state == PlayModeStateChange.EnteredEditMode)
                    {
                        isInitialized = false;
                        Initialize();
                        isMonitoringNetwork = false; // stop any network monitoring
                    }

                    // Start monitoring for network activity
                    if (state == PlayModeStateChange.EnteredPlayMode)
                    {
                        isMonitoringNetwork = true;
                        this.StartCoroutine(DoMonitorNetwork());

                        // Try to locate the current scene
                        if (scene == null) scene = FindObjectOfType<SessionsScene>();
                        if (scene != null)
                        {
                            // Reapply current configurations
                            var routingConfig = scene.RoutingConfiguration;
                            if (routingConfig != null) SelectRoutingConfig(routingConfig, false);

                            var entitiesConfig = scene.EntitiesConfiguration;
                            if (entitiesConfig != null) SelectEntitiesConfig(entitiesConfig, false);

                            var xapiConfig = scene.XapiConfiguration;
                            if (xapiConfig != null) SelectXapiConfig(xapiConfig, false);
                        }
                    }
                };

                // Reload window for new scene
                EditorSceneManager.sceneOpened += (scene, mode) =>
                {
                    //Debug.LogFormat("Clipboard: {0}", RoutingClipboard != null);
                    isInitialized = false;
                    Initialize();
                };

                isListening = true;
            }

            isInitialized = false;
            Initialize();
        }

        private void OnDisable()
        {
            isInitialized = false;
            Initialize();
        }

        #endregion Init

        #region Window

        /// <summary>
        /// Shows the editor window.
        /// </summary>
        [MenuItem("Window/Sessions/Editor")]
        public static void ShowWindow()
        {
            Current = GetWindow<SessionsEditor>();
        }

        #region OnGUI

        private void OnGUI()
        {
            // Ensure the window is initialized
            Initialize();

            // Load the current sessions scene object
            if (scene == null) scene = FindObjectOfType<SessionsScene>();

            // Layout the tabs
            EditorGUILayout.BeginHorizontal(new GUIStyle() { alignment = TextAnchor.UpperCenter, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) });
            GUILayout.FlexibleSpace();
            tabIndex = GUILayout.Toolbar(tabIndex, tabs, tabOptions);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            HorizontalLine();
            HorizontalLine(new Color(0, 0, 0, 0));

            switch (tabIndex)
            {
                case 0:
                    RenderScenes();
                    break;

                case 1:
                    RenderEntitiesGUI();
                    break;

                case 2:
                    RenderOscGUI();
                    break;

                case 3:
                    RenderRoutingGUI();
                    break;

                case 4:
                    RenderNetworkGUI();
                    break;

                case 5:
                    RenderXapiGUI();
                    break;
            }
        }

        #endregion OnGUI

        #region Initialize

        // Initializes an instance of the editor window
        private void Initialize()
        {
            if (isInitialized) return;

            // Hook into undo/redo events to make sure out UI updates properly
            Undo.undoRedoPerformed += () => 
            {
                this.StartCoroutine(DoPostDestroyRoutingConfig(0));
                this.StartCoroutine(DoPostDestroyEntitiesConfig(0));
                Repaint();
            };

            #region Variables & Resources

            // Whether or not the pro/dark theme is being used
            var theme = EditorGUIUtility.isProSkin ? "_Dark" : "";

            // Initialize needed variables
            oscMaps = new List<OscRangeMapFloat>();
            routingRules = new List<SessionsRoutingRule>();
            xapiActors = new List<XapiActor>();
            xapiObjects = new List<XapiObject>();
            xapiVerbs = new List<XapiVerb>();
            //deletedAssets = new List<string>();

            // Load the current sessions scene object
            scene = FindObjectOfType<SessionsScene>();

            // Icons
            oscIcon = Resources.Load<Texture2D>("Images/OscIcon" + theme);
            routingIcon = Resources.Load<Texture2D>("Images/RoutingIcon" + theme);
            networkIcon = Resources.Load<Texture2D>("Images/NetworkIcon" + theme);
            newIcon = Resources.Load<Texture2D>("Images/NewIcon" + theme);
            saveIcon = Resources.Load<Texture2D>("Images/SaveIcon" + theme);
            exportIcon = Resources.Load<Texture2D>("Images/ExportIcon" + theme);
            deleteIcon = Resources.Load<Texture2D>("Images/DeleteIcon" + theme);
            importIcon = Resources.Load<Texture2D>("Images/ImportIcon" + theme);
            addIcon = Resources.Load<Texture2D>("Images/AddIcon" + theme);
            clearIcon = Resources.Load<Texture2D>("Images/ClearIcon" + theme);
            prefabIcon = Resources.Load<Texture2D>("Images/PrefabIcon" + theme);
            copyIcon = Resources.Load<Texture2D>("Images/CopyIcon" + theme);
            pasteIcon = Resources.Load<Texture2D>("Images/PasteIcon" + theme);
            editIcon = Resources.Load<Texture2D>("Images/EditIcon" + theme);
            learnIcon = Resources.Load<Texture2D>("Images/LearnIcon" + theme);
            scenesIcon = Resources.Load<Texture2D>("Images/ScenesIcon" + theme);
            warnIcon = Resources.Load<Texture2D>("Images/WarnIcon");
            defaultScene = Resources.Load<Texture2D>("Images/DefaultScene");

            // Load the error texture resource
            errorTexture = Resources.Load<Texture2D>("Images/TextInputErrorBackground");

            scenesList = new List<SessionsSceneInfo>();

            #endregion Variables & Resources

            #region Title

            // Set window title
            titleContent = new GUIContent("Sessions", networkIcon);

            #endregion Title

            #region Styles

            // Setup styles for horizontal line break
            horizontalLineStyle = new GUIStyle();
            horizontalLineStyle.normal.background = EditorGUIUtility.whiteTexture;
            horizontalLineStyle.margin = new RectOffset(0, 0, 4, 4);
            horizontalLineStyle.fixedHeight = 1;

            #endregion Styles

            #region Tabs

            tabs = new GUIContent[]
            {
                new GUIContent("Scenes", scenesIcon),
                new GUIContent("Entities", prefabIcon),
                new GUIContent("OSC", oscIcon),
                new GUIContent("Routing", routingIcon),
                new GUIContent("Network", networkIcon),
                new GUIContent("xAPI", learnIcon),
            };

            tabOptions = null;

            tabOptions = new GUILayoutOption[]
            {
                GUILayout.ExpandWidth(false)
            };

            #endregion Tabs

            #region Restore Last Scenes Configuration

            if (scene != null)
            {
                // Get the current configuration
                var sceneConfig = scene.ScenesConfiguration;

                if (sceneConfig != null)
                {
                    // Use the name to find the configuration
                    var dirPath = GetScenesConfigPath();

                    if (!Directory.Exists(dirPath))
                    {
                        //EditorUtility.DisplayDialog("Error", "The Sessions OSC configuration folder for the project could not be found: " + dirPath, "OK");
                        return;
                    }
                    else
                    {
                        // Go through and find all of the OSC configuration files
                        var dirInfo = new DirectoryInfo(dirPath);

                        foreach (var fileInfo in dirInfo.GetFiles("*.json"))
                        {
                            if (Path.GetFileNameWithoutExtension(fileInfo.Name) == sceneConfig.name)
                            {
                                ImportScenesConfig(fileInfo.FullName, false);
                                break;
                            }
                        }
                    }
                }
            }

            #endregion Restore Last Scenes Configuration

            #region Restore Last OSC Configuration

            if (scene != null)
            {
                // Get the current configuration
                var sceneConfig = scene.OscConfiguration;

                if (sceneConfig != null)
                {
                    // Use the name to find the configuration
                    var dirPath = GetOscConfigPath();

                    if (!Directory.Exists(dirPath))
                    {
                        //EditorUtility.DisplayDialog("Error", "The Sessions OSC configuration folder for the project could not be found: " + dirPath, "OK");
                        return;
                    }
                    else
                    {
                        // Go through and find all of the OSC configuration files
                        var dirInfo = new DirectoryInfo(dirPath);

                        foreach (var fileInfo in dirInfo.GetFiles("*.json"))
                        {
                            if (Path.GetFileNameWithoutExtension(fileInfo.Name) == sceneConfig.name)
                            {
                                ImportOscConfig(fileInfo.FullName, false);
                                break;
                            }
                        }
                    }
                }
            }

            #endregion Restore Last OSC Configuration

            #region Restore Last Routing Configuration

            if (scene != null)
            {
                // Get the current configuration
                var sceneConfig = scene.RoutingConfiguration;
                if (sceneConfig != null) SelectRoutingConfig(sceneConfig, false);
            }

            #endregion Restore Last Routing Configuration

            #region Restore Last Entities Configuration

            if (scene!= null)
            {
                // Get the current configuration
                var sceneConfig = scene.EntitiesConfiguration;
                if (sceneConfig != null) SelectEntitiesConfig(sceneConfig, false);
            }

            #endregion Restore Last Routing Configuration

            #region Restore Last xAPI Configuration

            if (scene != null)
            {
                // Get the current configuration
                var sceneConfig = scene.XapiConfiguration;
                if (sceneConfig != null) SelectXapiConfig(sceneConfig, false);
            }

            #endregion Restore Last xAPI Configuration

            #region Undo/Redo HACKS

            #region SessionsSceneInfo

            // HACK to work around the fact that when our maps our recreated via a redo command we have no other way to access them to restore them
            SessionsSceneInfo.OnCreated = (sceneInfo) =>
            {
                if (sceneInfo.EditIndex < 0) sceneInfo.EditIndex = scenesList.Count;
                if (scenesList != null) scenesList.Add(sceneInfo);
                if (Current != null) Current.Repaint();
            };

            #endregion SessionsSceneInfo

            #region OscRangeMapFloat

            // HACK to work around the fact that when our maps our recreated via a redo command we have no other way to access them to restore them
            OscRangeMapFloat.OnCreated = (map) =>
            {
                if (map.EditIndex < 0) map.EditIndex = oscMaps.Count;
                if (oscMaps != null) oscMaps.Add(map);
                if (Current != null) Current.Repaint();
            };

            #endregion OscRangeMapFloat

            #region SessionsRoutingConfiguration

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            SessionsRoutingConfiguration.OnCreated = (config) =>
            {
                if (scene == null) return;
                if (scene.AllRoutingConfigurations == null) scene.AllRoutingConfigurations = new List<SessionsRoutingConfiguration>();
                if (!scene.AllRoutingConfigurations.Contains(config))
                {
                    scene.AllRoutingConfigurations.Add(config);
                    SelectRoutingConfig(config);
                }

                Repaint();
            };

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            SessionsRoutingConfiguration.OnDestroyed = (config) =>
            {
                if (scene == null) return;
                var oldConfig = (from r in scene.AllRoutingConfigurations where r != null && r.Name == config.Name select r).FirstOrDefault();
                if (oldConfig != null) scene.AllRoutingConfigurations.Remove(oldConfig);
                if (scene.AllRoutingConfigurations != null && scene.AllRoutingConfigurations.Contains(config)) scene.AllRoutingConfigurations.Remove(config);

                SelectRoutingConfig((from r in scene.AllRoutingConfigurations where r != null select r).FirstOrDefault());
                Repaint();
            };

            #endregion SessionsRoutingConfiguration

            #region SessionsRoutingRule

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            SessionsRoutingRule.OnCreated = (rule) =>
            {
                if (rule.EditIndex < 0) rule.EditIndex = routingRules.Count;
                if (routingRules == null) routingRules = new List<SessionsRoutingRule>();
                routingRules.Add(rule);
                if (SelectedRoutingConfig != null) SelectedRoutingConfig.Rules = routingRules.ToArray();
                if (Current != null) Current.Repaint();
                rule.IsExpanded = true;
                routingScrollView.y = float.MaxValue;
            };

            #endregion SessionsRoutingRule

            #region SessionsEntitiesConfiguration

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            SessionsEntitiesConfiguration.OnCreated = (config) =>
            {
                if (scene == null) return;
                if (scene.AllEntitiesConfigurations == null) scene.AllEntitiesConfigurations = new List<SessionsEntitiesConfiguration>();
                if (!scene.AllEntitiesConfigurations.Contains(config))
                {
                    scene.AllEntitiesConfigurations.Add(config);
                    SelectEntitiesConfig(config);
                }

                Repaint();
            };

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            SessionsEntitiesConfiguration.OnDestroyed = (config) =>
            {
                if (scene == null) return;
                var oldConfig = (from e in scene.AllEntitiesConfigurations where e != null && e.Name == config.Name select e).FirstOrDefault();
                if (oldConfig != null) scene.AllEntitiesConfigurations.Remove(oldConfig);

                if (scene.AllEntitiesConfigurations != null && scene.AllEntitiesConfigurations.Contains(config))
                {
                    scene.AllEntitiesConfigurations.Remove(config);
                    scene.ApplyEntitiesConfiguration();
                }

                SelectEntitiesConfig((from e in scene.AllEntitiesConfigurations where e != null select e).FirstOrDefault());
                Repaint();
            };

            #endregion SessionsEntitiesConfiguration

            #region SessionsNetworkEntityInfo

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            SessionsNetworkEntityInfo.OnCreated = (entityInfo) =>
            {
                if (entityInfo.EditIndex < 0) entityInfo.EditIndex = routingRules.Count;
                if (networkEntities == null) networkEntities = new List<SessionsNetworkEntityInfo>();
                networkEntities.Add(entityInfo);
                if (SelectedEntitiesConfig != null) SelectedEntitiesConfig.Entities = networkEntities.ToArray();
                if (Current != null) Current.Repaint();
                entitiesScrollView.y = float.MaxValue;
            };

            #endregion SessionsNetworkEntityInfo

            #region XapiConfiguration

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            XapiConfiguration.OnCreated = (config) =>
            {
                if (scene == null) return;
                if (scene.AllXapiConfigurations == null) scene.AllXapiConfigurations = new List<XapiConfiguration>();
                if (!scene.AllXapiConfigurations.Contains(config))
                {
                    scene.AllXapiConfigurations.Add(config);
                    SelectXapiConfig(config);
                }

                Repaint();
            };

            // HACK to work around the fact that when our rules our recreated via a redo command we have no other way to access them to restore them
            SessionsEntitiesConfiguration.OnDestroyed = (config) =>
            {
                if (scene == null) return;
                var oldConfig = (from e in scene.AllEntitiesConfigurations where e != null && e.Name == config.Name select e).FirstOrDefault();
                if (oldConfig != null) scene.AllEntitiesConfigurations.Remove(oldConfig);

                if (scene.AllEntitiesConfigurations != null && scene.AllEntitiesConfigurations.Contains(config))
                {
                    scene.AllEntitiesConfigurations.Remove(config);
                    scene.ApplyEntitiesConfiguration();
                }

                SelectEntitiesConfig((from e in scene.AllEntitiesConfigurations where e != null select e).FirstOrDefault());
                Repaint();
            };

            #endregion XapiConfiguration

            #endregion Undo/Redo HACKS

            isInitialized = true;
        }

        #endregion Initialize

        #endregion Window

        #region Scenes

        #region Render Scenes

        // Renders the "scenes" tab
        private void RenderScenes()
        {
            #region Toolbar

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
            HorizontalLine(new Color(0, 0, 0, 0));

            #region Select Dropdown

            // Select scene
            var dropdownStyle = new GUIStyle(EditorStyles.toolbarDropDown) { alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 0, 0), fontSize = 11 };
            if (EditorGUI.DropdownButton(new Rect(8, 36, 192, 19), new GUIContent(SelectedScenesConfig != null ? SelectedScenesConfig.Name : ""), FocusType.Keyboard, dropdownStyle))
            {
                var menu = new GenericMenu();

                // Get the current list of configurations
                var dirPath = GetScenesConfigPath();

                if (!Directory.Exists(dirPath))
                {
                    EditorUtility.DisplayDialog("Error", "The Sessions Scenes configuration folder for the project could not be found: " + dirPath, "OK");
                }
                else
                {
                    // Go through and find all of the OSC configuration files
                    var dirInfo = new DirectoryInfo(dirPath);
                    var filesByName = new SortedDictionary<string, SessionsScenesConfiguration>();

                    foreach (var fileInfo in dirInfo.GetFiles("*.json"))
                    {
                        // Try and parse the file as a valid configuration file
                        try
                        {
                            var json = File.ReadAllText(fileInfo.FullName);
                            var config = JsonConvert.DeserializeObject<DataSessionsScenesConfiguration>(json);

                            // If we loaded a valid configuration, get its name
                            if (config != null)
                            {
                                var name = !string.IsNullOrEmpty(config.Name) ? config.Name : fileInfo.Name;

                                if (!filesByName.ContainsKey(name))
                                {
                                    var converted = SessionsScenesConfiguration.ToScriptable(config);
                                    converted.FilePath = fileInfo.FullName;
                                    filesByName.Add(name, converted);
                                }
                                else
                                {
                                    Debug.LogWarningFormat("Duplicate Sessions Scene configuration name detected: '{0}' for file {1}", name, fileInfo.FullName);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }

                    foreach (var pair in filesByName)
                    {
                        // Add a menu item for it
                        menu.AddItem(new GUIContent(pair.Key), false, () =>
                        {
                            //if (SelectedScenesConfig != null && SelectedScenesConfig.Name == pair.Value.Name) return;
                            SelectScenesConfig(pair.Value);
                        });
                    }
                }

                menu.ShowAsContext();
            }

            #endregion Config Select Dropdown

            var btnX = 210;
            var btnY = 35;

            #region New

            // Create new configuration
            if (GUI.Button(new Rect(btnX, btnY, 24, 19), new GUIContent(newIcon, "New Scenes configuration")))
            {
                var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                nameDialog.UserInput = "My Scenes Config";
                nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                nameDialog.DialogTitle = new GUIContent("New Scenes Configuration");
                nameDialog.DialogContent = new GUIContent("Enter the name of the new Scenes configuration.");
                nameDialog.DialogOKButtonLabel = new GUIContent("Create");
                nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                nameDialog.OnDialogAccepted = () =>
                {
                    // Get the user input
                    var name = nameDialog.UserInput;
                    if (string.IsNullOrEmpty(name)) return false;

                    // Ensure unique name
                    if (!EnsureUniqueOscConfigName(name))
                    {
                        var errorMsg = string.Format("The Scenes configuration name '{0}' is already in use. Please choose a different name.", name);
                        EditorUtility.DisplayDialog("Name in Use", errorMsg, "OK");
                        return false;
                    }

                    var config = CreateInstance<SessionsScenesConfiguration>();
                    config.Name = name;
                    SelectScenesConfig(config); // TODO add
                    Debug.LogFormat("[Sessions] Created new Scenes configuration: {0}", name);
                    AddNewSceneInfo(); // add single by default
                    return true;
                };

                nameDialog.ShowAuxWindow();
            }

            #endregion New

            #region Save

            // Delete configuration
            if (SelectedScenesConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 32, btnY, 24, 19), new GUIContent(saveIcon, "Save current Scenes configuration")))
            {
                if (SelectedOscConfig == null) return;
                var savePath = GetScenesConfigPath() + Path.DirectorySeparatorChar + GetSafeFilename(SelectedScenesConfig.Name).Replace(" ", "") + ".json";
                SaveScenesConfig(savePath);
                ShowNotification(new GUIContent("Scenes configuration file saved"));
            }
            GUI.enabled = true;

            #endregion Save

            #region Rename

            // Rename configuration
            if (SelectedScenesConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 64, btnY, 24, 19), new GUIContent(editIcon, "Rename the current scenes configuration")))
            {
                var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                nameDialog.UserInput = SelectedScenesConfig.Name;
                nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                nameDialog.DialogTitle = new GUIContent("Rename Scenes Configuration");
                nameDialog.DialogContent = new GUIContent("The name of the Scenes configuration.");
                nameDialog.DialogOKButtonLabel = new GUIContent("Rename");
                nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                nameDialog.OnDialogAccepted = () =>
                {
                    // Get the user input
                    var name = nameDialog.UserInput;
                    if (string.IsNullOrEmpty(name)) return false;

                    if (SelectedScenesConfig.Name == name) return true; // no update

                    // Ensure unique name
                    if (!EnsureUniqueScenesConfigName(name))
                    {
                        var errorMsg = string.Format("The Scenes configuration name '{0}' is already in use. Please choose a different name.", name);
                        EditorUtility.DisplayDialog("Name in Use", errorMsg, "OK");
                        return false;
                    }
                    else
                    {
                        var dirPath = GetScenesConfigPath();
                        var oldFileName = GetSafeFilename(SelectedScenesConfig.Name).Replace(" ", "") + ".json";
                        var newFileName = GetSafeFilename(name).Replace(" ", "") + ".json";

                        // Update name
                        SelectedScenesConfig.Name = name;

                        // Save the current file with the new name
                        SaveScenesConfig(Path.Combine(dirPath, oldFileName));

                        try
                        {
                            // Rename the file on disk
                            File.Move(Path.Combine(dirPath, oldFileName), Path.Combine(dirPath, newFileName));

                            var metaFile = Path.Combine(dirPath, oldFileName) + ".meta";
                            if (File.Exists(metaFile)) File.Delete(metaFile);

                            // Update to new file path
                            SelectedScenesConfig.FilePath = Path.Combine(dirPath, newFileName);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }

                        return true;
                    }
                };

                nameDialog.ShowAuxWindow();
            }
            GUI.enabled = true;

            #endregion Rename

            #region Delete

            // Delete configuration
            if (SelectedScenesConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 96, btnY, 24, 19), new GUIContent(deleteIcon, "Delete current Scenes configuration")))
            {
                var deleteMsg = string.Format("Are you sure you want to delete Scenes configuration '{0}'?\n\nThere is no undo.", SelectedScenesConfig.Name);
                if (EditorUtility.DisplayDialog("Confirm Delete", deleteMsg, "Delete", "Cancel")) DeleteScenesConfig();
            }
            GUI.enabled = true;

            #endregion Delete

            #region Add Scene

            // Add rule
            if (GUI.Button(new Rect(btnX + 128, btnY, 24, 19), new GUIContent(addIcon, "Add a new scene")))
            {
                AddNewSceneInfo();
            }

            #endregion Add Scene

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            #endregion Toolbar

            #region Edit Area

            HorizontalLine();

            scenesScrollView = EditorGUILayout.BeginScrollView(scenesScrollView);

            #region Table Body

            var deletedScenes = new List<SessionsSceneInfo>();
            if (SelectedScenesConfig != null)
            {
                var valueList = (from s in scenesList where s != null select s).ToArray();

                #region List

                // Make sure values are sorted correct (more undo/redo HACKs yay!)
                foreach (var sceneInfo in valueList)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Create a scriptable object out of the current routing rule
                    SerializedObject sobj = new SerializedObject(sceneInfo);

                    #region Delete

                    var deleteStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(4, 4, 2, 2), margin = new RectOffset(4, 4, 2, 4) };
                    if (GUILayout.Button(new GUIContent(deleteIcon, "Delete this scene"), deleteStyle, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        deletedScenes.Add(sceneInfo);
                    }

                    #endregion Delete

                    #region Row

                    // Get the current image URL
                    var lastImageUrl = sceneInfo.ImageUrl;

                    try
                    {
                        // Draw the scene image/icon
                        EditorGUILayout.LabelField(new GUIContent(sceneInfo.Image != null ? sceneInfo.Image : defaultScene), GUILayout.Width(128), GUILayout.Height(128));
                        EditorGUILayout.BeginVertical();

                        Color cachedGuiColor = GUI.color;
                        sobj.Update();
                        var property = sobj.GetIterator();
                        var next = property.NextVisible(true);
                        if (next)
                            do
                            {
                                GUI.color = cachedGuiColor;
                                bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                bool cachedGUIEnabled = GUI.enabled;
                                if (isdefaultScriptProperty) continue; // don't render

                                if (property.name == "Name")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("Name", "The name of the scene. Must be unique."), GUILayout.Width(42));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(160));
                                }
                                else if (property.name == "Image")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("Image", "The image to use to represent the scene."), GUILayout.Width(42));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(160));
                                }
                                else if (property.name == "ImageUrl")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("Image URL", "The optional URL of the scene's image."), GUILayout.Width(72));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(256));
                                }

                                if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                            } while (property.NextVisible(false));
                        sobj.ApplyModifiedProperties();
                    }
                    catch { }

                    // If the image URL was updated and is a valid URL
                    if (sceneInfo.ImageUrl != lastImageUrl && Uri.IsWellFormedUriString(sceneInfo.ImageUrl, UriKind.RelativeOrAbsolute))
                    {
                        // Stop any current coroutine
                        this.StopCoroutine(DoLoadSceneImage(sceneInfo));

                        // Start new coroutine
                        this.StartCoroutine(DoLoadSceneImage(sceneInfo));
                    }

                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space();

                    EditorGUILayout.BeginVertical();

                    try
                    {
                        Color cachedGuiColor = GUI.color;
                        sobj.Update();
                        var property = sobj.GetIterator();
                        var next = property.NextVisible(true);
                        if (next)
                            do
                            {
                                GUI.color = cachedGuiColor;
                                bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                bool cachedGUIEnabled = GUI.enabled;
                                if (isdefaultScriptProperty) continue; // don't render

                                if (property.name == "Url")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("URL", "The URL of the scene. Must be unique."), GUILayout.Width(42));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(256));
                                }
                                else if (property.name == "Info")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("Info", "A description of the scene."), GUILayout.Width(42));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(256));
                                }

                                if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                            } while (property.NextVisible(false));
                        sobj.ApplyModifiedProperties();
                    }
                    catch { }

                    EditorGUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    HorizontalLine();

                    #endregion Row
                }

                #endregion List
            }

            #endregion Table Body

            #region Process Deleted

            // Clean up deleted 
            var scenesDeleted = deletedScenes.Count > 0;
            foreach (var sceneInfo in deletedScenes)
            {
                Undo.DestroyObjectImmediate(sceneInfo);
                scenesList.Remove(sceneInfo);
                SelectedScenesConfig.Scenes = scenesList.ToArray();
            }

            if (scenesDeleted) Repaint();
            scenesList = new List<SessionsSceneInfo>((from s in scenesList where s != null select s));

            #endregion Process Deleted

            EditorGUILayout.EndScrollView();

            #endregion Edit Area
        }

        #endregion Render Scenes

        #region Utility

        #region Select Scenes Config

        // Selects an entities configuration
        private void SelectScenesConfig(SessionsScenesConfiguration config, bool applyToScene = true)
        {
            //Debug.Log("Selecting: " + (config != null ? config.Name : "NULL"));
            SelectedScenesConfig = config;

            if (config != null)
            {
                if (scene != null)
                {
                    var assetName = GetSafeFilename(config.Name).Replace(" ", "");

                    // Load the resource and assign
                    var assetPath = "Scenes/" + assetName;
                    var selectedAsset = Resources.Load<TextAsset>(assetPath);

                    if (selectedAsset == null)
                    {
                        Debug.LogWarningFormat("Sessions Editor could not find Scenes configuration resource: " + assetPath);
                    }
                    // If the asset was found, assign it to the OSC controller in the scene
                    else if (applyToScene)
                    {
                        //Debug.LogFormat("Apply OSC value controller configuration to scene...");
                        scene.ScenesConfiguration = selectedAsset;
                        scene.ApplyScenesConfiguration();
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }
                }
            }

            if (scenesList == null) scenesList = new List<SessionsSceneInfo>();
            else scenesList.Clear();
            if (config != null && config.Scenes != null && config.Scenes.Length > 0) scenesList.AddRange(config.Scenes);
            Repaint();
        }

        private IEnumerator DoSelectScenesConfig(SessionsScenesConfiguration config, float delay = 0.5f)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            SelectScenesConfig(config);
            yield break;
        }

        #endregion Select Scenes Config

        #region Get Scenes Config Path

        // Returns the Scenes config asset folder
        private string GetScenesConfigPath()
        {
            var sc = Path.DirectorySeparatorChar;
            return Directory.GetCurrentDirectory() + sc + "Assets" + sc + "CymaticLabs" + sc + "Sessions" + sc + "Resources" + sc + "Scenes";
        }

        #endregion Get Scenes Config Path

        #region Import Scenes Config

        // Imports an Scenes configuration file
        private void ImportScenesConfig(string filepath, bool applyToScene = true)
        {
            try
            {
                // Read in the file JSON
                var json = File.ReadAllText(filepath);

                // Deserialize
                var importedConfig = JsonConvert.DeserializeObject<DataSessionsScenesConfiguration>(json);

                // Since we didn't properly instantiate these as scriptable objects, let's do that now and copy over
                var converted = SessionsScenesConfiguration.ToScriptable(importedConfig);
                SelectScenesConfig(converted, applyToScene);
                //Debug.LogFormat("Scenes configuration file imported from: {0}", filepath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        #endregion Import OSC Config

        #region Delete Scenes Config

        // Deletes the current scenes config
        private void DeleteScenesConfig()
        {
            if (SelectedScenesConfig == null) return;

            try
            {
                if (string.IsNullOrEmpty(SelectedScenesConfig.FilePath))
                {
                    Debug.LogErrorFormat("Selected Scenes configuration has no file path reference");
                    return;
                }

                File.Delete(SelectedScenesConfig.FilePath);
                var metaFile = SelectedScenesConfig.FilePath + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                SelectScenesConfig(null); // clear the current selection
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        #endregion Delete Entities Config

        #region Save Scenes Config

        // Saves the current configuration to the specified path
        private void SaveScenesConfig(string savePath)
        {
            if (!string.IsNullOrEmpty(savePath))
            {
                var config = SelectedScenesConfig;

                // Copy all of the values from the scriptable version to the non-scriptable for deserialization
                var exportConfig = new DataSessionsScenesConfiguration();
                exportConfig.Name = config.Name;
                exportConfig.Version = config.Version;

                var valueList = new List<DataSessionsSceneInfo>();

                foreach (var s in scenesList.ToArray())
                {
                    if (s == null) continue;
                    var scene = new DataSessionsSceneInfo();
                    if (s.Image != null) scene.ImageRes = AssetDatabase.GetAssetPath(s.Image);
                    scene.ImageUrl = s.ImageUrl;
                    scene.Info = s.Info;
                    scene.Name = s.Name;
                    scene.Url = s.Url;
                    valueList.Add(scene);
                }

                exportConfig.Scenes = valueList.ToArray();

                File.WriteAllText(savePath, JsonConvert.SerializeObject(exportConfig, Formatting.Indented));
                Debug.LogFormat("Scenes configuration file saved to: {0}", savePath);
                config.FilePath = savePath;
            }
        }

        #endregion Save Scenes Config

        #region Add Scene Info

        // Adds a new scene info
        private void AddNewSceneInfo()
        {
            var s = CreateInstance<SessionsSceneInfo>();
            s.Info = "The default lobby scene.";
            s.Name = "The Lobby";
            s.Url = "app://SessionsLobby";
            Undo.RegisterCreatedObjectUndo(s, "Sessions Scene Info Created");
            Repaint();
            oscScrollViewV.y = float.MaxValue;
        }

        #endregion Add Scene Info

        #region Ensure Unique Name

        // Ensures an Scenes configuration name is unique
        private bool EnsureUniqueScenesConfigName(string name)
        {
            var dirPath = GetScenesConfigPath();

            if (!Directory.Exists(dirPath))
            {
                Debug.LogWarningFormat("Sessions Scenes configuration path not found: {0}", dirPath);
                return false;
            }

            var dirInfo = new DirectoryInfo(dirPath);
            var fileName = GetSafeFilename(name).Replace(" ", "") + ".json";

            foreach (var fileInfo in dirInfo.GetFiles("*.json"))
            {
                if (fileInfo.Extension.ToLower() == ".meta") continue;
                if (fileInfo.Name.ToLower() == fileName.ToLower()) return false;
            }

            return true;
        }

        #endregion Ensure Unique Name

        #region Load Scene Image

        // Loads the image of a scene
        private IEnumerator DoLoadSceneImage(SessionsSceneInfo sceneInfo)
        {
            yield return new WaitForSeconds(1.0f);
            var www = new WWW(sceneInfo.ImageUrl);
            yield return www;
            sceneInfo.Image = www.texture;
            Repaint();
        }

        #endregion Load Scene Image

        #endregion Utility

        #endregion Scenes

        #region OSC

        #region Render OSC GUI

        // Renders the OSC "tab" GUI        
        private void RenderOscGUI()
        {
            #region Toolbar

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
            HorizontalLine(new Color(0, 0, 0, 0));

            #region Config Select Dropdown

            // Select configuration
            var dropdownStyle = new GUIStyle(EditorStyles.toolbarDropDown) { alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 0, 0), fontSize = 11 };
            if (EditorGUI.DropdownButton(new Rect(8, 36, 192, 19), new GUIContent(SelectedOscConfig != null ? SelectedOscConfig.Name : ""), FocusType.Keyboard, dropdownStyle))
            {
                var menu = new GenericMenu();

                // Get the current list of configurations
                var dirPath = GetOscConfigPath();

                if (!Directory.Exists(dirPath))
                {
                    EditorUtility.DisplayDialog("Error", "The Sessions OSC configuration folder for the project could not be found: " + dirPath, "OK");
                }
                else
                {
                    // Go through and find all of the OSC configuration files
                    var dirInfo = new DirectoryInfo(dirPath);
                    var filesByName = new SortedDictionary<string, SessionsOscConfiguration>();

                    foreach (var fileInfo in dirInfo.GetFiles("*.json"))
                    {
                        // Try and parse the file as a valid configuration file
                        try
                        {
                            var json = File.ReadAllText(fileInfo.FullName);
                            var config = JsonConvert.DeserializeObject<DataSessionsOscConfiguration>(json);

                            // If we loaded a valid configuration, get its name
                            if (config != null)
                            {
                                var name = !string.IsNullOrEmpty(config.Name) ? config.Name : fileInfo.Name;

                                if (!filesByName.ContainsKey(name))
                                {
                                    var converted = ConvertToScriptableOscConfig(config);
                                    converted.FilePath = fileInfo.FullName;
                                    filesByName.Add(name, converted);
                                }
                                else
                                {
                                    Debug.LogWarningFormat("Duplicate Sessions OSC configuration name detected: '{0}' for file {1}", name, fileInfo.FullName);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }

                    foreach (var pair in filesByName)
                    {
                        // Add a menu item for it
                        menu.AddItem(new GUIContent(pair.Key), false, () => { SelectOscConfig(pair.Value); });
                    }
                }

                menu.ShowAsContext();
            }

            #endregion Config Select Dropdown

            var btnX = 210;
            var btnY = 35;

            #region New

            // Create new configuration
            if (GUI.Button(new Rect(btnX, btnY, 24, 19), new GUIContent(newIcon, "New OSC configuration")))
            {
                var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                nameDialog.DialogTitle = new GUIContent("New OSC Configuration");
                nameDialog.DialogContent = new GUIContent("Enter the name of the new OSC configuration.");
                nameDialog.DialogOKButtonLabel = new GUIContent("Create");
                nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                nameDialog.OnDialogAccepted = () =>
                {
                    // Get the user input
                    var name = nameDialog.UserInput;
                    if (string.IsNullOrEmpty(name)) return false;

                    // Ensure unique name
                    if (!EnsureUniqueOscConfigName(name))
                    {
                        var errorMsg = string.Format("The OSC configuration name '{0}' is already in use. Please choose a different name.", name);
                        EditorUtility.DisplayDialog("Name in Use", errorMsg, "OK");
                        return false;
                    }

                    var config = new SessionsOscConfiguration();
                    config.Name = name;
                    config.AllowedFloats = new OscRangeMapFloat[0];
                    SelectOscConfig(config);
                    Debug.LogFormat("[Sessions] Created new OSC configuration: {0}", name);
                    AddNewOscMapping(); // a a default mapping
                    return true;
                };

                nameDialog.ShowAuxWindow();
            }

            #endregion New

            #region Save

            // Delete configuration
            if (SelectedOscConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 32, btnY, 24, 19), new GUIContent(saveIcon, "Save current OSC configuration")))
            {
                if (SelectedOscConfig == null) return;
                var savePath = GetOscConfigPath() + Path.DirectorySeparatorChar + GetSafeFilename(SelectedOscConfig.Name).Replace(" ", "") + ".json";
                SaveOscConfig(savePath);
                ShowNotification(new GUIContent("OSC configuration file saved"));
            }
            GUI.enabled = true;

            #endregion Save

            #region Rename

            // Rename configuration
            if (SelectedRoutingConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 64, btnY, 24, 19), new GUIContent(editIcon, "Rename the current routing configuration")))
            {
                var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                nameDialog.UserInput = SelectedOscConfig.Name;
                nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                nameDialog.DialogTitle = new GUIContent("Rename OSC Configuration");
                nameDialog.DialogContent = new GUIContent("The name of the OSC configuration.");
                nameDialog.DialogOKButtonLabel = new GUIContent("Rename");
                nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                nameDialog.OnDialogAccepted = () =>
                {
                    // Get the user input
                    var name = nameDialog.UserInput;
                    if (string.IsNullOrEmpty(name)) return false;

                    if (SelectedOscConfig.Name == name) return true; // no update

                    // Ensure unique name
                    if (!EnsureUniqueOscConfigName(name))
                    {
                        var errorMsg = string.Format("The OSC configuration name '{0}' is already in use. Please choose a different name.", name);
                        EditorUtility.DisplayDialog("Name in Use", errorMsg, "OK");
                        return false;
                    }
                    else
                    {
                        var dirPath = GetOscConfigPath();
                        var oldFileName = GetSafeFilename(SelectedOscConfig.Name).Replace(" ", "") + ".json";
                        var newFileName = GetSafeFilename(name).Replace(" ", "") + ".json";

                        // Update name
                        SelectedOscConfig.Name = name;

                        // Save the current file with the new name
                        SaveOscConfig(Path.Combine(dirPath, oldFileName));

                        try
                        {
                            // Rename the file on disk
                            File.Move(Path.Combine(dirPath, oldFileName), Path.Combine(dirPath, newFileName));

                            var metaFile = Path.Combine(dirPath, oldFileName) + ".meta";
                            if (File.Exists(metaFile)) File.Delete(metaFile);

                            // Update to new file path
                            SelectedOscConfig.FilePath = Path.Combine(dirPath, newFileName);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }

                        return true;
                    }
                };

                nameDialog.ShowAuxWindow();
            }
            GUI.enabled = true;

            #endregion Rename

            #region Import

            // Import configuration
            if (GUI.Button(new Rect(btnX + 96, btnY, 24, 19), new GUIContent(importIcon, "Import OSC configuration from file")))
            {
                var openPath = EditorUtility.OpenFilePanel("Import OSC Configuration File", Directory.GetCurrentDirectory(), "json");
                if (string.IsNullOrEmpty(openPath)) return;
                ImportOscConfig(openPath);
            }

            #endregion Import

            #region Export

            // Export configuration
            if (SelectedOscConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 128, btnY, 24, 19), new GUIContent(exportIcon, "Export current OSC configuration to file")))
            {
                if (SelectedOscConfig == null) return;
                var savePath = EditorUtility.SaveFilePanel("Export OSC Configuration File", Directory.GetCurrentDirectory(), GetSafeFilename(SelectedOscConfig.Name), "json");
                SaveOscConfig(savePath);
            }
            GUI.enabled = true;

            #endregion Export

            #region Delete

            // Delete configuration
            if (SelectedOscConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 160, btnY, 24, 19), new GUIContent(deleteIcon, "Delete current OSC configuration")))
            {
                var deleteMsg = string.Format("Are you sure you want to delete OSC configuration '{0}'?\n\nThere is no undo.", SelectedOscConfig.Name);
                if (EditorUtility.DisplayDialog("Confirm Delete", deleteMsg, "Delete", "Cancel")) DeleteOscConfig();
            }
            GUI.enabled = true;

            #endregion Delete

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            #endregion Toolbar

            #region Edit Area

            #region Table Header

            // Create the table header
            oscScrollViewH = EditorGUILayout.BeginScrollView(oscScrollViewH, GUI.skin.horizontalScrollbar, GUIStyle.none);
            EditorGUILayout.BeginHorizontal();
            var tableHeaderStyle = new GUIStyle(GUI.skin.box) { normal = { textColor = Color.white }, margin = new RectOffset(0, 0, 0, 0) };
            var tableButtonHeaderStyle = new GUIStyle(EditorStyles.miniButton) { normal = { textColor = Color.white }, margin = new RectOffset(4, 4, 0, 0), padding = new RectOffset(2, 2, 4, 4) };

            // Add new mapping
            if (SelectedOscConfig == null) GUI.enabled = false;
            if (GUILayout.Button(addIcon, tableButtonHeaderStyle, GUILayout.Width(20), GUILayout.Height(19)))
            {
                AddNewOscMapping();
            }
            GUI.enabled = true;

            GUILayout.Box("Name", tableHeaderStyle, GUILayout.Width(160));
            GUILayout.Box("Address", tableHeaderStyle, GUILayout.Width(160));
            GUILayout.Box("Arg Index", tableHeaderStyle, GUILayout.Width(72));
            GUILayout.Box("Clamp Input", tableHeaderStyle, GUILayout.Width(96));
            GUILayout.Box("Min Input", tableHeaderStyle, GUILayout.Width(72));
            GUILayout.Box("Max Input", tableHeaderStyle, GUILayout.Width(72));
            GUILayout.Box("Scale Output", tableHeaderStyle, GUILayout.Width(96));
            GUILayout.Box("Min Output", tableHeaderStyle, GUILayout.Width(72));
            GUILayout.Box("Max Output", tableHeaderStyle, GUILayout.Width(80));
            GUILayout.Box("Reliable", tableHeaderStyle, GUILayout.Width(72));
            GUILayout.Box("No Relay", tableHeaderStyle, GUILayout.Width(72));
            GUILayout.Box("", tableHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            #endregion Table Header

            #region Table Body

            // Start scrolling area for OSC value mapping list
            oscScrollViewV = EditorGUILayout.BeginScrollView(oscScrollViewV, GUIStyle.none, GUI.skin.verticalScrollbar);

            if (SelectedOscConfig != null)
            {
                var textFieldStyle = new GUIStyle(GUI.skin.textField) { margin = new RectOffset(0, 0, 2, 2) };
                var errorState = new GUIStyleState() { background = errorTexture, textColor = Color.black };
                var textFieldErrorStyle = new GUIStyle(textFieldStyle) { active = errorState, normal = errorState, focused = errorState, hover = errorState };
                var toggleStyle = new GUIStyle(EditorStyles.toggle) { margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0) };
                var valueList = (from m in oscMaps where m != null select m).OrderBy(m => m.EditIndex).ToArray();

                #region Render Mappings List

                // Make sure values are sorted correct (more undo/redo HACKs yay!)
                foreach (var map in valueList)
                {
                    EditorGUILayout.BeginHorizontal();

                    #region Delete

                    var deleteStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(4, 4, 2, 2), margin = new RectOffset(4, 4, 2, 4) };
                    if (GUILayout.Button(deleteIcon, deleteStyle, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        oscMaps.Remove(map);
                        Undo.DestroyObjectImmediate(map);
                    }

                    #endregion Delete

                    // Layout mapping editable fields

                    #region Name

                    // Name
                    EditorGUI.BeginChangeCheck();
                    var nameStyle = !string.IsNullOrEmpty(map.Name) ? textFieldStyle : textFieldErrorStyle;
                    var name = EditorGUILayout.TextField(map.Name, nameStyle, GUILayout.Width(160));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(map, "Edited OSC Map Name");
                        map.Name = name;
                    }

                    #endregion Name

                    #region Address

                    // Address
                    EditorGUI.BeginChangeCheck();
                    var addrStyle = !string.IsNullOrEmpty(map.Address) ? textFieldStyle : textFieldErrorStyle;
                    var address = EditorGUILayout.TextField(map.Address, addrStyle, GUILayout.Width(160));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(map, "Edited OSC Map Address");
                        map.Address = address;
                    }

                    #endregion Address

                    #region Argument Index

                    // Argument Index
                    EditorGUI.BeginChangeCheck();
                    var argIndexStr = EditorGUILayout.TextField(map.ArgumentIndex.ToString(), textFieldStyle, GUILayout.Width(72));
                    int argIndex; if (int.TryParse(argIndexStr, out argIndex))
                    {
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(map, "Edited OSC Map Argument Index");
                            map.ArgumentIndex = argIndex;
                        }
                    }

                    #endregion Argument Index

                    #region Clamp Input

                    // Clamp Input
                    EditorGUILayout.LabelField("", GUILayout.Width(32));
                    EditorGUI.BeginChangeCheck();
                    var clampInput = EditorGUILayout.Toggle(map.ClampInput, toggleStyle, GUILayout.Width(52));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(map, "Edited OSC Map Clamp Input");
                        map.ClampInput = clampInput;
                    }

                    #endregion Clamp Input

                    #region Min Input

                    // Min Input
                    EditorGUI.BeginChangeCheck();
                    var minInputStr = EditorGUILayout.TextField(map.MinInputValue.ToString(), textFieldStyle, GUILayout.Width(72));
                    float minInput; if (float.TryParse(minInputStr, out minInput))
                    {
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(map, "Edited OSC Map Min Input Value");
                            map.MinInputValue = minInput;
                        }
                    }

                    #endregion Min Input

                    #region Max Input

                    // Max Input
                    EditorGUI.BeginChangeCheck();
                    var maxInputStr = EditorGUILayout.TextField(map.MaxInputValue.ToString(), textFieldStyle, GUILayout.Width(72));
                    float maxInput; if (float.TryParse(maxInputStr, out maxInput))
                    {
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(map, "Edited OSC Map Max Input Value");
                            map.MaxInputValue = maxInput;
                        }
                    }

                    #endregion Max Input

                    #region Scale Output

                    // Scale Output
                    EditorGUILayout.LabelField("", GUILayout.Width(32));
                    EditorGUI.BeginChangeCheck();
                    var scaleOutput = EditorGUILayout.Toggle(map.ScaleOutput, toggleStyle, GUILayout.Width(52));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(map, "Edited OSC Map Scale Output Value");
                        map.ScaleOutput = scaleOutput;
                    }

                    #endregion Scale Output

                    #region Min Output

                    // Min Output
                    EditorGUI.BeginChangeCheck();
                    var minOutputStr = EditorGUILayout.TextField(map.MinOutputValue.ToString(), textFieldStyle, GUILayout.Width(72));
                    float minOutput; if (float.TryParse(minOutputStr, out minOutput))
                    {
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(map, "Edited OSC Map Min Output Value");
                            map.MinOutputValue = minOutput;
                        }
                    }

                    #endregion Min Output

                    #region Max Output

                    // Max Output
                    EditorGUI.BeginChangeCheck();
                    var maxOutputStr = EditorGUILayout.TextField(map.MaxOutputValue.ToString(), textFieldStyle, GUILayout.Width(80));
                    float maxOutput; if (float.TryParse(maxOutputStr, out maxOutput))
                    {
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(map, "Edited OSC Map Max Output Value");
                            map.MaxOutputValue = maxOutput;
                        }
                    }

                    #endregion Max Output

                    #region Reliable

                    // Reliable
                    EditorGUILayout.LabelField("", GUILayout.Width(20));
                    EditorGUI.BeginChangeCheck();
                    var reliable = EditorGUILayout.Toggle(map.Reliable, toggleStyle, GUILayout.Width(44));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(map, "Edited OSC Map Reliable");
                        map.Reliable = reliable;
                    }

                    #endregion Reliable

                    #region No Broadcast

                    EditorGUILayout.LabelField("", GUILayout.Width(16));

                    // No Broadcast
                    EditorGUI.BeginChangeCheck();
                    var noBroadcast = EditorGUILayout.Toggle(map.NoBroadcast, toggleStyle, GUILayout.Width(40));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(map, "Edited OSC Map NoBroadcast");
                        map.NoBroadcast = noBroadcast;
                    }

                    #endregion No Broadcast

                    EditorGUILayout.EndHorizontal();
                }

                #endregion Render Mappings List
            }

            #endregion Table Body

            // Clean up deleted maps
            //foreach (var map in deleteMapList) oscMaps.Remove(map);
            oscMaps = new List<OscRangeMapFloat>((from m in oscMaps where m != null select m));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndScrollView();

            #endregion Edit Area
        }

        #endregion Render OSC GUI

        #region Utility

        #region Convert to ScriptableOscConfiguration

        // Copies form a non-scriptbale to a scriptable OSC config
        private SessionsOscConfiguration ConvertToScriptableOscConfig(DataSessionsOscConfiguration config)
        {
            var c = CreateInstance<SessionsOscConfiguration>();
            c.Name = config.Name;
            c.Version = config.Version;

            var valueList = new List<OscRangeMapFloat>(config.AllowedFloats != null ? config.AllowedFloats.Length : 0);

            if (config.AllowedFloats != null)
            {
                foreach (var m in config.AllowedFloats)
                {
                    // Copy over to a scriptable version
                    var map = CreateInstance<OscRangeMapFloat>();
                    map.Name = m.Name;
                    map.Address = m.Address;
                    map.ArgumentIndex = m.ArgumentIndex;
                    map.ClampInput = m.ClampInput;
                    map.MinInputValue = m.MinInputValue;
                    map.MaxInputValue = m.MaxInputValue;
                    map.ScaleOutput = m.ScaleOutput;
                    map.MinOutputValue = m.MinOutputValue;
                    map.MaxOutputValue = m.MaxOutputValue;
                    map.Reliable = m.Reliable;
                    map.NoBroadcast = m.NoBroadcast;
                    valueList.Add(map);
                }
            }

            // Add to the new configuration
            c.AllowedFloats = valueList.ToArray();
            return c;
        }

        #endregion Convert to ScriptableOscConfiguration

        #region Select OSC Config

        // Selects an OSC configuration
        private void SelectOscConfig(SessionsOscConfiguration config, bool applyToScene = true)
        {
            //Debug.Log("Selecting: " + (config != null ? config.Name : "NULL"));
            SelectedOscConfig = config;

            if (config != null)
            {
                if (scene != null)
                {
                    var assetName = GetSafeFilename(config.Name).Replace(" ", "");

                    // Load the resource and assign
                    var assetPath = "OSC/" + assetName;
                    var selectedAsset = Resources.Load<TextAsset>(assetPath);

                    if (selectedAsset == null)
                    {
                        Debug.LogWarningFormat("Sessions Editor could not find OSC configuration resource: " + assetPath);
                    }
                    // If the asset was found, assign it to the OSC controller in the scene
                    else if (applyToScene)
                    {
                        //Debug.LogFormat("Apply OSC value controller configuration to scene...");
                        scene.OscConfiguration = selectedAsset;
                        scene.ApplyOscConfiguration();
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }
                }
            }

            if (oscMaps == null) oscMaps = new List<OscRangeMapFloat>();
            else oscMaps.Clear();
            if (config != null && config.AllowedFloats != null && config.AllowedFloats.Length > 0) oscMaps.AddRange(config.AllowedFloats);
            Repaint();
        }

        #endregion Select OSC Config

        #region Get OSC Config Path

        // Returns the OSC config asset folder
        private string GetOscConfigPath()
        {
            var sc = Path.DirectorySeparatorChar;
            return Directory.GetCurrentDirectory() + sc + "Assets" + sc + "CymaticLabs" + sc + "Sessions" + sc + "Resources" + sc + "OSC";
        }

        #endregion Get OSC Config Path

        #region Add OSC Mapping

        // Adds a new OSC value mapping
        private void AddNewOscMapping()
        {
            var map = CreateInstance<OscRangeMapFloat>();
            map.Name = "Env/Lights/0/Intensity";
            map.Address = "/1/scene/1";
            map.ArgumentIndex = 0;
            map.ClampInput = false;
            map.MinInputValue = 0;
            map.MaxInputValue = 1;
            map.ScaleOutput = false;
            map.MinOutputValue = 0;
            map.MaxOutputValue = 1;
            map.Reliable = false;
            map.NoBroadcast = false;
            Undo.RegisterCreatedObjectUndo(map, "OSC Value Map Created");
            Repaint();
            oscScrollViewV.y = float.MaxValue;
        }

        #endregion Add OSC Mapping

        #region Save OSC Config

        // Saves the current configuration to the specified path
        private void SaveOscConfig(string savePath)
        {
            if (!string.IsNullOrEmpty(savePath))
            {
                var config = SelectedOscConfig;

                // Copy all of the values from the scriptable version to the non-scriptable for deserialization
                var exportConfig = new DataSessionsOscConfiguration();
                exportConfig.Name = config.Name;
                exportConfig.Version = config.Version;

                var valueList = new List<DataOscRangeMapFloat>();

                foreach (var m in oscMaps.ToArray())
                {
                    if (m == null) continue;
                    var map = new DataOscRangeMapFloat();
                    map.Address = m.Address;
                    map.ArgumentIndex = m.ArgumentIndex;
                    map.ClampInput = m.ClampInput;
                    map.MaxInputValue = m.MaxInputValue;
                    map.MaxOutputValue = m.MaxOutputValue;
                    map.MinInputValue = m.MinInputValue;
                    map.MinOutputValue = m.MinOutputValue;
                    map.Name = m.Name;
                    map.NoBroadcast = m.NoBroadcast;
                    map.Reliable = m.Reliable;
                    map.ScaleOutput = m.ScaleOutput;
                    valueList.Add(map);
                }

                exportConfig.AllowedFloats = valueList.ToArray();

                File.WriteAllText(savePath, JsonConvert.SerializeObject(exportConfig, Formatting.Indented));
                Debug.LogFormat("OSC configuration file saved to: {0}", savePath);
                config.FilePath = savePath;
            }
        }

        #endregion Save OSC Config

        #region Delete OSC Config

        // Deletes an OSC configuration given its name
        private void DeleteOscConfig()
        {
            if (SelectedOscConfig == null) return;

            try
            {
                if (string.IsNullOrEmpty(SelectedOscConfig.FilePath))
                {
                    Debug.LogErrorFormat("Selected OSC configuration has no file path reference");
                    return;
                }

                File.Delete(SelectedOscConfig.FilePath);
                var metaFile = SelectedOscConfig.FilePath + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                SelectOscConfig(null); // clear the current selection
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        #endregion Delete OSC Config

        #region Import OSC Config

        // Imports an OSC configuration file
        private void ImportOscConfig(string filepath, bool applyToScene = true)
        {
            try
            {
                // Read in the file JSON
                var json = File.ReadAllText(filepath);

                // Deserialize
                var importedConfig = JsonConvert.DeserializeObject<DataSessionsOscConfiguration>(json);

                // Since we didn't properly instantiate these as scriptable objects, let's do that now and copy over
                var converted = ConvertToScriptableOscConfig(importedConfig);
                SelectOscConfig(converted, applyToScene);
                //Debug.LogFormat("OSC configuration file imported from: {0}", filepath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        #endregion Import OSC Config

        #region Ensure Unique Name

        // Ensures an OSC configuration name is unique
        private bool EnsureUniqueOscConfigName(string name)
        {
            var dirPath = GetOscConfigPath();

            if (!Directory.Exists(dirPath))
            {
                Debug.LogWarningFormat("Sessions OSC configuration path not found: {0}", dirPath);
                return false;
            }

            var dirInfo = new DirectoryInfo(dirPath);
            var fileName = GetSafeFilename(name).Replace(" ", "") + ".json";

            foreach (var fileInfo in dirInfo.GetFiles("*.json"))
            {
                if (fileInfo.Extension.ToLower() == ".meta") continue;
                if (fileInfo.Name.ToLower() == fileName.ToLower()) return false;
            }

            return true;
        }

        #endregion Ensure Unique Name

        #endregion Utility

        #endregion OSC

        #region Routing Rules

        #region Render Routing GUI

        // Renders the Routing "tab" GUI        
        private void RenderRoutingGUI()
        {
            #region Toolbar

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
            HorizontalLine(new Color(0, 0, 0, 0));

            #region Config Select Dropdown

            // Select configuration
            var dropdownStyle = new GUIStyle(EditorStyles.toolbarDropDown) { alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 0, 0), fontSize = 11 };
            if (EditorGUI.DropdownButton(new Rect(8, 36, 192, 19), new GUIContent(SelectedRoutingConfig != null ? SelectedRoutingConfig.Name : ""), FocusType.Keyboard, dropdownStyle))
            {
                var dirPath = GetRoutingConfigPath();

                if (!Directory.Exists(dirPath))
                {
                    EditorUtility.DisplayDialog("Error", "The Sessions Routing configuration folder for the project could not be found: " + dirPath, "OK");
                }
                else
                {
                    var menu = new GenericMenu();

                    if ((scene.AllRoutingConfigurations == null || scene.AllRoutingConfigurations.Count == 0) && scene.AllRoutingConfigurations != null)
                    {
                        scene.AllRoutingConfigurations = new List<SessionsRoutingConfiguration>();
                        scene.AllRoutingConfigurations.Add(scene.RoutingConfiguration);
                    }

                    if (scene != null && scene.AllRoutingConfigurations != null && scene.AllRoutingConfigurations.Count > 0)
                    {
                        var configs = new SortedDictionary<string, SessionsRoutingConfiguration>();

                        foreach (var loadedConfig in scene.AllRoutingConfigurations)
                        {
                            if (loadedConfig == null) continue;

                            if (configs.ContainsKey(loadedConfig.Name))
                            {
                                Debug.LogWarningFormat("Configuration already exists with name: {0}", loadedConfig.Name);
                                continue;
                            }

                            configs.Add(loadedConfig.Name, loadedConfig);
                        }

                        foreach (var pair in configs)
                        {
                            // Add a menu item for it
                            menu.AddItem(new GUIContent(pair.Key), SelectedRoutingConfig != null && SelectedRoutingConfig.Name == pair.Key, () =>
                            {
                                this.StartCoroutine(DoSelectRouting(pair.Value));
                            });
                        }
                    }

                    menu.ShowAsContext();
                }
            }

            #endregion Config Select Dropdown

            var btnX = 210;
            var btnY = 35;
            var deletedRules = new List<SessionsRoutingRule>();

            #region Warning Message

            // If there's no instance, abort...
            if (scene == null)
            {
                var guiStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
                var content = new GUIContent(" No SessionScene instance found. Routing Disabled.", warnIcon);
                var rect = GUILayoutUtility.GetRect(content, guiStyle);
                rect.x = 4;
                rect.y = 35;
                EditorGUI.LabelField(rect, content, guiStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }

            #endregion Warning Message

            #region New

            // Create new configuration
            if (GUI.Button(new Rect(btnX, btnY, 24, 19), new GUIContent(newIcon, "New Routing configuration")))
            {
                if (scene == null)
                {
                    Debug.LogWarning("No SessionsScene instance was found to associate routing configuration with");
                }
                else
                {
                    var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                    nameDialog.UserInput = "My Routing Config";
                    nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                    nameDialog.DialogTitle = new GUIContent("New Routing Configuration");
                    nameDialog.DialogContent = new GUIContent("The name of the new Routing configuration.");
                    nameDialog.DialogOKButtonLabel = new GUIContent("Create");
                    nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                    nameDialog.OnDialogAccepted = () =>
                    {
                        // Get the user input
                        var name = nameDialog.UserInput;
                        if (string.IsNullOrEmpty(name)) return false;

                        // Ensure unique name
                        if (scene.AllRoutingConfigurations != null && scene.AllRoutingConfigurations.Count(rc => rc != null && rc.Name == name) > 0)
                        {
                            EditorUtility.DisplayDialog("Name In Use", string.Format("The routing configuration name '{0}' is already in use. Please choose another name.", name), "OK");
                            return false;
                        }

                        AddNewRoutingConfiguration(name);
                        return true;
                    };

                    nameDialog.ShowAuxWindow();
                }
            }

            #endregion New

            #region Rename

            // Rename configuration
            if (SelectedRoutingConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 32, btnY, 24, 19), new GUIContent(editIcon, "Rename the current routing configuration")))
            {
                var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                nameDialog.UserInput = SelectedRoutingConfig.Name;
                nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                nameDialog.DialogTitle = new GUIContent("Rename Routing Configuration");
                nameDialog.DialogContent = new GUIContent("The name of the Routing configuration.");
                nameDialog.DialogOKButtonLabel = new GUIContent("Rename");
                nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                nameDialog.OnDialogAccepted = () =>
                {
                    // Get the user input
                    var name = nameDialog.UserInput;
                    if (string.IsNullOrEmpty(name)) return false;

                    if (SelectedRoutingConfig.Name == name) return true; // no update

                    // Ensure unique name
                    if (scene.AllRoutingConfigurations != null && scene.AllRoutingConfigurations.Count(rc => rc.Name == name) > 0)
                    {
                        EditorUtility.DisplayDialog("Name In Use", string.Format("The routing configuration name '{0}' is already in use. Please choose another name.", name), "OK");
                        return false;
                    }

                    SelectedRoutingConfig.Name = name;

                    if (!EditorApplication.isPlaying)
                    {
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }

                    return true;
                };

                nameDialog.ShowAuxWindow();
            }
            GUI.enabled = true;

            #endregion Rename

            #region Copy

            // Save configuration
            if (SelectedRoutingConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 64, btnY, 24, 19), new GUIContent(copyIcon, "Copy current routing configuration to clipboard")))
            {
                if (SelectedRoutingConfig == null) return;
                CopyRoutingConfig();
            }
            GUI.enabled = true;

            #endregion Copy

            #region Paste

            // Import configuration
            if (RoutingClipboard == null) GUI.enabled = false; // TODO if clipboard data
            if (GUI.Button(new Rect(btnX + 96, btnY, 24, 19), new GUIContent(pasteIcon, "Paste routing configuration data")))
            {
                PasteRoutingConfig();
            }
            GUI.enabled = true;

            #endregion Paste

            #region Delete

            // Delete configuration
            if (SelectedRoutingConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 128, btnY, 24, 19), new GUIContent(deleteIcon, "Delete current routing configuration")))
            {
                var deleteMsg = string.Format("Are you sure you want to delete Routing configuration '{0}'?\n\nThere is no undo.", SelectedRoutingConfig.Name);
                if (EditorUtility.DisplayDialog("Confirm Delete", deleteMsg, "Delete", "Cancel")) DeleteRoutingConfig();
            }
            GUI.enabled = true;

            #endregion Delete

            #region Add Rule

            // Add rule
            if (GUI.Button(new Rect(btnX + 160, btnY, 24, 19), new GUIContent(addIcon, "Add a new routing rule")))
            {
                AddNewRoutingRule();
            }

            #endregion Add Rule

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            #endregion Toolbar

            #region Edit Area

            HorizontalLine();

            // Create the table header
            routingScrollView = EditorGUILayout.BeginScrollView(routingScrollView);

            #region Table Body

            // Prepare a possible instance for rule duplication
            SessionsRoutingRule duplicateRule = null;

            if (SelectedRoutingConfig != null)
            {
                var valueList = (from r in routingRules where r != null select r).OrderBy(r => r.EditIndex).ToArray();

                #region Routing Rule List

                // Make sure values are sorted correct (more undo/redo HACKs yay!)
                foreach (var rule in valueList)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Create a scriptable object out of the current routing rule
                    SerializedObject sobj = new SerializedObject(rule);

                    #region Expander

                    // Get the expanded state of this rule
                    var style = new GUIStyle(EditorStyles.foldout) { };
                    rule.IsExpanded = EditorGUILayout.Toggle(GUIContent.none, rule.IsExpanded, style, GUILayout.Width(16));

                    #endregion Expander

                    #region Delete

                    var deleteStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(4, 4, 2, 2), margin = new RectOffset(4, 4, 2, 4) };
                    if (GUILayout.Button(new GUIContent(deleteIcon, "Delete this routing rule"), deleteStyle, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        deletedRules.Add(rule);
                    }

                    #endregion Delete

                    #region Duplicate

                    var dupeStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(4, 4, 2, 2), margin = new RectOffset(4, 4, 2, 4) };
                    if (GUILayout.Button(new GUIContent(copyIcon, "Duplicate this routing rule"), dupeStyle, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        duplicateRule = AddNewRoutingRule(rule);
                        duplicateRule.EditIndex = rule.EditIndex + 1;
                    }

                    #endregion Duplicate

                    #region Row

                    try
                    {
                        if (!rule.IsExpanded)
                        {
                            Color cachedGuiColor = GUI.color;
                            sobj.Update();
                            var property = sobj.GetIterator();
                            var next = property.NextVisible(true);
                            if (next)
                                do
                                {
                                    GUI.color = cachedGuiColor;
                                    bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                    bool cachedGUIEnabled = GUI.enabled;
                                    if (isdefaultScriptProperty) continue; // don't render

                                    if (property.name == "Enabled")
                                    {
                                        EditorGUILayout.PropertyField(property, GUIContent.none, property.isExpanded, GUILayout.Width(24));
                                    }
                                    else if (property.name == "Name")
                                    {
                                        EditorGUILayout.LabelField(new GUIContent(property.stringValue, "The name of the session value to monitor"));
                                    }
                                    else if (property.name == "Comparison")
                                    {
                                        var content = new GUIContent("[" + property.enumDisplayNames[property.enumValueIndex] + "]", "The type of comparison to use for the incoming value");
                                        EditorGUILayout.LabelField(content);
                                    }
                                    else if (property.name == "Value")
                                    {
                                        var content = new GUIContent("" + property.floatValue.ToString() + "", "A value to which the incoming session value will be comapred and will either pass or fail depending on the comparison and value choice.");
                                        EditorGUILayout.LabelField(content, GUILayout.Width(84));
                                    }
                                    else if (property.name == "OnPassed")
                                    {
                                        var listeners = rule.OnPassed.GetPersistentEventCount().ToString();
                                        var content = new GUIContent("+(" + listeners + ")", "Unity Event triggers that will be called if the routing rule evaluation passes. These callbacks will be invoked and passed the name and value for processing.");
                                        EditorGUILayout.LabelField(content);
                                    }

                                    if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                                } while (property.NextVisible(false));
                            sobj.ApplyModifiedProperties();

                            GUILayout.FlexibleSpace();
                        }
                        else
                        {
                            Color cachedGuiColor = GUI.color;
                            sobj.Update();
                            var property = sobj.GetIterator();
                            var next = property.NextVisible(true);
                            if (next)
                                do
                                {
                                    GUI.color = cachedGuiColor;
                                    bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                    bool cachedGUIEnabled = GUI.enabled;
                                    if (isdefaultScriptProperty) continue; // don't render

                                    if (property.name == "Enabled")
                                    {
                                        EditorGUILayout.PropertyField(property, GUIContent.none, property.isExpanded, GUILayout.Width(24));
                                    }
                                    else if (property.name == "Name")
                                    {
                                        EditorGUILayout.PropertyField(property, GUIContent.none, property.isExpanded, GUILayout.Width(192));
                                    }
                                    else if (property.name == "Comparison")
                                    {
                                        EditorGUILayout.LabelField("", GUILayout.Width(7));
                                        EditorGUILayout.PropertyField(property, GUIContent.none, property.isExpanded, GUILayout.Width(146));
                                    }
                                    else if (property.name == "Value")
                                    {
                                        var content = new GUIContent("    " + property.displayName, "A value to which the incoming session value will be comapred and will either pass or fail depending on the comparison and value choice.");
                                        EditorGUILayout.LabelField(content, GUILayout.Width(56));
                                        EditorGUILayout.PropertyField(property, GUIContent.none, property.isExpanded, GUILayout.Width(64));
                                    }
                                    else if (property.name == "OnPassed")
                                    {
                                        EditorGUILayout.LabelField("", GUILayout.Width(16));
                                        EditorGUILayout.PropertyField(property, property.isExpanded, GUILayout.ExpandWidth(true), GUILayout.MaxWidth(600));
                                    }

                                    if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                                } while (property.NextVisible(false));
                            sobj.ApplyModifiedProperties();
                        }

                        // If we're playing try to keep rules updated in realtime
                        if (EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            var routing = FindObjectOfType<SessionsRouting>();
                            if (routing != null) routing.UpdateRule(rule);
                        }
                    }
                    catch { }

                    //GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    #endregion Row
                }

                #endregion Render Routing Rule List
            }

            #endregion Table Body

            #region Process Deleted

            // Clean up deleted rules
            var rulesDeleted = deletedRules.Count > 0;
            foreach (var rule in deletedRules)
            {
                routingRules.Remove(rule);
                Undo.DestroyObjectImmediate(rule);
                SelectedRoutingConfig.Rules = routingRules.ToArray();
            }

            if (rulesDeleted) Repaint();
            routingRules = new List<SessionsRoutingRule>((from r in routingRules where r != null select r));

            #endregion Process Deleted

            #region Process Duplicated

            // Duplicate rule
            if (duplicateRule != null && duplicateRule.EditIndex < routingRules.Count)
            {
                routingRules.Remove(duplicateRule);
                var valueList = (from r in routingRules where r != null select r).OrderBy(r => r.EditIndex).ToList();
                for (var i = 0; i < valueList.Count; i++) valueList[i].EditIndex = i;
                for (var i = duplicateRule.EditIndex; i < valueList.Count; i++) valueList[i].EditIndex++;
                valueList.Insert(duplicateRule.EditIndex, duplicateRule);
                routingRules = valueList.OrderBy(r => r.EditIndex).ToList();
            }

            #endregion Process Duplicated

            EditorGUILayout.EndScrollView();

            #endregion Edit Area
        }

        #endregion Render Routing GUI

        #region Utility

        #region Add Routing Config

        // Creates a routing configuration
        private SessionsRoutingConfiguration AddNewRoutingConfiguration(string name)
        {
            var config = CreateInstance<SessionsRoutingConfiguration>();
            config.Name = name;
            config.Rules = new SessionsRoutingRule[0];

            if (scene != null)
            {
                if (scene.AllRoutingConfigurations == null) scene.AllRoutingConfigurations = new List<SessionsRoutingConfiguration>();
                routingRules.Clear();
                //Debug.LogFormat("Selected Sessions routing configuration '{0}'", name);
                Undo.RegisterCreatedObjectUndo(config, "New Routing Configuration Created");
            }

            return config;
        }

        #endregion Add Routing Config

        #region Delete Routing Config

        // Deletes the current routing config
        private void DeleteRoutingConfig()
        {
            if (SelectedRoutingConfig == null) return;
            var name = SelectedRoutingConfig.Name;

            if (scene == null || scene.AllRoutingConfigurations == null || scene.AllRoutingConfigurations.Count == 0)
            {
                SelectRoutingConfig(null);
                return;
            }
            else
            {
                if (scene.RoutingConfiguration == SelectedRoutingConfig) scene.RoutingConfiguration = null;

                if (scene.RoutingConfiguration != null)
                {
                    scene.AllRoutingConfigurations.Remove(SelectedRoutingConfig);
                    scene.ApplyRoutingConfiguration();
                }
            }

            Undo.DestroyObjectImmediate(SelectedRoutingConfig);

            //Debug.LogFormat("Sessions Routing Configuration deleted: {0}", name);
        }

        #endregion Delete Routing Config

        #region Add Routing Rule

        // Adds a new routing rule
        private SessionsRoutingRule AddNewRoutingRule(SessionsRoutingRule source = null)
        {
            if (SelectedRoutingConfig == null) return null;
            var rule = CreateInstance<SessionsRoutingRule>();
            rule.Name = "Env/Lights/0/Intensity";
            rule.Comparison = ValueCompare.Any;
            rule.Value = 0;

            // If a source rule was provided, duplicate its values
            if (source != null)
            {
                rule.Comparison = source.Comparison;
                rule.Enabled = source.Enabled;
                rule.IsExpanded = source.IsExpanded;
                rule.Name = source.Name;
                rule.OnPassed = source.OnPassed;
                rule.Value = source.Value;
            }

            Undo.RegisterCreatedObjectUndo(rule, "Routing Rule Created");
            SessionsRoutingRule.OnCreated(rule);
            return rule;
        }

        #endregion Add Routing Rule

        #region Select Routing Config

        // Selects a routing configuration
        private void SelectRoutingConfig(SessionsRoutingConfiguration config, bool applyToScene = true)
        {
            //Debug.Log("Selecting: " + (config != null ? config.Name : "NULL"));
            SelectedRoutingConfig = config;

            if (config != null)
            {
                if (scene != null && applyToScene)
                {
                    scene.RoutingConfiguration = config;
                    scene.ApplyRoutingConfiguration();

                    if (!EditorApplication.isPlaying)
                    {
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }
                }

                // Ensure proper edit index for rows
                config.Rules = config.Rules != null ? (from r in config.Rules where r != null select r).ToArray() : new SessionsRoutingRule[0];
                for (var i = 0; i < config.Rules.Length; i++) config.Rules[i].EditIndex = i;
            }

            if (routingRules == null) routingRules = new List<SessionsRoutingRule>();
            else routingRules.Clear();
            if (config != null && config.Rules != null && config.Rules.Length > 0) routingRules.AddRange(config.Rules);
            Repaint();
        }

        private IEnumerator DoSelectRouting(SessionsRoutingConfiguration config, float delay = 0.5f)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            SelectRoutingConfig(config);
            yield break;
        }

        #endregion Select Routing Config

        #region Copy Routing Config

        // Copies the current routing configuration to the clipboard
        private void CopyRoutingConfig()
        {
            var source = SelectedRoutingConfig;
            var r = CreateInstance<SessionsRoutingConfiguration>();
            r.Name = source.Name;
            r.Version = source.Version;
            var rules = new List<SessionsRoutingRule>();
            
            foreach (var r1 in source.Rules)
            {
                if (r1 == null) continue;
                var r2 = CreateInstance<SessionsRoutingRule>();
                r2.Comparison = r1.Comparison;
                r2.EditIndex = r1.EditIndex;
                r2.Enabled = r1.Enabled;
                r2.IsExpanded = false;
                r2.Name = r1.Name;
                r2.OnPassed = r1.OnPassed;
                r2.Value = r1.Value;
                rules.Add(r2);
            }

            r.Rules = rules.ToArray();
            RoutingClipboard = r;

            // Remove the duplicate configuration so it won't so up in the menu
            if (scene != null) if (scene.AllRoutingConfigurations != null)
            {
                scene.AllRoutingConfigurations.Remove(r);
                scene.ApplyRoutingConfiguration();
            }

            //if (SelectedRoutingConfig != null) Debug.LogFormat("Sessions Routing Configuration copied: {0}", SelectedRoutingConfig.Name);
        }

        #endregion Copy Routing Config

        #region Paste Routing Config

        // Pastes the current routing configuration from the clipboard
        private void PasteRoutingConfig()
        {
            if (RoutingClipboard != null)
            {
                // If there's no current configuration create a new one
                if (SelectedRoutingConfig == null)
                {
                    AddNewRoutingConfiguration(RoutingClipboard.Name);
                    SelectedRoutingConfig.Version = RoutingClipboard.Version;

                    if (RoutingClipboard.Rules != null && RoutingClipboard.Rules.Length > 0)
                    {
                        var rules = new List<SessionsRoutingRule>(RoutingClipboard.Rules.Length);

                        foreach (var r1 in RoutingClipboard.Rules)
                        {
                            AddNewRoutingRule(r1);
                        }

                        SelectedRoutingConfig.Rules = rules.ToArray();
                        RoutingClipboard = null;
                        Repaint();
                    }
                }
                // Otherwise paste over the current one
                else
                {
                    if (RoutingClipboard.Rules != null && RoutingClipboard.Rules.Length > 0)
                    {
                        // Get the name from the current data
                        var name = SelectedRoutingConfig.Name;

                        // Delete the current configuration
                        DeleteRoutingConfig();

                        // Create a new routing configuration
                        AddNewRoutingConfiguration(name);
                        //routingRules.Clear();
                        //SelectedRoutingConfig.Rules = new SessionsRoutingRule[0];

                        foreach (var r1 in RoutingClipboard.Rules)
                        {
                            if (r1 == null) continue;
                            AddNewRoutingRule(r1);
                            r1.IsExpanded = false;
                        }

                        SelectedRoutingConfig.Rules = routingRules.ToArray();
                        Repaint();
                        RoutingClipboard = null;
                        return;
                    }
                }
            }
        }

        #endregion Paste Routing Config

        #region Get Routing Config Path

        // Returns the routing config asset folder
        private string GetRoutingConfigPath()
        {
            var sc = Path.DirectorySeparatorChar;
            return Directory.GetCurrentDirectory() + sc + "Assets" + sc + "CymaticLabs" + sc + "Sessions" + sc + "Resources" + sc + "Routing";
        }

        #endregion Get Routing Config Path

        #region Post Destroy Routing Config Cleanup

        private IEnumerator DoPostDestroyRoutingConfig(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Select the next available configuration
            if (scene == null) yield break;

            // Remove any NULL references
            if (scene.AllRoutingConfigurations != null)
            {
                var count = scene.AllRoutingConfigurations.Count(c => c == null);

                if (count > 0)
                {
                    scene.AllRoutingConfigurations = (from c in scene.AllRoutingConfigurations where c != null select c).ToList();
                    SelectRoutingConfig(scene.AllRoutingConfigurations.FirstOrDefault());
                    Repaint();
                }
            }
        }

        #endregion Post Destroy Routing Config Cleanup

        #endregion Utility

        #endregion Routing Rules

        #region Entities

        #region Render Entities GUI

        // Renders the Entities "tab" GUI        
        private void RenderEntitiesGUI()
        {
            #region Toolbar

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
            HorizontalLine(new Color(0, 0, 0, 0));

            #region Config Select Dropdown

            // Select configuration
            var dropdownStyle = new GUIStyle(EditorStyles.toolbarDropDown) { alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 0, 0), fontSize = 11 };
            if (EditorGUI.DropdownButton(new Rect(8, 36, 192, 19), new GUIContent(SelectedEntitiesConfig != null ? SelectedEntitiesConfig.Name : ""), FocusType.Keyboard, dropdownStyle))
            {
                var dirPath = GetEntitiesConfigPath();

                if (!Directory.Exists(dirPath))
                {
                    EditorUtility.DisplayDialog("Error", "The Sessions Entities configuration folder for the project could not be found: " + dirPath, "OK");
                }
                else
                {
                    var menu = new GenericMenu();

                    if ((scene != null && scene.AllEntitiesConfigurations == null || scene.AllEntitiesConfigurations.Count == 0) && scene.EntitiesConfiguration != null)
                    {
                        scene.AllEntitiesConfigurations = new List<SessionsEntitiesConfiguration>();
                        scene.AllEntitiesConfigurations.Add(scene.EntitiesConfiguration);
                    }

                    if (scene != null && scene.AllEntitiesConfigurations != null && scene.AllEntitiesConfigurations.Count > 0)
                    {
                        var configs = new SortedDictionary<string, SessionsEntitiesConfiguration>();

                        foreach (var loadedConfig in scene.AllEntitiesConfigurations)
                        {
                            if (loadedConfig == null) continue;

                            if (configs.ContainsKey(loadedConfig.Name))
                            {
                                Debug.LogWarningFormat("Configuration already exists with name: {0}", loadedConfig.Name);
                                continue;
                            }

                            configs.Add(loadedConfig.Name, loadedConfig);
                        }

                        foreach (var pair in configs)
                        {
                            // Add a menu item for it
                            menu.AddItem(new GUIContent(pair.Key), SelectedEntitiesConfig != null && SelectedEntitiesConfig.Name == pair.Key, () =>
                            {
                                this.StartCoroutine(DoSelectEntitiesConfig(pair.Value));
                            });
                        }
                    }

                    menu.ShowAsContext();
                }
            }

            #endregion Config Select Dropdown

            var btnX = 210;
            var btnY = 35;
            var deletedEntities = new List<SessionsNetworkEntityInfo>();

            #region Warning Message

            // If there's no instance, abort...
            if (scene == null)
            {
                var guiStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
                var content = new GUIContent(" No SessionsScene instance found. Entities Disabled.", warnIcon);
                var rect = GUILayoutUtility.GetRect(content, guiStyle);
                rect.x = 4;
                rect.y = 35;
                EditorGUI.LabelField(rect, content, guiStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }

            #endregion Warning Message

            #region New

            // Create new configuration
            if (GUI.Button(new Rect(btnX, btnY, 24, 19), new GUIContent(newIcon, "New Entities configuration")))
            {
                if (scene == null)
                {
                    Debug.LogWarning("No SessionsScene instance was found in the scene to associate entities configuration with");
                }
                else
                {
                    var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                    nameDialog.UserInput = "My Entities Config";
                    nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                    nameDialog.DialogTitle = new GUIContent("New Entities Configuration");
                    nameDialog.DialogContent = new GUIContent("The name of the new Entities configuration.");
                    nameDialog.DialogOKButtonLabel = new GUIContent("Create");
                    nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                    nameDialog.OnDialogAccepted = () =>
                    {
                        // Get the user input
                        var name = nameDialog.UserInput;
                        if (string.IsNullOrEmpty(name)) return false;

                        // Ensure unique name
                        if (scene.AllEntitiesConfigurations != null && scene.AllEntitiesConfigurations.Count(ec => ec != null && ec.Name == name) > 0)
                        {
                            EditorUtility.DisplayDialog("Name In Use", string.Format("The entities configuration name '{0}' is already in use. Please choose another name.", name), "OK");
                            return false;
                        }

                        AddNewEntitiesConfiguration(name);
                        return true;
                    };

                    nameDialog.ShowAuxWindow();
                }
            }

            #endregion New

            #region Rename

            // Rename configuration
            if (SelectedEntitiesConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 32, btnY, 24, 19), new GUIContent(editIcon, "Rename the current entities configuration")))
            {
                var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                nameDialog.UserInput = SelectedEntitiesConfig.Name;
                nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                nameDialog.DialogTitle = new GUIContent("Rename Entities Configuration");
                nameDialog.DialogContent = new GUIContent("The name of the Entities configuration.");
                nameDialog.DialogOKButtonLabel = new GUIContent("Rename");
                nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                nameDialog.OnDialogAccepted = () =>
                {
                    // Get the user input
                    var name = nameDialog.UserInput;
                    if (string.IsNullOrEmpty(name)) return false;

                    if (SelectedEntitiesConfig.Name == name) return true; // no update

                    if (scene == null) return false;

                    // Ensure unique name
                    if (scene.AllEntitiesConfigurations != null && scene.AllEntitiesConfigurations.Count(rc => rc.Name == name) > 0)
                    {
                        EditorUtility.DisplayDialog("Name In Use", string.Format("The entities configuration name '{0}' is already in use. Please choose another name.", name), "OK");
                        return false;
                    }

                    SelectedEntitiesConfig.Name = name;

                    if (!EditorApplication.isPlaying)
                    {
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }

                    return true;
                };

                nameDialog.ShowAuxWindow();
            }
            GUI.enabled = true;

            #endregion Rename

            #region Import

            // Export configuration
            if (SelectedEntitiesConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 64, btnY, 24, 19), new GUIContent(copyIcon, "Import entities configuration")))
            {
                // TODO Import
            }
            GUI.enabled = true;

            #endregion Import

            #region Export

            // Export configuration
            if (SelectedEntitiesConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 96, btnY, 24, 19), new GUIContent(exportIcon, "Export entities configuration")))
            {
                ExportEntitiesConfig();
            }
            GUI.enabled = true;

            #endregion Export

            #region Delete

            // Delete configuration
            if (SelectedEntitiesConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 128, btnY, 24, 19), new GUIContent(deleteIcon, "Delete current entities configuration")))
            {
                var deleteMsg = string.Format("Are you sure you want to delete Entities configuration '{0}'?\n\nThere is no undo.", SelectedEntitiesConfig.Name);
                if (EditorUtility.DisplayDialog("Confirm Delete", deleteMsg, "Delete", "Cancel")) DeleteEntitiesConfig();
            }
            GUI.enabled = true;

            #endregion Delete

            #region Add Entitiy

            // Add rule
            if (GUI.Button(new Rect(btnX + 160, btnY, 24, 19), new GUIContent(addIcon, "Add a new network entity")))
            {
                AddNewNetworkEntity();
            }

            #endregion Add Entitiy

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            #endregion Toolbar

            #region Edit Area

            HorizontalLine();

            // Create the table header
            entitiesScrollView = EditorGUILayout.BeginScrollView(entitiesScrollView);

            #region Table Body

            if (SelectedEntitiesConfig != null)
            {
                var valueList = (from e in networkEntities where e != null select e).OrderBy(e => e.EditIndex).ToArray();

                #region Entities List

                // Make sure values are sorted correct (more undo/redo HACKs yay!)
                foreach (var entityInfo in valueList)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Create a scriptable object out of the current routing rule
                    SerializedObject sobj = new SerializedObject(entityInfo);

                    #region Delete

                    var deleteStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(4, 4, 2, 2), margin = new RectOffset(4, 4, 2, 4) };
                    if (GUILayout.Button(new GUIContent(deleteIcon, "Delete this network entity"), deleteStyle, GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        deletedEntities.Add(entityInfo);
                    }

                    #endregion Delete

                    #region Row

                    try
                    {
                            
                        Color cachedGuiColor = GUI.color;
                        sobj.Update();
                        var property = sobj.GetIterator();
                        var next = property.NextVisible(true);
                        if (next)
                            do
                            {
                                GUI.color = cachedGuiColor;
                                bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                bool cachedGUIEnabled = GUI.enabled;
                                if (isdefaultScriptProperty) continue; // don't render

                                if (property.name == "Enabled")
                                {
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(24));
                                }
                                else if (property.name == "Name")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("Name", "The network name of the entity. Must be unique."), GUILayout.Width(42));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(160));
                                }
                                else if (property.name == "MaxInstances")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("Max Instances", "The maximum number of instances of this entity type allowed at one time."), GUILayout.Width(96));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(48));
                                }
                                else if (property.name == "Prefab")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("     Prefab", "The XR prefab to use when instantiating this entity."), GUILayout.Width(60));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(160));
                                }
                                else if (property.name == "NonXrPrefab")
                                {
                                    EditorGUILayout.LabelField(new GUIContent("     Non-XR Prefab", "The prefab to use when instantiating this entity in a non-XR build."), GUILayout.Width(108));
                                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.Width(160));
                                }

                                if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                            } while (property.NextVisible(false));
                        sobj.ApplyModifiedProperties();
                        
                        // If we're playing try to keep rules updated in realtime
                        //if (EditorApplication.isPlayingOrWillChangePlaymode) entities.UpdateRule(rule);
                    }
                    catch { }

                    //GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    #endregion Row
                }

                #endregion Entities List
            }

            #endregion Table Body

            #region Process Deleted

            // Clean up deleted rules
            var entitiesDeleted = deletedEntities.Count > 0;
            foreach (var entityInfo in deletedEntities)
            {
                Undo.DestroyObjectImmediate(entityInfo);
                networkEntities.Remove(entityInfo);
                SelectedEntitiesConfig.Entities = networkEntities.ToArray();
            }

            if (entitiesDeleted) Repaint();
            if (networkEntities != null) networkEntities = new List<SessionsNetworkEntityInfo>((from e in networkEntities where e != null select e));

            #endregion Process Deleted

            EditorGUILayout.EndScrollView();

            #endregion Edit Area
        }

        #endregion Render Entities GUI

        #region Utility

        #region Add Entities Config

        // Creates an entities configuration
        private SessionsEntitiesConfiguration AddNewEntitiesConfiguration(string name)
        {
            var config = CreateInstance<SessionsEntitiesConfiguration>();
            config.Name = name;
            config.Entities = new SessionsNetworkEntityInfo[0];

            if (scene != null)
            {
                if (scene.AllEntitiesConfigurations == null) scene.AllEntitiesConfigurations = new List<SessionsEntitiesConfiguration>();
                if (networkEntities != null) networkEntities.Clear();
                //Debug.LogFormat("Selected Sessions entities configuration '{0}'", name);
                Undo.RegisterCreatedObjectUndo(config, "New Entities Configuration Created");
                scene.ApplyEntitiesConfiguration();
            }

            return config;
        }

        #endregion Add Entities Config

        #region Select Entities Config

        // Selects an entities configuration
        private void SelectEntitiesConfig(SessionsEntitiesConfiguration config, bool applyToScene = true)
        {
            //Debug.Log("Selecting: " + (config != null ? config.Name : "NULL"));
            SelectedEntitiesConfig = config;

            if (config != null)
            {
                if (scene != null && applyToScene)
                {
                    scene.EntitiesConfiguration = config;
                    scene.ApplyEntitiesConfiguration();

                    if (!EditorApplication.isPlaying)
                    {
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }
                }

                // Ensure proper edit index for rows
                config.Entities = config.Entities != null ? (from e in config.Entities where e != null select e).ToArray() : new SessionsNetworkEntityInfo[0];
                for (var i = 0; i < config.Entities.Length; i++) config.Entities[i].EditIndex = i;
            }

            if (networkEntities == null) networkEntities = new List<SessionsNetworkEntityInfo>();
            else networkEntities.Clear();
            if (config != null && config.Entities != null && config.Entities.Length > 0) networkEntities.AddRange(config.Entities);
            Repaint();
        }

        private IEnumerator DoSelectEntitiesConfig(SessionsEntitiesConfiguration config, float delay = 0.5f)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            SelectEntitiesConfig(config);
            yield break;
        }

        #endregion Select Entities Config

        #region Export Entities Config

        // Exports current entities configuration to disk
        private void ExportEntitiesConfig()
        {
            if (SelectedEntitiesConfig == null) return;

            // Create a folder for this configuration to contain the child rules
            var entitiesPath = "Assets/CymaticLabs/Sessions/Resources/Entities";

            if (!AssetDatabase.IsValidFolder(entitiesPath))
            {
                AssetDatabase.CreateFolder("Assets/CymaticLabs/Sessions/Resources", "Entities");
            }

            // We need to delay after potential folder creation otherwise the Editor will yell at us when we try to create the child assets in it
            this.StartCoroutine(DoFinishSaveEntitiesConfig(entitiesPath, true));
        }

        // Finishes saving the rule configuration
        IEnumerator DoFinishSaveEntitiesConfig(string entitiesPath, bool waitForPath)
        {
            if (waitForPath)
            {
                while (!AssetDatabase.IsValidFolder(entitiesPath))
                {
                    //Debug.Log("Waiting");
                    yield return new WaitForSeconds(0.25f);
                }
            }

            // Save out entries first
            var entries = (from e in networkEntities where e != null select e).ToArray();

            // Create the asset for each rule
            for (var i = 0; i < entries.Length; i++)
            {
                var entityInfo = entries[i];
                var configName = GetSafeFilename(SelectedEntitiesConfig.Name).Replace(" ", "");
                entityInfo.Name = GetSafeFilename(entityInfo.Name);
                var assetPath = entitiesPath + "/" + configName + "-" + entityInfo.Name + ".asset";

                // Create this rule as an asset if it doesn't already exist
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(entityInfo)))
                {
                    AssetDatabase.CreateAsset(entityInfo, assetPath);
                }
                // Otherwise mark it dirty for saving
                else
                {
                    //EditorUtility.SetDirty(rule);
                }
            }

            // Apply the freshly saved rules
            SelectedEntitiesConfig.Entities = entries;

            // Mark dirty and save
            AssetDatabase.CreateAsset(SelectedEntitiesConfig, entitiesPath + "/" + GetSafeFilename(SelectedEntitiesConfig.Name).Replace(" ", "") + ".asset");
            EditorUtility.SetDirty(SelectedEntitiesConfig);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("Entities configuration files exported"));

            // TODO Delta on names and delete orphaned assets
        }

        #endregion Export Entities Config

        #region Delete Entities Config

        // Deletes the current routing config
        private void DeleteEntitiesConfig()
        {
            if (SelectedEntitiesConfig == null) return;
            var name = SelectedEntitiesConfig.Name;

            if (scene == null || scene.AllEntitiesConfigurations == null || scene.AllEntitiesConfigurations.Count == 0)
            {
                SelectEntitiesConfig(null);
                return;
            }
            else
            {
                if (scene.EntitiesConfiguration == SelectedEntitiesConfig) scene.EntitiesConfiguration = null;

                if (scene.AllEntitiesConfigurations != null)
                {
                    scene.AllEntitiesConfigurations.Remove(SelectedEntitiesConfig);
                    scene.ApplyEntitiesConfiguration();
                }
            }

            Undo.DestroyObjectImmediate(SelectedEntitiesConfig);

            Debug.LogFormat("Sessions Entities Configuration deleted: {0}", name);
        }

        #endregion Delete Entities Config

        #region Add Network Entity

        // Adds a new routing rule
        private SessionsNetworkEntityInfo AddNewNetworkEntity(SessionsNetworkEntityInfo source = null)
        {
            if (SelectedEntitiesConfig == null) return null;
            var entityInfo = CreateInstance<SessionsNetworkEntityInfo>();
            entityInfo.Name = "My Entity";

            // If a source rule was provided, duplicate its values
            if (source != null)
            {
                entityInfo.Enabled = source.Enabled;
                entityInfo.MaxInstances = source.MaxInstances;
                entityInfo.Name = source.Name;
                entityInfo.NonXrPrefab = source.NonXrPrefab;
                entityInfo.Prefab = source.Prefab;
            }

            Undo.RegisterCreatedObjectUndo(entityInfo, "Network Entity Entry Created");
            //SessionsNetworkEntityInfo.OnCreated(entityInfo);
            return entityInfo;
        }

        #endregion Add Network Entity

        #region Post Destroy Entities Config Cleanup

        private IEnumerator DoPostDestroyEntitiesConfig(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Select the next available configuration
            if (scene == null) yield break;

            // Remove any NULL references
            if (scene.AllEntitiesConfigurations != null)
            {
                var count = scene.AllEntitiesConfigurations.Count(c => c == null);

                if (count > 0)
                {
                    scene.AllEntitiesConfigurations = (from c in scene.AllEntitiesConfigurations where c != null select c).ToList();
                    SelectEntitiesConfig(scene.AllEntitiesConfigurations.FirstOrDefault());
                    Repaint();
                }
            }
        }

        #endregion Post Destroy Entities Config Cleanup

        #region Get Entities Config Path

        // Returns the entities config asset folder
        private string GetEntitiesConfigPath()
        {
            var sc = Path.DirectorySeparatorChar;
            return Directory.GetCurrentDirectory() + sc + "Assets" + sc + "CymaticLabs" + sc + "Sessions" + sc + "Resources" + sc + "Entities";
        }

        #endregion Get Entities Config Path

        #endregion Utility

        #endregion Entities

        #region Network

        // Editor coroutine that monitors the network during editor play mode
        IEnumerator DoMonitorNetwork(float interval = 1)
        {
            while (isMonitoringNetwork)
            {
                // If we're on the network tab, refresh the window
                if (tabIndex == 2) Repaint();
                yield return new WaitForSeconds(interval);
            }
        }

        private void RenderNetworkGUI()
        {
            var sessions = SessionsUdpNetworking.Current;

            #region Toolbar

            var boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

            EditorGUILayout.BeginHorizontal();

            var isRegistered = SessionsUdpNetworking.Current != null && SessionsUdpNetworking.Current.IsRegistered;
            EditorGUILayout.LabelField("Registered w/ Facilitator:", boldStyle, GUILayout.Width(172));
            EditorGUILayout.LabelField(isRegistered ? "yes" : "no", GUILayout.Width(32));

            var currentSession = SessionsUdpNetworking.Current != null ? SessionsUdpNetworking.Current.SessionName : "-";
            EditorGUILayout.LabelField("Current Session:", boldStyle, GUILayout.Width(112));
            EditorGUILayout.LabelField(currentSession, GUILayout.Width(112));

            var isHost = SessionsUdpNetworking.Current != null && SessionsUdpNetworking.Current.IsHost;
            EditorGUILayout.LabelField("Is Host:", boldStyle, GUILayout.Width(56));
            EditorGUILayout.LabelField(isHost ? "yes" : "no", GUILayout.Width(32));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            #endregion Toolbar

            #region Edit Area

            #region Table Header

            // Create the table header
            EditorGUILayout.BeginHorizontal();
            var tableHeaderStyle = new GUIStyle(GUI.skin.box) { normal = { textColor = Color.white }, margin = new RectOffset(0, 0, 0, 0) };
            //var tableButtonHeaderStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = Color.white }, margin = new RectOffset(0, 0, 0, 0) };

            // Create the table body
            GUILayout.Box("Host", tableHeaderStyle, GUILayout.Width(132));
            GUILayout.Box("Port", tableHeaderStyle, GUILayout.Width(68));
            GUILayout.Box("Agent ID", tableHeaderStyle, GUILayout.Width(262));
            GUILayout.Box("Name", tableHeaderStyle, GUILayout.Width(132));
            GUILayout.Box("Voice", tableHeaderStyle, GUILayout.Width(68));
            GUILayout.Box("Ping", tableHeaderStyle, GUILayout.Width(68));
            GUILayout.Box("Avg Ping", tableHeaderStyle, GUILayout.Width(68));
            GUILayout.Box("Entities", tableHeaderStyle, GUILayout.Width(76));
            GUILayout.Box("", tableHeaderStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            #endregion Table Header

            #region Table Body

            // Start scrolling area for connected agent list
            oscScrollViewV = EditorGUILayout.BeginScrollView(oscScrollViewV);

            if (sessions != null)
            {
                var entityManager = SessionsNetworkEntityManager.Current;

                //var labelStyle = new GUIStyle(GUI.skin.label) { margin = new RectOffset(0, 0, 0, 0) };
                var labelStyle = new GUIStyle(GUI.skin.label) { margin = new RectOffset(4, 0, 2, 2), padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleCenter };
                var agents = sessions.GetAllAgents();

                foreach (var agent in agents)
                {
                    EditorGUILayout.BeginHorizontal();
                    var ip = agent.ConnectedIP != null ? agent.ConnectedIP.ToString() : agent.PrivateIP.ToString();
                    if (agent.IsRelayed) ip = "[relayed]";
                    var port = agent.ConnectedIP != null ? agent.ConnectedPort.ToString() : agent.PrivatePort.ToString();
                    if (agent.IsRelayed) port = "[relayed]";
                    var agentId = agent.Id.ToString();
                    var name = agent.Name;
                    var voice = !string.IsNullOrEmpty(agent.VoiceId) ? "yes" : "no";
                    var ping = agent.LastLatency.ToString();
                    var avgPing = agent.AverageLatency.ToString();
                    var instances = entityManager != null ? entityManager.GetInstancesByOwner(agent.Id).Count.ToString() : "0";

                    GUILayout.Label(ip, labelStyle, GUILayout.Width(128));
                    GUILayout.Label(port, labelStyle, GUILayout.Width(64));
                    GUILayout.Label(agentId, labelStyle, GUILayout.Width(256));
                    GUILayout.Label(name, labelStyle, GUILayout.Width(128));
                    GUILayout.Label(voice, labelStyle, GUILayout.Width(64));
                    GUILayout.Label(ping, labelStyle, GUILayout.Width(64));
                    GUILayout.Label(avgPing, labelStyle, GUILayout.Width(64));
                    GUILayout.Label(instances, labelStyle, GUILayout.Width(72));
                    EditorGUILayout.EndHorizontal();
                    HorizontalLine();
                }
            }

            #endregion Table Body

            EditorGUILayout.EndScrollView();

            #endregion Edit Area
        }

        #endregion Network

        #region xAPI

        #region Render xAPI GUI

        private void RenderXapiGUI()
        {
            #region Toolbar

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
            HorizontalLine(new Color(0, 0, 0, 0));

            #region Config Select Dropdown

            // Select configuration
            var dropdownStyle = new GUIStyle(EditorStyles.toolbarDropDown) { alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 0, 0), fontSize = 11 };
            if (EditorGUI.DropdownButton(new Rect(8, 36, 192, 19), new GUIContent(SelectedXapiConfig != null ? SelectedXapiConfig.Name : ""), FocusType.Keyboard, dropdownStyle))
            {
                var dirPath = GetXapiConfigPath();

                if (!Directory.Exists(dirPath))
                {
                    EditorUtility.DisplayDialog("Error", "The Sessions xAPI configuration folder for the project could not be found: " + dirPath, "OK");
                }
                else
                {
                    var menu = new GenericMenu();

                    if ((scene != null && scene.AllXapiConfigurations == null || scene.AllXapiConfigurations.Count == 0) && scene.AllXapiConfigurations != null)
                    {
                        scene.AllXapiConfigurations = new List<XapiConfiguration>();
                        scene.AllXapiConfigurations.Add(scene.XapiConfiguration);
                    }

                    if (scene != null && scene.AllXapiConfigurations != null && scene.AllXapiConfigurations.Count > 0)
                    {
                        var configs = new SortedDictionary<string, XapiConfiguration>();

                        foreach (var loadedConfig in scene.AllXapiConfigurations)
                        {
                            if (loadedConfig == null) continue;

                            if (configs.ContainsKey(loadedConfig.Name))
                            {
                                Debug.LogWarningFormat("Configuration already exists with name: {0}", loadedConfig.Name);
                                continue;
                            }

                            //Debug.LogFormat("Loaded: {0} {1} {2}", loadedConfig.Name, loadedConfig.Version, loadedConfig.Entities);
                            configs.Add(loadedConfig.Name, loadedConfig);
                        }

                        foreach (var pair in configs)
                        {
                            // Add a menu item for it
                            menu.AddItem(new GUIContent(pair.Key), SelectedXapiConfig != null && SelectedXapiConfig.Name == pair.Key, () =>
                            {
                                this.StartCoroutine(DoSelectXapiConfig(pair.Value));
                            });
                        }
                    }

                    menu.ShowAsContext();
                }
            }

            #endregion Config Select Dropdown

            var btnX = 210;
            var btnY = 35;

            #region Warning Message

            // If there's no instance, abort...
            if (scene == null)
            {
                var guiStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
                var content = new GUIContent(" No SessionsScene instance found. xAPI Disabled.", warnIcon);
                var rect = GUILayoutUtility.GetRect(content, guiStyle);
                rect.x = 4;
                rect.y = 35;
                EditorGUI.LabelField(rect, content, guiStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }

            #endregion Warning Message

            #region New

            // Create new configuration
            if (GUI.Button(new Rect(btnX, btnY, 24, 19), new GUIContent(newIcon, "New xAPI configuration")))
            {
                if (scene == null)
                {
                    Debug.LogWarning("No SessionsScene instance was found in the scene to associate xAPI configuration with");
                }
                else
                {
                    var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                    nameDialog.UserInput = "My xAPI Config";
                    nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                    nameDialog.DialogTitle = new GUIContent("New xAPI Configuration");
                    nameDialog.DialogContent = new GUIContent("The name of the new xAPI configuration.");
                    nameDialog.DialogOKButtonLabel = new GUIContent("Create");
                    nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                    nameDialog.OnDialogAccepted = () =>
                    {
                        // Get the user input
                        var name = nameDialog.UserInput;
                        if (string.IsNullOrEmpty(name)) return false;

                        // Ensure unique name
                        if (scene.AllXapiConfigurations != null && scene.AllXapiConfigurations.Count(xc => xc != null && xc.Name == name) > 0)
                        {
                            EditorUtility.DisplayDialog("Name In Use", string.Format("The xAPI configuration name '{0}' is already in use. Please choose another name.", name), "OK");
                            return false;
                        }

                        AddNewXapiConfiguration(name);
                        return true;
                    };

                    nameDialog.ShowAuxWindow();
                }
            }

            #endregion New

            #region Rename

            // Rename configuration
            if (SelectedXapiConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 32, btnY, 24, 19), new GUIContent(editIcon, "Rename the current xAPI configuration")))
            {
                var nameDialog = CreateInstance<SessionsUserInputEditorDialog>();
                nameDialog.UserInput = SelectedXapiConfig.Name;
                nameDialog.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
                nameDialog.DialogTitle = new GUIContent("Rename xAPI Configuration");
                nameDialog.DialogContent = new GUIContent("The name of the xAPI configuration.");
                nameDialog.DialogOKButtonLabel = new GUIContent("Rename");
                nameDialog.DialogCancelButtonLabel = new GUIContent("Cancel");

                nameDialog.OnDialogAccepted = () =>
                {
                    // Get the user input
                    var name = nameDialog.UserInput;
                    if (string.IsNullOrEmpty(name)) return false;

                    if (SelectedXapiConfig.Name == name) return true; // no update

                    if (scene == null) return false;

                    // Ensure unique name
                    if (scene.AllXapiConfigurations != null && scene.AllXapiConfigurations.Count(rc => rc.Name == name) > 0)
                    {
                        EditorUtility.DisplayDialog("Name In Use", string.Format("The xAPI configuration name '{0}' is already in use. Please choose another name.", name), "OK");
                        return false;
                    }

                    SelectedXapiConfig.Name = name;

                    if (!EditorApplication.isPlaying)
                    {
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }

                    return true;
                };

                nameDialog.ShowAuxWindow();
            }
            GUI.enabled = true;

            #endregion Rename

            #region Import

            // Export configuration
            if (SelectedXapiConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 64, btnY, 24, 19), new GUIContent(copyIcon, "Import xAPI configuration")))
            {
                // TODO Import
            }
            GUI.enabled = true;

            #endregion Import

            #region Export

            // Export configuration
            if (SelectedXapiConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 96, btnY, 24, 19), new GUIContent(exportIcon, "Export xAPI configuration")))
            {
                // TODO export
            }
            GUI.enabled = true;

            #endregion Export

            #region Delete

            // Delete configuration
            if (SelectedXapiConfig == null) GUI.enabled = false;
            if (GUI.Button(new Rect(btnX + 128, btnY, 24, 19), new GUIContent(deleteIcon, "Delete current xAPI configuration")))
            {
                var deleteMsg = string.Format("Are you sure you want to delete xAPI configuration '{0}'?\n\nThere is no undo.", SelectedXapiConfig.Name);
                if (EditorUtility.DisplayDialog("Confirm Delete", deleteMsg, "Delete", "Cancel")) DeleteXapiConfig();
            }
            GUI.enabled = true;

            #endregion Delete

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            HorizontalLine();

            #endregion Toolbar

            // Create the table header
            xapiScrollView = EditorGUILayout.BeginScrollView(xapiScrollView);

            if (SelectedXapiConfig != null && SelectedXapiConfig.DefaultStatement != null)
            {
                #region Service

                isXapiServiceFoldoutOpen = EditorGUILayout.Foldout(isXapiServiceFoldoutOpen, "Service", true);

                if (isXapiServiceFoldoutOpen)
                {
                    EditorGUI.indentLevel = 1;

                    try
                    {
                        // Create a scriptable object out of the current routing rule
                        SerializedObject sobj = new SerializedObject(SelectedXapiConfig);

                        Color cachedGuiColor = GUI.color;
                        sobj.Update();

                        var apiVersion = sobj.FindProperty("ApiVersion");
                        EditorGUILayout.PropertyField(apiVersion, GUILayout.Width(240));

                        var baseUrl = sobj.FindProperty("BaseURL");
                        EditorGUILayout.PropertyField(baseUrl, GUILayout.Width(500));

                        var basicAuth = sobj.FindProperty("BasicAuth");
                        EditorGUILayout.PropertyField(basicAuth, GUILayout.Width(1024));

                        sobj.ApplyModifiedProperties();

                        // If we're playing try to keep rules updated in realtime
                        //if (EditorApplication.isPlayingOrWillChangePlaymode) entities.UpdateRule(rule);
                    }
                    catch { }

                    EditorGUI.indentLevel = 0;
                }

                #endregion Service

                #region Context

                HorizontalLine();

                isXapiContextFoldoutOpen = EditorGUILayout.Foldout(isXapiContextFoldoutOpen, "Context", true);

                if (isXapiContextFoldoutOpen)
                {
                    EditorGUI.indentLevel = 1;

                    try
                    {
                        // Create a scriptable object out of the current routing rule
                        SerializedObject sobj = new SerializedObject(SelectedXapiConfig.DefaultStatement.Context);

                        Color cachedGuiColor = GUI.color;
                        sobj.Update();
                        var property = sobj.GetIterator();
                        var next = property.NextVisible(true);
                        if (next)
                            do
                            {
                                GUI.color = cachedGuiColor;
                                bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                bool cachedGUIEnabled = GUI.enabled;
                                if (isdefaultScriptProperty) continue; // don't render

                                EditorGUILayout.PropertyField(property, GUILayout.Width(350));

                                if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                            } while (property.NextVisible(false));
                        sobj.ApplyModifiedProperties();

                        // If we're playing try to keep rules updated in realtime
                        //if (EditorApplication.isPlayingOrWillChangePlaymode) entities.UpdateRule(rule);
                    }
                    catch { }

                    EditorGUI.indentLevel = 0;
                }

                #endregion Service

                #region Default Actor

                HorizontalLine();

                isXapiActorFoldoutOpen = EditorGUILayout.Foldout(isXapiActorFoldoutOpen, "Default Actor", true);

                if (isXapiActorFoldoutOpen)
                {
                    EditorGUI.indentLevel = 1;

                    try
                    {
                        // Create a scriptable object out of the current routing rule
                        SerializedObject sobj = new SerializedObject(SelectedXapiConfig.DefaultStatement.Actor);

                        Color cachedGuiColor = GUI.color;
                        sobj.Update();
                        var property = sobj.GetIterator();
                        var next = property.NextVisible(true);
                        if (next)
                            do
                            {
                                GUI.color = cachedGuiColor;
                                bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                bool cachedGUIEnabled = GUI.enabled;
                                if (isdefaultScriptProperty) continue; // don't render

                                EditorGUILayout.PropertyField(property, GUILayout.Width(600));

                                if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                            } while (property.NextVisible(false));
                        sobj.ApplyModifiedProperties();

                        // If we're playing try to keep rules updated in realtime
                        //if (EditorApplication.isPlayingOrWillChangePlaymode) entities.UpdateRule(rule);
                    }
                    catch { }

                    EditorGUI.indentLevel = 0;
                }

                #endregion  Default Actor

                #region Default Verb

                HorizontalLine();

                isXapiVerbFoldoutOpen = EditorGUILayout.Foldout(isXapiVerbFoldoutOpen, "Default Verb", true);

                if (isXapiVerbFoldoutOpen)
                {
                    EditorGUI.indentLevel = 1;

                    try
                    {
                        // Create a scriptable object out of the current routing rule
                        SerializedObject sobj = new SerializedObject(SelectedXapiConfig.DefaultStatement.Verb);

                        Color cachedGuiColor = GUI.color;
                        sobj.Update();
                        var property = sobj.GetIterator();
                        var next = property.NextVisible(true);
                        if (next)
                            do
                            {
                                GUI.color = cachedGuiColor;
                                bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                bool cachedGUIEnabled = GUI.enabled;
                                if (isdefaultScriptProperty) continue; // don't render

                                EditorGUILayout.PropertyField(property, GUILayout.Width(600));

                                if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                            } while (property.NextVisible(false));
                        sobj.ApplyModifiedProperties();

                        // If we're playing try to keep rules updated in realtime
                        //if (EditorApplication.isPlayingOrWillChangePlaymode) entities.UpdateRule(rule);
                    }
                    catch { }

                    EditorGUI.indentLevel = 0;
                }

                #endregion  Default Verb

                #region Default Object

                HorizontalLine();

                isXapiObjectFoldoutOpen = EditorGUILayout.Foldout(isXapiObjectFoldoutOpen, "Default Object", true);

                if (isXapiObjectFoldoutOpen)
                {
                    EditorGUI.indentLevel = 1;

                    try
                    {
                        // Create a scriptable object out of the current routing rule
                        SerializedObject sobj = new SerializedObject(SelectedXapiConfig.DefaultStatement.Object);

                        Color cachedGuiColor = GUI.color;
                        sobj.Update();
                        var property = sobj.GetIterator();
                        var next = property.NextVisible(true);
                        if (next)
                            do
                            {
                                GUI.color = cachedGuiColor;
                                bool isdefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
                                bool cachedGUIEnabled = GUI.enabled;
                                if (isdefaultScriptProperty) continue; // don't render

                                EditorGUILayout.PropertyField(property, GUILayout.Width(600));

                                if (isdefaultScriptProperty) GUI.enabled = cachedGUIEnabled;

                            } while (property.NextVisible(false));
                        sobj.ApplyModifiedProperties();

                        // If we're playing try to keep rules updated in realtime
                        //if (EditorApplication.isPlayingOrWillChangePlaymode) entities.UpdateRule(rule);
                    }
                    catch { }

                    EditorGUI.indentLevel = 0;
                }

                #endregion  Default Object
            }

            EditorGUILayout.EndScrollView();
        }

        #endregion Render xAPI GUI

        #region Utility

        #region Add xAPI Config

        // Creates an xAPI configuration
        private XapiConfiguration AddNewXapiConfiguration(string name)
        {
            var config = CreateInstance<XapiConfiguration>();
            config.Name = name;

            // Create the default statement
            config.DefaultStatement = CreateInstance<XapiStatement>();
            config.DefaultStatement.Actor = CreateInstance<XapiActor>();
            config.DefaultStatement.Context = CreateInstance<XapiContext>();
            config.DefaultStatement.Object = CreateInstance<XapiObject>();
            config.DefaultStatement.Verb = CreateInstance<XapiVerb>();

            if (scene != null)
            {
                if (scene.AllXapiConfigurations == null) scene.AllXapiConfigurations = new List<XapiConfiguration>();
                if (xapiActors != null) xapiActors.Clear();
                if (xapiVerbs != null) xapiVerbs.Clear();
                if (xapiObjects != null) xapiObjects.Clear();
                //Debug.LogFormat("Selected Sessions entities configuration '{0}'", name);
                Undo.RegisterCreatedObjectUndo(config, "New xAPI Configuration Created");
                scene.ApplyXapiConfiguration();
            }

            return config;
        }

        #endregion Add xAPI Config

        #region Select xAPI Config

        // Selects an entities configuration
        private void SelectXapiConfig(XapiConfiguration config, bool applyToScene = true)
        {
            //Debug.Log("Selecting: " + (config != null ? config.Name : "NULL"));
            SelectedXapiConfig = config;

            if (config != null)
            {
                if (scene != null && applyToScene)
                {
                    scene.XapiConfiguration = config;
                    scene.ApplyXapiConfiguration();

                    if (!EditorApplication.isPlaying)
                    {
                        EditorUtility.SetDirty(scene);
                        EditorSceneManager.MarkAllScenesDirty();
                    }
                }

                // Ensure proper edit index for rows
                //config.Entities = config.Entities != null ? (from e in config.Entities where e != null select e).ToArray() : new SessionsNetworkEntityInfo[0];
                //for (var i = 0; i < config.Entities.Length; i++) config.Entities[i].EditIndex = i;
            }

            if (xapiActors == null) xapiActors = new List<XapiActor>();
            else xapiActors.Clear();

            if (xapiVerbs == null) xapiVerbs = new List<XapiVerb>();
            else xapiVerbs.Clear();

            if (xapiObjects == null) xapiObjects = new List<XapiObject>();
            else xapiObjects.Clear();

            //if (xapiActors != null && config.xapiActors != null && config.xapiActors.Length > 0) networkEntities.AddRange(config.Entities);

            Repaint();
        }

        private IEnumerator DoSelectXapiConfig(XapiConfiguration config, float delay = 0.5f)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            SelectXapiConfig(config);
            yield break;
        }

        #endregion Select xAPI Config

        #region Export xAPI Config

        // Exports current entities configuration to disk
        private void ExportXapiConfig()
        {
            if (SelectedXapiConfig == null) return;

            EditorUtility.DisplayDialog("//TODO", "Imeplement xAPI configuration export", "OK");

            // Create a folder for this configuration to contain the child rules
            var xapiPath = "Assets/CymaticLabs/Sessions/Resources/Xapi";

            if (!AssetDatabase.IsValidFolder(xapiPath))
            {
                AssetDatabase.CreateFolder("Assets/CymaticLabs/Sessions/Resources", "Xapi");
            }

            // We need to delay after potential folder creation otherwise the Editor will yell at us when we try to create the child assets in it
            this.StartCoroutine(DoFinishSaveXapiConfig(xapiPath, true));
        }

        // Finishes saving the rule configuration
        IEnumerator DoFinishSaveXapiConfig(string xapiPath, bool waitForPath)
        {
            if (waitForPath)
            {
                while (!AssetDatabase.IsValidFolder(xapiPath))
                {
                    //Debug.Log("Waiting");
                    yield return new WaitForSeconds(0.25f);
                }
            }

            //// Save out entries first
            //var entries = (from e in networkEntities where e != null select e).ToArray();

            //// Create the asset for each rule
            //for (var i = 0; i < entries.Length; i++)
            //{
            //    var entityInfo = entries[i];
            //    var configName = GetSafeFilename(SelectedEntitiesConfig.Name).Replace(" ", "");
            //    entityInfo.Name = GetSafeFilename(entityInfo.Name);
            //    var assetPath = xapiPath + "/" + configName + "-" + entityInfo.Name + ".asset";

            //    // Create this rule as an asset if it doesn't already exist
            //    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(entityInfo)))
            //    {
            //        AssetDatabase.CreateAsset(entityInfo, assetPath);
            //    }
            //    // Otherwise mark it dirty for saving
            //    else
            //    {
            //        //EditorUtility.SetDirty(rule);
            //    }
            //}

            //// Apply the freshly saved rules
            //SelectedEntitiesConfig.Entities = entries;

            //// Mark dirty and save
            //AssetDatabase.CreateAsset(SelectedEntitiesConfig, xapiPath + "/" + GetSafeFilename(SelectedEntitiesConfig.Name).Replace(" ", "") + ".asset");
            //EditorUtility.SetDirty(SelectedEntitiesConfig);
            //AssetDatabase.SaveAssets();
            //ShowNotification(new GUIContent("Entities configuration files exported"));

            //// TODO Delta on names and delete orphaned assets
        }

        #endregion Export xAPI Config

        #region Delete xAPI Config

        // Deletes the current routing config
        private void DeleteXapiConfig()
        {
            if (SelectedXapiConfig == null) return;
            var name = SelectedXapiConfig.Name;

            if (scene == null || scene.AllXapiConfigurations == null || scene.AllXapiConfigurations.Count == 0)
            {
                SelectXapiConfig(null);
                return;
            }
            else
            {
                if (scene.XapiConfiguration == SelectedXapiConfig) scene.XapiConfiguration = null;

                if (scene.AllXapiConfigurations != null)
                {
                    scene.AllXapiConfigurations.Remove(SelectedXapiConfig);
                    scene.ApplyXapiConfiguration();
                }
            }

            Undo.DestroyObjectImmediate(SelectedXapiConfig);

            Debug.LogFormat("Sessions xAPI Configuration deleted: {0}", name);
        }

        #endregion Delete xAPI Config

        #region Get xAPI Config Path

        // Returns the xAPI config asset folder
        private string GetXapiConfigPath()
        {
            var sc = Path.DirectorySeparatorChar;
            return Directory.GetCurrentDirectory() + sc + "Assets" + sc + "CymaticLabs" + sc + "Sessions" + sc + "Resources" + sc + "xAPI";
        }

        #endregion Get xAPI Config Path

        #endregion Utility

        #endregion xAPI

        #region Utility

        /// <summary>
        /// Draws a horizontal line break.
        /// </summary>
        /// <param name="color">The color of the line.</param>
        public void HorizontalLine(Color? color = null)
        {
            if (color == null) color = HorizontalLineColor;
            var c = GUI.color;
            GUI.color = color.Value;
            GUILayout.Box(GUIContent.none, horizontalLineStyle);
            GUI.color = c;
        }

        /// <summary>
        /// Removes illegal filename characters from a text string.
        /// </summary>
        /// <param name="filename">The text to remove the characters from.</param>
        /// <returns>The text with all illegal file characters removed.</returns>
        public static string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        #endregion Utility

        #endregion Methods
    }
}
