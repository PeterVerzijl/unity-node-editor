﻿using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor {

    /// <summary>
    /// Abstract Node class used for the (old) dialogue node system.
    /// </summary>
    public abstract class Node : ScriptableObject {

        [HideInInspector]
        public Rect rect = new Rect();

        //[HideInInspector]
        public List<NodeInput> Inputs = new List<NodeInput>();

        //[HideInInspector]
        public List<NodeOutput> Outputs = new List<NodeOutput>();

        // Abstract member to get the ID of the node
        public abstract string GetID { get; }

        /// <summary>
        /// Function implemented by the children to create the node
        /// </summary>
        /// <param name="pos">Position.</param>
        public abstract Node Create(Vector2 pos);

        /// <summary>
        /// Function implemented by the children to draw the node
        /// </summary>
        public abstract void NodeGUI();

        /// <summary>
        /// Optional callback when the node is deleted
        /// </summary>
        public virtual void OnDelete() { }

        #region Member Functions

        /// <summary>
        /// Checks if there are no unassigned and no null-value inputs.
        /// </summary>
        public bool allInputsReady() {
            for (int inCnt = 0; inCnt < Inputs.Count; inCnt++) {
                foreach (NodeOutput connection in Inputs[inCnt].connections)
                    if (connection == null || connection.GetValue<FloatValue>() == null)
                        return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if there are any unassigned inputs.
        /// </summary>
        public bool hasNullInputs() {
            for (int inCnt = 0; inCnt < Inputs.Count; inCnt++) {
                foreach (NodeOutput connection in Inputs[inCnt].connections)
                    if (connection == null)
                        return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if there are any null-value inputs.
        /// </summary>
        public bool hasNullInputValues() {
            for (int inCnt = 0; inCnt < Inputs.Count; inCnt++) {
                foreach (NodeOutput connection in Inputs[inCnt].connections)
                    if (connection != null && connection.GetValue<FloatValue>() == null)
                        return true;
            }
            return false;
        }

        /// <summary>
        /// Recursively checks whether this node is a child of the other node
        /// </summary>
        public bool isChildOf(Node otherNode) {
            if (otherNode == null)
                return false;
            for (int cnt = 0; cnt < Inputs.Count; cnt++) {
                foreach (NodeOutput connection in Inputs[cnt].connections)
                    if (connection != null) {
                        if (connection.body == otherNode)
                            return true;
                        else if (connection.body.isChildOf(otherNode)) // Recursively searching
                            return true;
                    }
            }
            return false;
        }

        /// <summary>
        /// Call this method in your NodeGUI to setup an output knob aligning with the y position of the last GUILayout control drawn.
        /// </summary>
        /// <param name="outputIdx">The index of the output in the Node's Outputs list</param>
        protected void OutputKnob(int outputIdx) {
            if (Event.current.type == EventType.Repaint)
                Outputs[outputIdx].SetRect(GUILayoutUtility.GetLastRect());
        }

        /// <summary>
        /// Call this method in your NodeGUI to setup an input knob aligning with the y position of the last GUILayout control drawn.
        /// </summary>
        /// <param name="inputIdx">The index of the input in the Node's Inputs list</param>
        protected void InputKnob(int inputIdx) {
            if (Event.current.type == EventType.Repaint)
                Inputs[inputIdx].SetRect(GUILayoutUtility.GetLastRect());
        }

        /// <summary>
        /// Call this method to create an output on your node
        /// </summary>
        /// <param name="outputName">the name of the output</param>
        /// <param name="outputType">the type of the output</param>
        public void CreateOutput(string outputName, string outputType) {
            NodeOutput.Create(this, outputName, outputType);
        }

        /// <summary>
        /// Call this method to create an input on your node
        /// </summary>
        /// <param name="inputName">the name of the input</param>
        /// <param name="inputType">the type of the input</param>
        public void CreateInput(string inputName, string inputType) {
            NodeInput.Create(this, inputName, inputType);
        }

        /// <summary>
        /// Init this node. Has to be called when creating a child node
        /// </summary>
        public void InitBase() {
            NodeEditor.curNodeCanvas.nodes.Add(this);
#if UNITY_EDITOR
            if (name == "") {
                name = UnityEditor.ObjectNames.NicifyVariableName(GetID);
            }
            if (!System.String.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(NodeEditor.curNodeCanvas))) {
                UnityEditor.AssetDatabase.AddObjectToAsset(this, NodeEditor.curNodeCanvas);
                for (int inCnt = 0; inCnt < Inputs.Count; inCnt++)
                    UnityEditor.AssetDatabase.AddObjectToAsset(Inputs[inCnt], this);
                for (int outCnt = 0; outCnt < Outputs.Count; outCnt++)
                    UnityEditor.AssetDatabase.AddObjectToAsset(Outputs[outCnt], this);

                UnityEditor.AssetDatabase.ImportAsset(UnityEditor.AssetDatabase.GetAssetPath(NodeEditor.curNodeCanvas));
            }
#endif
        }

        /// <summary>
        /// Returns the input knob that is at the position on this node or null
        /// </summary>
        public NodeInput GetInputAtPos(Vector2 pos) {
            for (int inCnt = 0; inCnt < Inputs.Count; inCnt++) { // Search for an input at the position
                if (Inputs[inCnt].GetScreenKnob().Contains(new Vector3(pos.x, pos.y)))
                    return Inputs[inCnt];
            }
            return null;
        }

        /// <summary>
        /// Returns the output knob that is at the position on this node or null
        /// </summary>
        public NodeOutput GetOutputAtPos(Vector2 pos) {
            for (int outCnt = 0; outCnt < Outputs.Count; outCnt++) { // Search for an output at the position
                if (Outputs[outCnt].GetScreenKnob().Contains(new Vector3(pos.x, pos.y)))
                    return Outputs[outCnt];
            }
            return null;
        }

        /// <summary>
        /// Draws the node knobs; splitted from curves because of the render order
        /// </summary>
        public void DrawKnobs() {
            for (int outCnt = 0; outCnt < Outputs.Count; outCnt++) {
                GUI.DrawTexture(Outputs[outCnt].GetGUIKnob(), ConnectionTypes.GetTypeData(Outputs[outCnt].type).OutputKnob);
            }
            for (int inCnt = 0; inCnt < Inputs.Count; inCnt++) {
                GUI.DrawTexture(Inputs[inCnt].GetGUIKnob(), ConnectionTypes.GetTypeData(Inputs[inCnt].type).InputKnob);
            }
        }

        /// <summary>
        /// Draws the node curves; splitted from knobs because of the render order
        /// </summary>
        public virtual void DrawConnections() {
            for (int outCnt = 0; outCnt < Outputs.Count; outCnt++) {
                NodeOutput output = Outputs[outCnt];
                for (int conCnt = 0; conCnt < output.connections.Count; conCnt++) {
                    NodeEditor.DrawNodeCurve(output.GetGUIKnob().center,
                                              output.connections[conCnt].GetGUIKnob().center,
                                              ConnectionTypes.GetTypeData(output.type).col);
                }
            }
        }

        /// <summary>
        /// Deletes this Node from curNodeCanvas. Depends on that.
        /// </summary>
        public void Delete() {
            NodeEditor.curNodeCanvas.nodes.Remove(this);
            for (int outCnt = 0; outCnt < Outputs.Count; outCnt++) {
                NodeOutput output = Outputs[outCnt];

                for (int conCnt = 0; conCnt < output.connections.Count; conCnt++)
                    output.connections[conCnt].connections.Remove(output);
                DestroyImmediate(output, true);
            }
            for (int inCnt = 0; inCnt < Inputs.Count; inCnt++) {
                NodeInput input = Inputs[inCnt];
                for (int conCnt = 0; conCnt < input.connections.Count; conCnt++)
                    input.connections[conCnt].connections.Remove(input);
                DestroyImmediate(input, true);
            }
            DestroyImmediate(this, true);

#if UNITY_EDITOR
            if (!System.String.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(NodeEditor.curNodeCanvas))) {
                UnityEditor.AssetDatabase.ImportAsset(UnityEditor.AssetDatabase.GetAssetPath(NodeEditor.curNodeCanvas));
            }
#endif
            OnDelete();
        }

        #endregion Member Functions

        #region Static Functions

        /// <summary>
        /// Check if an output and an input can be connected (same type, ...)
        /// </summary>
        public static bool CanApplyConnection(NodeOutput output, NodeInput input) {
            if (input == null || output == null)
                return false;

            if (input.body == output.body)
                return false;

            foreach (NodeOutput connection in input.connections)
                if (connection == output)
                    return false;

            if (input.type != output.type)
                return false;

            return true;
        }

        /// <summary>
        /// Applies a connection between output and input. 'CanApplyConnection' has to be checked before
        /// </summary>
        public static void ApplyConnection(NodeOutput output, NodeInput input) {
            if (input != null && output != null) {
                input.connections.Add(output);
                output.connections.Add(input);
            }
        }

        #endregion Static Functions
    }
}