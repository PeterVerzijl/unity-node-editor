﻿using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor {

    [HideInInspector]
    public class NodeOutput : ScriptableObject
    {
        [HideInInspector]
        public Node body;

        [HideInInspector]
        public Rect outputRect = new Rect();

        //[HideInInspector]
        public List<NodeInput> connections = new List<NodeInput>();

        [HideInInspector]
        public string type;

        [System.NonSerialized]
        private object value = null;

        private static System.Type valueType;

        public T GetValue<T>()
            where T : class, new()
        {
            if (valueType == null || valueType == typeof(ConnectionTypes))
                valueType = ConnectionTypes.GetOutputType(type);

            if (valueType == typeof(T))
            {
                if (value == null)
                    value = new T();
                return (T)value;
            }
            UnityEngine.Debug.LogError("Trying to GetValue<" + typeof(T).FullName + "> for Output Type: " + type);

            return null;
        }

        public T SetValue<T>()
            where T : class, new()
        {
            if (valueType == null)
                valueType = ConnectionTypes.GetOutputType(type);

            if (valueType == typeof(T))
            {
                if (value == null)
                    value = new T();
                return (T)value;
            }
            UnityEngine.Debug.LogError("Trying to SetValue<" + typeof(T).FullName + "> for Output Type: " + type);

            return null;
        }

        /// <summary>
        /// Creates a new NodeOutput in NodeBody of specified type
        /// </summary>
        public static NodeOutput Create(Node NodeBody, string OutputName, string OutputType)
        {
            NodeOutput output = CreateInstance<NodeOutput>();
            output.body = NodeBody;
            output.type = OutputType;
            output.name = OutputName;
            NodeBody.Outputs.Add(output);
            return output;
        }

        /// <summary>
        /// Function to automatically draw and update the output with a label for it's name
        /// </summary>
        public void DisplayLayout()
        {
            DisplayLayout(new GUIContent(name));
        }

        /// <summary>
        /// Function to automatically draw and update the output
        /// </summary>
        public void DisplayLayout(GUIContent content)
        {
    #if UNITY_EDITOR
            GUIStyle style = new GUIStyle (UnityEditor.EditorStyles.label);
    #else
            GUIStyle style = new GUIStyle();
    #endif
            style.alignment = TextAnchor.MiddleRight;
            GUILayout.Label(content, style);
            if (Event.current.type == EventType.Repaint)
                SetRect(GUILayoutUtility.GetLastRect());
        }

        /// <summary>
        /// Set the output rect as labelrect in global canvas space and extend it to the right node edge
        /// </summary>
        public void SetRect(Rect labelRect)
        {
            outputRect = new Rect(body.rect.x + labelRect.x,
                                   body.rect.y + labelRect.y + 20,
                                   body.rect.width - labelRect.x,
                                   labelRect.height);
        }

        /// <summary>
        /// Get the rect of the knob right to the output NOT ZOOMED; Used for GUI drawing in scaled areas
        /// </summary>
        public Rect GetGUIKnob()
        {
            Rect knobRect = new Rect(outputRect);
            knobRect.position += NodeEditor.curEditorState.zoomPanAdjust;
            return TransformOutputRect(knobRect);
        }

        /// <summary>
        /// Get the rect of the knob right to the output ZOOMED; Used for input checks; Representative of the actual screen rect
        /// </summary>
        public Rect GetScreenKnob()
        {
            return NodeEditor.GUIToScreenRect(TransformOutputRect(outputRect));
        }

        /// <summary>
        /// Transforms the output rect to the knob format
        /// </summary>
        private Rect TransformOutputRect(Rect knobRect)
        {
            float knobSize = (float)NodeEditor.knobSize;
            return new Rect(knobRect.x + knobRect.width,
                             knobRect.y + (knobRect.height - knobSize) / 2,
                             knobSize, knobSize);
        }
    }
}