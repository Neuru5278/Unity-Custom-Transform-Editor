#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

#pragma warning disable CS0618 // Obsolete

// 날짜 : 2021-02-17 02:06
// 작성자 : Rito

namespace Rito
{
    [CustomEditor(typeof(Transform))]
    public class TransformEditor : UnityEditor.Editor
    {
        private Transform transform;

        private static bool globalFoldOut;

        private Texture refreshTexture;
        private string texturePath;

        private object _localRotationGUI; // 사용 이유 : 안쓰면 Euler.x 짐벌락 걸림
        private System.Reflection.MethodInfo _trguiOnEnableMethod;
        private System.Reflection.MethodInfo _trguiRotationFieldMethod;

        private static Vector3[] copiedValues = new Vector3[3];
        private static bool hasCopiedValues;
        private static int contextChoice;

        private void OnEnable()
        {
            transform = target as Transform;

            texturePath = TransformEditorHelper.FolderPath + @"\EditorResources\Refresh.png";
            refreshTexture = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;

            // 치트키 : 기존 TransformEditor로부터 RotationField 빌려쓰기
            if (_localRotationGUI == null)
            {
                var bunch = TransformEditorHelper.GetTransformRotationGUI();
                _localRotationGUI = bunch.rotationGUI;
                _trguiOnEnableMethod = bunch.OnEnable;
                _trguiRotationFieldMethod = bunch.RotationField;
            }

            _trguiOnEnableMethod.Invoke(_localRotationGUI, new object[] {
                base.serializedObject.FindProperty("m_LocalRotation"), EditorGUIUtility.TrTextContent("Local Rotation")
            });

        }
        /***********************************************************************
        *                               Inspector Methods
        ***********************************************************************/
        #region .
        public override void OnInspectorGUI()
        {
            // 1. Local Transform
            DrawDefaultTransformInspector();

            EditorGUILayout.Space();

            // 2. Global Transform
            EditorGUI.BeginChangeCheck();
            if (globalFoldOut = EditorGUILayout.Foldout(globalFoldOut, "Global"))
            {
                Rect globalRect = GUILayoutUtility.GetLastRect();
                DrawGlobalTransformInspector();
                EditorGUILayout.Space();

                Event e = Event.current;
                Vector2 m = e.mousePosition;
                if (e.type == EventType.ContextClick)
                {
                    contextChoice = 0;
                    if (globalRect.Contains(m)) contextChoice = 7;
                    if (contextChoice > 0)
                    {
                        GenericMenu myMenu = new GenericMenu();
                        myMenu.AddItem(new GUIContent("Copy"), false, CopyFieldValues);
                        myMenu.AddItem(new GUIContent("Paste"), false, hasCopiedValues ? new GenericMenu.MenuFunction(PasteFieldValues) : null);
                        myMenu.AddItem(new GUIContent("Reset"), false, ResetFieldValues);
                        myMenu.ShowAsContext();
                    }
                }
            }
            Rect foldRect = GUILayoutUtility.GetLastRect();
            Event f = Event.current;
            Vector2 n = f.mousePosition;
            if (f.type == EventType.ContextClick)
            {
                contextChoice = 0;
                if (foldRect.Contains(n)) contextChoice = 7;
                if (contextChoice > 0)
                {
                    GenericMenu myMenu = new GenericMenu();
                    myMenu.AddItem(new GUIContent("Copy"), false, CopyFieldValues);
                    myMenu.AddItem(new GUIContent("Paste"), false, hasCopiedValues ? new GenericMenu.MenuFunction(PasteFieldValues) : null);
                    myMenu.AddItem(new GUIContent("Reset"), false, ResetFieldValues);
                    myMenu.ShowAsContext();
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                TransformEditorHelper.SaveGlobalFoldOutPref(globalFoldOut);
            }



            //base.serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefaultTransformInspector()
        {
            // Reset Button
            Color oldBgColor = GUI.backgroundColor;
            Color oldTxtColor = GUI.contentColor;
            GUI.backgroundColor = Color.green;
            GUI.contentColor = Color.white;
            if (GUILayout.Button("Reset"))
            {
                transform.localPosition = Vector3.zero;
                transform.localScale = Vector3.one;

                TransformUtils.SetInspectorRotation(transform, Vector3.zero);
            }
            GUI.backgroundColor = oldBgColor;
            GUI.contentColor = oldTxtColor;

            //base.serializedObject.Update();
            // ==================================== Local Position =========================================
            EditorGUILayout.BeginHorizontal();

            Undo.RecordObject(transform, "Transform Local Position Changed");

            if (globalFoldOut)
                transform.localPosition = Vector3e4Round(transform.localPosition);

            // Local Position Field
            transform.localPosition = EditorGUILayout.Vector3Field("Local Position", transform.localPosition);
            Rect posRect = GUILayoutUtility.GetLastRect();

            // Refresh Button
            DrawRefreshButton(Color.green, () => transform.localPosition = Vector3.zero);

            EditorGUILayout.EndHorizontal();
            // ==================================== Local Rotation =========================================
            EditorGUILayout.BeginHorizontal();

            Undo.RecordObject(transform, "Transform Local Rotation Changed");

            // Local Rotation Field
            _trguiRotationFieldMethod.Invoke(_localRotationGUI, new object[] { });
            Rect rotRect = GUILayoutUtility.GetLastRect();

            // 0~360 값 벗어나면 제한
            Vector3 exposedLocalEulerAngle = TransformUtils.GetInspectorRotation(transform);
            if (exposedLocalEulerAngle.x < 0f || exposedLocalEulerAngle.y < 0f || exposedLocalEulerAngle.z < 0f ||
                exposedLocalEulerAngle.x > 360f || exposedLocalEulerAngle.y > 360f || exposedLocalEulerAngle.z > 360f)
            {
                TransformUtils.SetInspectorRotation(transform, transform.localEulerAngles);
            }

            // Refresh Button
            DrawRefreshButton(Color.green, () => TransformUtils.SetInspectorRotation(transform, Vector3.zero));

            EditorGUILayout.EndHorizontal();
            // ==================================== Local Scale =========================================
            EditorGUILayout.BeginHorizontal();

            Undo.RecordObject(transform, "Transform Local Scale Changed");

            if (globalFoldOut)
                transform.localScale = Vector3e4Round(transform.localScale);

            // Local Scale Field
            transform.localScale = EditorGUILayout.Vector3Field("Local Scale", transform.localScale);
            Rect scaleRect = GUILayoutUtility.GetLastRect();

            // Refresh Button
            DrawRefreshButton(Color.green, () => transform.localScale = Vector3.one);

            EditorGUILayout.EndHorizontal();

            Event e = Event.current;
            Vector2 m = e.mousePosition;
            if (e.type == EventType.ContextClick)
            {
                contextChoice = 0;
                if (posRect.Contains(m)) contextChoice = 1;
                if (rotRect.Contains(m)) contextChoice = 2;
                if (scaleRect.Contains(m)) contextChoice = 3;
                if (contextChoice > 0)
                {
                    GenericMenu myMenu = new GenericMenu();
                    myMenu.AddItem(new GUIContent("Copy"), false, CopyFieldValues);
                    myMenu.AddItem(new GUIContent("Paste"), false, hasCopiedValues ? new GenericMenu.MenuFunction(PasteFieldValues) : null);
                    myMenu.AddItem(new GUIContent("Reset"), false, ResetFieldValues);
                    myMenu.ShowAsContext();
                }
            }
        }

        private void DrawGlobalTransformInspector()
        {
            // Reset Button
            Color oldBgColor = GUI.backgroundColor;
            Color oldTxtColor = GUI.contentColor;
            GUI.backgroundColor = Color.blue;
            GUI.contentColor = Color.white;
            if (GUILayout.Button("Reset"))
            {
                transform.position = Vector3.zero;
                transform.eulerAngles = Vector3.zero;
                ChangeGlobalScale(Vector3.one);
            }
            GUI.backgroundColor = oldBgColor;
            GUI.contentColor = oldTxtColor;

            // ==================================== Global Position =========================================
            EditorGUILayout.BeginHorizontal();

            Undo.RecordObject(transform, "Transform Global Position Changed");
            transform.position = EditorGUILayout.Vector3Field("Global Position", Vector3e4Round(transform.position));
            Rect posRect = GUILayoutUtility.GetLastRect();

            // Refresh Button
            DrawRefreshButton(Color.blue, () => transform.position = Vector3.zero);

            EditorGUILayout.EndHorizontal();

            // ==================================== Global Rotation =========================================
            EditorGUILayout.BeginHorizontal();

            Undo.RecordObject(transform, "Transform Global Rotation Changed");

            Vector3 globalRot = transform.eulerAngles;
            globalRot = EditorGUILayout.Vector3Field("Global Rotation", Vector3e4Round(globalRot));
            Rect rotRect = GUILayoutUtility.GetLastRect();

            // 90 ~ 270 각도 건너뛰기
            if (90f < globalRot.x && globalRot.x < 180f) globalRot.x += 180f;
            else if (180 < globalRot.x && globalRot.x < 270f) globalRot.x -= 180f;

            transform.eulerAngles = globalRot;//Vector3E4Round(globalRot);

            // Refresh Button
            DrawRefreshButton(Color.blue, () => transform.eulerAngles = Vector3.zero);

            EditorGUILayout.EndHorizontal();
            // ==================================== Global Scale =========================================
            EditorGUILayout.BeginHorizontal();

            Undo.RecordObject(transform, "Transform Global Scale Changed");
            Vector3 changedGlobalScale = EditorGUILayout.Vector3Field("Global Scale", Vector3e4Round(transform.lossyScale));
            Rect scaleRect = GUILayoutUtility.GetLastRect();
            ChangeGlobalScale(changedGlobalScale);

            // Refresh Button
            DrawRefreshButton(Color.blue, () => ChangeGlobalScale(Vector3.one));

            EditorGUILayout.EndHorizontal();

            Event e = Event.current;
            Vector2 m = e.mousePosition;
            if (e.type == EventType.ContextClick)
            {
                contextChoice = 0;
                if (posRect.Contains(m)) contextChoice = 4;
                if (rotRect.Contains(m)) contextChoice = 5;
                if (scaleRect.Contains(m)) contextChoice = 6;
                if (contextChoice > 0)
                {
                    GenericMenu myMenu = new GenericMenu();
                    myMenu.AddItem(new GUIContent("Copy"), false, CopyFieldValues);
                    myMenu.AddItem(new GUIContent("Paste"), false, hasCopiedValues ? new GenericMenu.MenuFunction(PasteFieldValues) : null);
                    myMenu.AddItem(new GUIContent("Reset"), false, ResetFieldValues);
                    myMenu.ShowAsContext();
                }
            }
        }

        #endregion
        /***********************************************************************
        *                               Private Methods
        ***********************************************************************/
        #region .
        /// <summary> 버튼을 그립니당 </summary>
        private void DrawRefreshButton(in Color color, System.Action action)
        {
            Color oldBgColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            if (GUILayout.Button(refreshTexture, GUILayout.Width(20f), GUILayout.Height(18f)))
            {
                action();
            }
            GUI.backgroundColor = oldBgColor;
        }

        System.Collections.Generic.Queue<Vector3> _scaleHierarchy = new System.Collections.Generic.Queue<Vector3>();
        private void ChangeGlobalScale(Vector3 globalScale)
        {
            _scaleHierarchy.Clear();
            Transform parentTr = transform.parent;
            while (parentTr != null)
            {
                _scaleHierarchy.Enqueue(parentTr.localScale);
                parentTr = parentTr.parent;
            }

            while (_scaleHierarchy.Count > 0)
            {
                var current = _scaleHierarchy.Dequeue();
                float x = globalScale.x / current.x;
                float y = globalScale.y / current.y;
                float z = globalScale.z / current.z;
                globalScale.Set(x, y, z);
            }

            transform.localScale = globalScale;
        }

        /// <summary> 소수 4번째 자리까지 반올림 </summary>
        private Vector3 Vector3e4Round(Vector3 vector)
        {
            vector.x = Mathf.Round(vector.x * 10000f) * 0.0001f;
            vector.y = Mathf.Round(vector.y * 10000f) * 0.0001f;
            vector.z = Mathf.Round(vector.z * 10000f) * 0.0001f;
            return vector;
        }

        private void ResetFieldValues()
        {
            Undo.RecordObject(transform, "Transform Changed");
            switch (contextChoice)
            {
                case 1:
                    transform.localPosition = Vector3.zero;
                    break;
                case 2:
                    TransformUtils.SetInspectorRotation(transform, Vector3.zero);
                    break;
                case 3:
                    transform.localScale = Vector3.one;
                    break;
                case 4:
                    transform.position = Vector3.zero;
                    break;
                case 5:
                    transform.eulerAngles = Vector3.zero;
                    break;
                case 6:
                    ChangeGlobalScale(Vector3.one); ;
                    break;
                case 7:
                    transform.position = Vector3.zero;
                    transform.eulerAngles = Vector3.zero;
                    ChangeGlobalScale(Vector3.one);
                    break;
            }
            serializedObject.ApplyModifiedProperties();
        }
        private void CopyFieldValues()
        {
            hasCopiedValues = true;
            switch (contextChoice)
            {
                case 1:
                    copiedValues[0] = transform.localPosition;
                    break;
                case 2:
                    copiedValues[0] = transform.localEulerAngles;
                    break;
                case 3:
                    copiedValues[0] = transform.localScale;
                    break;
                case 4:
                    copiedValues[0] = transform.position;
                    break;
                case 5:
                    copiedValues[0] = transform.eulerAngles;
                    break;
                case 6:
                    copiedValues[0] = transform.lossyScale;
                    break;
                case 7:
                    copiedValues[0] = transform.position;
                    copiedValues[1] = transform.eulerAngles;
                    copiedValues[2] = transform.lossyScale;
                    break;
            }
        }
        private void PasteFieldValues()
        {
            Undo.RecordObject(transform, "Transform Changed");
            switch (contextChoice)
            {
                case 1:
                    transform.localPosition = Vector3e4Round(copiedValues[0]);
                    break;
                case 2:
                    TransformUtils.SetInspectorRotation(transform, copiedValues[0]);
                    break;
                case 3:
                    transform.localScale = Vector3e4Round(copiedValues[0]);
                    break;
                case 4:
                    transform.position = Vector3e4Round(copiedValues[0]);
                    break;
                case 5:
                    transform.eulerAngles = Vector3e4Round(copiedValues[0]);
                    break;
                case 6:
                    ChangeGlobalScale(Vector3e4Round(copiedValues[0]));
                    break;
                case 7:
                    transform.position = Vector3e4Round(copiedValues[0]);
                    transform.eulerAngles = Vector3e4Round(copiedValues[1]);
                    ChangeGlobalScale(Vector3e4Round(copiedValues[2]));
                    break;
            }
            serializedObject.ApplyModifiedProperties();
        }
        #endregion
        /***********************************************************************
        *                               Public Methods
        ***********************************************************************/
        #region .
        public static void LoadGlobalFoldOutValue(bool value)
        {
            globalFoldOut = value;
        }

        #endregion
    }
}
#endif
