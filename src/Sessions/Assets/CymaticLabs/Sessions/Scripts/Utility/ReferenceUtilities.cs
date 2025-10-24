using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Utility class for dealing with Unity references.
    /// </summary>
    public static class ReferenceUtilities
    {
        #region Methods

        /// <summary>
        /// Serializes a reference to a game object to string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>The serialized object string.</returns>
        public static string GetGameObjectReference(GameObject obj)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            var r = new GameObjectReference { Reference = obj };

            var json = JsonUtility.ToJson(r);
            Debug.Log(json);
#if UNITY_EDITOR
            var rst = JsonUtility.FromJson<GameObjectEditorReference>(json);
            return rst.Reference.instanceID.ToString();
#else
             var rst = JsonUtility.FromJson<GameObjectPlayerReference>(json);
             return rst.Reference.m_FileID + "/" + rst.Reference.m_PathID;
#endif
        }

        /// <summary>
        /// Parses a game object from its reference in string form.
        /// </summary>
        /// <param name="expression">The game object expression to parse.</param>
        /// <param name="res">The game object reference to deserialize into.</param>
        /// <returns>True if the reference was found, False if not.</returns>
        public static bool ParseGameObject(string expression, out GameObject res)
        {
            res = null;
            try
            {
                var exp = expression.Replace("$", "");
                var i = 0;
                if (int.TryParse(exp, out i))
                {
                    //GameObject Reference Id
                    var rid = "{\"Reference\":{\"instanceID\":" + exp + "}}";
                    Debug.Log(rid);
                    var rq = JsonUtility.FromJson<GameObjectReference>(rid);
                    res = rq.Reference;
                }
                else
                {
                    if (exp.Contains("/"))
                    {
                        var ss = exp.Split('/');
                        var i1 = int.Parse(ss[0]);
                        var i2 = int.Parse(ss[1]);
                        var rid = "{\"Reference\":{\"m_FileID\":" + i1 + ", \"m_PathID\":" + i2 + "}}";
                        Debug.LogWarning(rid);
                        var rq = JsonUtility.FromJson<GameObjectReference>(rid);
                        res = rq.Reference;
                        return true;
                    }
                    //GameObject Name
                    var obj = GameObject.Find(exp);
                    res = obj;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.Log(ex.ToString());
                return false;
            }
        }

        #endregion Methods
    }

    [System.Serializable]
    public class GameObjectReference
    {

        public GameObject Reference;

    }

    [System.Serializable]
    public class GameObjectEditorReference
    {
        public EditorReferenceRef Reference;

        [System.Serializable]
        public class EditorReferenceRef
        {
            public int instanceID;
        }
    }

    [System.Serializable]
    public class GameObjectPlayerReference
    {

        public PlayerReferenceRef Reference;

        [System.Serializable]
        public class PlayerReferenceRef
        {
            public int m_FileID;
            public int m_PathID;
        }
    }
}

