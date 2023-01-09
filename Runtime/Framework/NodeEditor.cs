﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor {
    public static class NodeEditor
    {
        // The NodeCanvas which represents the currently drawn Node Canvas; globally accessed
        public static NodeCanvas curNodeCanvas;

        public static NodeEditorState curEditorState;

        // Quick access
        public static Vector2 mousePos;

        // Settings
        public static int knobSize = 18;

        // Static textures and styles
        public static Texture2D Box;

        public static Texture2D InputKnob;
        public static Texture2D OutputKnob;
        public static Texture2D Background;
        public static Texture2D AALineTex;
        public static GUIStyle nodeBox;
        public static GUIStyle nodeButton;
        public static GUIStyle nodeLabel;
        public static GUIStyle nodeLabelBold;

        // Constants
        public const string editorPath = "Assets/Node_Editor/";
#if UNITY_EDITOR
        public static string assetPath;
#endif

        #region Setup

        [NonSerialized]
        private static bool initiated = false;

        public static void checkInit()
        {
            if (!initiated)
            {
#if UNITY_EDITOR
                assetPath = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NodeEditor).Assembly).assetPath;

                InputKnob = LoadTexture("Textures/In_Knob.png");
                OutputKnob = LoadTexture("Textures/Out_Knob.png");
                Background = LoadTexture("Textures/background.png");

                AALineTex = LoadTexture("Textures/AALine.png");
#endif

                ConnectionTypes.FetchTypes();
                NodeTypes.FetchNodes();

                // Styles
                nodeBox = new GUIStyle(GUI.skin.box);
    #if UNITY_EDITOR
                nodeBox.normal.background = ColorToTex(/*UnityEditor.EditorGUIUtility.isProSkin?*/ new Color(0.5f, 0.5f, 0.5f, 0.5f) /*: new Color (0.2f, 0.2f, 0.2f)*/);
    #else
                nodeBox.normal.background = ColorToTex(new Color(0.5f, 0.5f, 0.5f));
    #endif
                nodeBox.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

                nodeButton = new GUIStyle(GUI.skin.button);

                nodeLabel = new GUIStyle(GUI.skin.label);
                nodeLabel.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

                nodeLabelBold = new GUIStyle(nodeLabel);
                nodeLabelBold.fontStyle = FontStyle.Bold;
                nodeLabelBold.wordWrap = false;

                initiated = true;
            }
        }

        #endregion Setup

        #region GUI

        /// <summary>
        /// Draws the Node Canvas on the screen in the rect specified by editorState. Has to be called out of any GUI Group or area because of zooming.
        /// </summary>
        public static void DrawCanvas(NodeCanvas nodeCanvas, NodeEditorState editorState)
        {
            if (!editorState.drawing)
                return;

            curNodeCanvas = nodeCanvas;
            curEditorState = editorState;

            //		if (curEditorState.parent != null)
            //			curEditorState.canvasRect.position += curEditorState.parent.zoomPanAdjust;

            if (Event.current.type == EventType.Repaint)
            { // Draw Background when Repainting
                GUI.BeginClip(curEditorState.canvasRect);

                float width = Background.width / curEditorState.zoom;
                float height = Background.height / curEditorState.zoom;
                Vector2 offset = new Vector2((curEditorState.panOffset.x / curEditorState.zoom) % width - width,
                                              (curEditorState.panOffset.y / curEditorState.zoom) % height - height);
                int tileX = Mathf.CeilToInt((curEditorState.canvasRect.width + (width - offset.x)) / width);
                int tileY = Mathf.CeilToInt((curEditorState.canvasRect.height + (height - offset.y)) / height);

                for (int x = 0; x < tileX; x++)
                {
                    for (int y = 0; y < tileY; y++)
                    {
                        Rect texRect = new Rect(offset.x + x * width,
                                                 offset.y + y * height,
                                                 width, height);

                        GUI.DrawTexture(texRect, Background);
                    }
                }
                GUI.EndClip();
            }

            //		if (curEditorState.parent != null)
            //			curEditorState.canvasRect.position -= curEditorState.parent.zoomPanAdjust - curEditorState.parent.zoomPos;

            // Fetch all nested nodeEditors and set their canvasRects to be ignored by input,
            // so it can be handled later, and note it down, as it will be drawn later ontop
            List<Rect> ignoreInput = new List<Rect>();
            for (int childCnt = 0; childCnt < curEditorState.childs.Count; childCnt++)
            {
                if (curEditorState.childs[childCnt].drawing)
                {
                    NodeEditorState nestedEditor = curEditorState.childs[childCnt];
                    ignoreInput.Add(GUIToScreenRect(nestedEditor.canvasRect));
                }
            }

            // Check the inputs
            InputEvents(ignoreInput);

            // We want to scale our nodes, but as GUI.matrix also scales our widnow's clipping group,
            // we have to scale it up first to receive a correct one as a result

            #region Scale Setup

            // End the default clipping group
            GUI.EndGroup();

            // The Rect of the new clipping group to draw our nodes in
            Rect ScaledCanvasRect = ScaleRect(curEditorState.canvasRect, curEditorState.zoomPos + curEditorState.canvasRect.position, new Vector2(curEditorState.zoom, curEditorState.zoom));
            ScaledCanvasRect.y += 23; // Header tab height

    #if UNITY_EDITOR
            if (curNodeCanvas != NodeEditorWindow.MainNodeCanvas)
                GUI.DrawTexture(ScaledCanvasRect, Background);
    #endif

            // Now continue drawing using the new clipping group
            GUI.BeginGroup(ScaledCanvasRect);
            ScaledCanvasRect.position = Vector2.zero; // Adjust because we entered the new group

            // Because I currently found no way to actually scale to the center of the window rather than (0, 0),
            // I'm going to cheat and just pan it accordingly to let it appear as if it would scroll to the center
            // Note, due to that, other controls are still scaled to (0, 0)
            curEditorState.zoomPanAdjust = ScaledCanvasRect.center - curEditorState.canvasRect.size / 2 + curEditorState.zoomPos;

            // Take a matrix backup to restore back later on
            Matrix4x4 GUIMatrix = GUI.matrix;

            // Scale GUI.matrix. After that we have the correct clipping group again.
            GUIUtility.ScaleAroundPivot(new Vector2(1 / curEditorState.zoom, 1 / curEditorState.zoom), curEditorState.zoomPanAdjust);

            #endregion Scale Setup

            // Some features which require drawing (zoomed)
            if (curEditorState.navigate)
            { // Draw a curve to the origin/active node for orientation purposes
                DrawNodeCurve((curEditorState.activeNode != null ? curEditorState.activeNode.rect.center : curEditorState.panOffset), ScreenToGUIPos(mousePos) + curEditorState.zoomPos * curEditorState.zoom, Color.black);
    #if UNITY_EDITOR
                NodeEditorWindow.editor.Repaint();
    #endif
            }
            if (curEditorState.connectOutput != null)
            { // Draw the currently drawn connection
                DrawNodeCurve(curEditorState.connectOutput.GetGUIKnob().center, ScreenToGUIPos(mousePos) + curEditorState.zoomPos * curEditorState.zoom, ConnectionTypes.GetTypeData(curEditorState.connectOutput.type).col);
    #if UNITY_EDITOR
                NodeEditorWindow.editor.Repaint();
    #endif
            }
    #if UNITY_EDITOR
            if (curNodeCanvas != NodeEditorWindow.MainNodeCanvas)
                return;
    #endif
            // Draw their connectors; Seperated because of render order
            for (int nodeCnt = 0; nodeCnt < curNodeCanvas.nodes.Count; nodeCnt++)
                if (curNodeCanvas.nodes[nodeCnt] != null)
                {
                    curNodeCanvas.nodes[nodeCnt].DrawConnections();
                }
            for (int nodeCnt = 0; nodeCnt < curNodeCanvas.nodes.Count; nodeCnt++)
                if (curNodeCanvas.nodes[nodeCnt] != null)
                {
                    curNodeCanvas.nodes[nodeCnt].DrawKnobs();
                }
            // Draw the nodes
            for (int nodeCnt = 0; nodeCnt < curNodeCanvas.nodes.Count; nodeCnt++)
            {
                Node node = curNodeCanvas.nodes[nodeCnt];
                //if (node != curEditorState.activeNode)
                if (curNodeCanvas.nodes[nodeCnt] != null)
                {
                    NodeEditor.DrawNode(node);
                }
            }

            // Draw the active Node ontop
            //		if (editorState.activeNode != null)
            //			NodeEditor.DrawNode (curEditorState.activeNode);

            // Draw any node groups out there. Has to be drawn here, because they still need to scale according to their parents, but they mustn't be drawn inside a GUI group
            for (int editorCnt = 0; editorCnt < curEditorState.childs.Count; editorCnt++)
            {
                if (curEditorState.childs[editorCnt].drawing)
                {
                    NodeEditorState nestedEditor = curEditorState.childs[editorCnt];
                    nestedEditor.canvasRect.position += curEditorState.zoomPanAdjust;
                    //GUI.DrawTexture (nestedEditor.canvasRect, Background);
                    DrawCanvas(nestedEditor.canvas, nestedEditor);
                    nestedEditor.canvasRect.position -= curEditorState.zoomPanAdjust;
                }
            }
            curNodeCanvas = nodeCanvas;
            curEditorState = editorState;

            // End scaling group
            // Set default matrix and clipping group for the rest
            GUI.matrix = GUIMatrix;
            GUI.EndGroup();
            if (curNodeCanvas.parent == null)
            {
    #if UNITY_EDITOR
                GUI.BeginGroup(new Rect(0, 23, NodeEditorWindow.editor.position.width, NodeEditorWindow.editor.position.height));
    #endif
            }
            else
            {
                Rect parentGroupRect = ScaleRect(curEditorState.parent.canvasRect, curEditorState.parent.zoomPos + curEditorState.parent.canvasRect.position, new Vector2(curEditorState.parent.zoom, curEditorState.parent.zoom));
                parentGroupRect.y += 23; // Header tab height
                GUI.BeginGroup(parentGroupRect);
            }
            // Check events with less priority than node GUI controls
            LateEvents(ignoreInput);
        }

        #endregion GUI

        #region GUI Functions

        /// <summary>
        /// Returns the node at the position in the current canvas spcae. Depends on curEditorState and curNodecanvas
        /// </summary>
        public static Node NodeAtPosition(Vector2 pos)
        {
            return NodeAtPosition(curEditorState, curNodeCanvas, pos);
        }

        /// <summary>
        /// Returns the node at the position in specified canvas space.
        /// </summary>
        public static Node NodeAtPosition(NodeEditorState editorState, NodeCanvas nodecanvas, Vector2 pos)
        {
            if (!editorState.canvasRect.Contains(pos))
                return null;

            // Check if we clicked inside a window (or knobSize pixels left or right of it at outputs, for faster knob recognition)
            float KnobSize = (float)NodeEditor.knobSize / editorState.zoom;
            if (editorState.activeNode != null)
            { // active Node is drawn ontop, so we check it first
                Rect NodeRect = new Rect(GUIToScreenRect(editorState.activeNode.rect));
                NodeRect = new Rect(NodeRect.x - KnobSize, NodeRect.y, NodeRect.width + KnobSize * 2, NodeRect.height);
                if (NodeRect.Contains(pos))
                    return editorState.activeNode;
            }
            for (int nodeCnt = nodecanvas.nodes.Count - 1; nodeCnt >= 0; nodeCnt--)
            { // checked from top to bottom because of the render order
                if (nodecanvas.nodes[nodeCnt] != null)
                {
                    Rect NodeRect = new Rect(GUIToScreenRect(nodecanvas.nodes[nodeCnt].rect));
                    NodeRect = new Rect(NodeRect.x - KnobSize, NodeRect.y, NodeRect.width + KnobSize * 2, NodeRect.height);
                    if (NodeRect.Contains(pos))
                        return nodecanvas.nodes[nodeCnt];
                }
            }
            return null;
        }

        /// <summary>
        /// Draws the node. Depends on curEditorState
        /// </summary>
        public static void DrawNode(Node node)
        {
            // TODO (Peter): Node Editor Feature: Custom Windowing System
            Rect nodeRect = node.rect;
            nodeRect.position += curEditorState.zoomPanAdjust;
            float headerHeight = 20;
            Rect headerRect = new Rect(nodeRect.x, nodeRect.y, nodeRect.width, headerHeight);
            Rect bodyRect = new Rect(nodeRect.x, nodeRect.y + headerHeight, nodeRect.width, nodeRect.height - headerHeight);

            GUIStyle headerStyle = new GUIStyle(nodeBox);
            if (curEditorState.activeNode == node)
                headerStyle.fontStyle = FontStyle.Bold;
            GUI.Label(headerRect, new GUIContent(node.name), headerStyle);
            // Draw inner gui of the node
            GUILayout.BeginArea(bodyRect, nodeBox);
            node.NodeGUI();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws a node curve from start to end (with three shades of shadows)
        /// </summary>
        public static void DrawNodeCurve(Vector2 start, Vector2 end, Color col)
        {
            Vector3 startPos = new Vector3(start.x, start.y);
            Vector3 endPos = new Vector3(end.x, end.y);
            Vector3 startTan = startPos + Vector3.right * 50;
            Vector3 endTan = endPos + Vector3.left * 50;
            Color shadowColor = new Color(0, 0, 0, 0.1f);

            for (int i = 0; i < 3; i++) // Draw a shadow with 3 shades
                DrawBezier(startPos, endPos, startTan, endTan, shadowColor, null, (i + 1) * 7); // increasing width for fading shadow
            DrawBezier(startPos, endPos, startTan, endTan, col * Color.gray, null, 5);
        }

        /// <summary>
        /// Draws a Bezier curve just as UnityEditor.Handles.DrawBezier
        /// </summary>
        public static void DrawBezier(Vector2 startPos, Vector2 endPos, Vector2 startTan, Vector2 endTan, Color col, Texture2D tex, float width)
        {
    #if UNITY_EDITOR
            UnityEditor.Handles.DrawBezier(startPos, endPos, startTan, endTan, col, tex, width);
    #else
            int segmentCount = (int)(((startPos - startTan).magnitude + (startTan - endTan).magnitude + (endTan - endPos).magnitude) / 10);
            tex = tex != null ? Tint(tex, col) : Tint(AALineTex, col);
            Vector2 curPoint = startPos;
            for (int cnt = 1; cnt <= segmentCount; cnt++)
            {
                float t = (float)cnt / segmentCount;
                Vector2 nextPoint = new Vector2(startPos.x * Mathf.Pow(1 - t, 3) +
                                                 startTan.x * 3 * Mathf.Pow(1 - t, 2) * t +
                                                 endTan.x * 3 * (1 - t) * Mathf.Pow(t, 2) +
                                                 endPos.x * Mathf.Pow(t, 3),

                                                 startPos.y * Mathf.Pow(1 - t, 3) +
                                                 startTan.y * 3 * Mathf.Pow(1 - t, 2) * t +
                                                 endTan.y * 3 * (1 - t) * Mathf.Pow(t, 2) +
                                                 endPos.y * Mathf.Pow(t, 3));
                DrawLine(curPoint, nextPoint, col, tex, width);
                curPoint = nextPoint;
            }
    #endif
        }

        public static void DrawLine(Vector2 startPos, Vector2 endPos, Color col, Texture2D tex, float width)
        {
            Vector2 dir = endPos - startPos;
            float angle = Mathf.Rad2Deg * Mathf.Atan(dir.y / dir.x);
            if (dir.x < 0)
                angle += 180;

            float widthHalf = Mathf.Ceil(width / 2);

            Matrix4x4 GUIMatrix = GUI.matrix;
            Color GUIColor = GUI.color;

            GUI.color = col;
            GUIUtility.RotateAroundPivot(angle, startPos);

            GUI.DrawTexture(new Rect(startPos.x, startPos.y - widthHalf, dir.magnitude, width),
                             tex != null ? tex : ColorToTex(Color.white));

            GUI.matrix = GUIMatrix;
            GUI.color = GUIColor;
        }

        /// <summary>
        /// Scales the rect around the pivot with scale
        /// </summary>
        public static Rect ScaleRect(Rect rect, Vector2 pivot, Vector2 scale)
        {
            rect.position = Vector2.Scale(rect.position - pivot, scale) + pivot;
            rect.size = Vector2.Scale(rect.size, scale);
            return rect;
        }

        /// <summary>
        /// Transforms the Rect in GUI space into Screen space. Depends on curEditorState
        /// </summary>
        public static Rect GUIToScreenRect(Rect rect)
        {
            return GUIToScreenRect(curEditorState, rect);
        }

        /// <summary>
        /// Transforms the Rect in GUI space into Screen space
        /// </summary>
        public static Rect GUIToScreenRect(NodeEditorState editorState, Rect rect)
        {
            rect.position += editorState.zoomPos;
            rect = ScaleRect(rect, editorState.zoomPos, new Vector2(1 / editorState.zoom, 1 / editorState.zoom));
            rect.position += editorState.canvasRect.position;
            return rect;
        }

        /// <summary>
        /// Transforms screen position pos (like mouse pos) to a point in current GUI space
        /// </summary>
        ///
        public static Vector2 ScreenToGUIPos(Vector2 pos)
        {
            return ScreenToGUIPos(curEditorState, pos);
        }

        /// <summary>
        /// Transforms screen position pos (like mouse pos) to a point in specified GUI space
        /// </summary>
        ///
        public static Vector2 ScreenToGUIPos(NodeEditorState editorState, Vector2 pos)
        {
            return Vector2.Scale(pos - editorState.zoomPos - editorState.canvasRect.position, new Vector2(editorState.zoom, editorState.zoom));
        }

        /// <summary>
        /// Loads a texture in both the editor and at runtime (coming soon)
        /// </summary>
        public static Texture2D LoadTexture(string texPath)
        {
    #if UNITY_EDITOR
            string fullPath = System.IO.Path.Combine(assetPath, "Editor", "Resources", texPath);
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (tex == null)
                Debug.LogError(string.Format("NodeEditor: Texture not found at '{0}', did you install Node_Editor correctly in the 'Plugins' folder?", texPath));
            return tex;
    #else
            Texture2D tex = Resources.Load<Texture2D>(texPath);
            if (tex == null)
                Debug.LogError(string.Format("NodeEditor: Texture not found at '{0}' in any Resource Folder!", texPath));
            return tex;
    #endif
        }

        /// <summary>
        /// Create a 1x1 tex with color col
        /// </summary>
        public static Texture2D ColorToTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(1, 1, col);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Tint the texture with the color.
        /// </summary>
        public static Texture2D Tint(Texture2D tex, Color color)
        {
            Texture2D tintedTex = UnityEngine.Object.Instantiate(tex);
            for (int x = 0; x < tex.width; x++)
                for (int y = 0; y < tex.height; y++)
                    tintedTex.SetPixel(x, y, tex.GetPixel(x, y) * color);
            tintedTex.Apply();
            return tintedTex;
        }

        #endregion GUI Functions

        #region Input Events

        /// <summary>
        /// Processes input events
        /// </summary>
        public static void InputEvents(List<Rect> ignoreInput)
        {
            Event e = Event.current;
            mousePos = e.mousePosition;

            bool insideCanvas = curEditorState.canvasRect.Contains(e.mousePosition);
            for (int ignoreCnt = 0; ignoreCnt < ignoreInput.Count; ignoreCnt++)
            {
                if (ignoreInput[ignoreCnt].Contains(e.mousePosition))
                {
                    insideCanvas = false;
                    break;
                }
            }

            Node clickedNode = null;
            if (insideCanvas && (e.type == EventType.MouseDown || e.type == EventType.MouseUp))
                clickedNode = NodeEditor.NodeAtPosition(e.mousePosition);

    #if UNITY_EDITOR
            if (clickedNode != null)
            {
                UnityEditor.Selection.activeObject = clickedNode;
            }
    #endif

            switch (e.type)
            {
                case EventType.MouseDown:

                    if (!insideCanvas)
                        break;

                    if (e.button == 0)
                        curEditorState.activeNode = clickedNode;

                    curEditorState.connectOutput = null;
                    curEditorState.dragNode = false;
                    curEditorState.panWindow = false;

                    if (clickedNode != null)
                    { // A click on a node
                        if (e.button == 1)
                        { // Right click -> Node Context Click
    #if UNITY_EDITOR
                            UnityEditor.GenericMenu menu = new UnityEditor.GenericMenu();

                            menu.AddItem(new GUIContent("Delete Node"), false, ContextCallback, new callbackObject("deleteNode", curNodeCanvas, curEditorState, clickedNode));
                            menu.AddItem(new GUIContent("Duplicate Node"), false, ContextCallback, new callbackObject("duplicateNode", curNodeCanvas, curEditorState, clickedNode));

                            menu.ShowAsContext();
    #endif
                            e.Use();
                        }
                        else if (e.button == 0 && !GUIToScreenRect(clickedNode.rect).Contains(e.mousePosition))
                        { // Left click at node edges -> Check for clicked connections to edit
                            NodeOutput nodeOutput = clickedNode.GetOutputAtPos(e.mousePosition);
                            if (nodeOutput != null)
                            { // Output Node -> New Connection drawn from this
                                curEditorState.connectOutput = nodeOutput;
                                curEditorState.connectMousePos = mousePos;

                                e.Use();
                            }
                            else
                            { // no output clicked, check input
                                NodeInput nodeInput = clickedNode.GetInputAtPos(e.mousePosition);
                                if (nodeInput != null && nodeInput.connections.Count > 0)
                                { // Input node -> Loose and edit Connection (always the first one)
                                    curEditorState.connectOutput = nodeInput.connections[0];
                                    nodeInput.connections[0].connections.Remove(nodeInput);
                                    nodeInput.connections.Remove(nodeInput.connections[0]);
                                    e.Use();
                                }
                            }
                        }
                    }
                    else
                    { // A click on the empty canvas
                        if (e.button == 2 || e.button == 0)
                        { // Left/Middle Click -> Start scrolling
                            curEditorState.panWindow = true;
                            e.delta = Vector2.zero;
                        }
                        else if (e.button == 1 && curEditorState.connectOutput == null)
                        { // Right click -> Editor Context Click
    #if UNITY_EDITOR
                            UnityEditor.GenericMenu menu = new UnityEditor.GenericMenu();

                            foreach (Node node in NodeTypes.nodes.Keys)
                            {
                                menu.AddItem(new GUIContent("Add " + NodeTypes.nodes[node]), false, ContextCallback, new callbackObject(node.GetID, curNodeCanvas, curEditorState));
                            }

                            menu.ShowAsContext();
    #endif
                            e.Use();
                        }
                    }

                    break;

                case EventType.MouseUp:

                    if (curEditorState.connectOutput != null && insideCanvas)
                    { // Apply a connection if theres a clicked input
                        if (clickedNode != null && !clickedNode.Outputs.Contains(curEditorState.connectOutput))
                        { // If an input was clicked, it'll will now be connected
                            NodeInput clickedInput = clickedNode.GetInputAtPos(e.mousePosition);
                            if (Node.CanApplyConnection(curEditorState.connectOutput, clickedInput))
                            { // If it can connect (type is equals, it does not cause recursion, ...)
                                Node.ApplyConnection(curEditorState.connectOutput, clickedInput);
                            }
                        }
                        else if (e.button == 1)
                        { // Show menu containing all node types that can take curEditorState.connectOutput as an input
    #if UNITY_EDITOR
                            UnityEditor.GenericMenu menu = new UnityEditor.GenericMenu();
                            // Iterate through all compatible nodes
                            foreach (Node node in NodeTypes.nodes.Keys)
                            {
                                foreach (var input in node.Inputs)
                                {
                                    if (input.type == curEditorState.connectOutput.type)
                                    {
                                        menu.AddItem(new GUIContent("Add " + NodeTypes.nodes[node]), false, ContextCallback, new callbackObject(node.GetID, curNodeCanvas, curEditorState, null, curEditorState.connectOutput));
                                        break;
                                    }
                                }
                            }

                            menu.ShowAsContext();
    #endif
                        }
                        e.Use();
                    }

                    curEditorState.connectOutput = null;
                    curEditorState.dragNode = false;
                    curEditorState.panWindow = false;

                    break;

                case EventType.ScrollWheel:

                    if (insideCanvas)
                        curEditorState.zoom = Mathf.Min(2.0f, Mathf.Max(0.6f, curEditorState.zoom + e.delta.y / 15));
    #if UNITY_EDITOR
                    NodeEditorWindow.editor.Repaint();
    #endif
                    break;

                case EventType.KeyDown:

                    if (!insideCanvas)
                        break;
                    // TODO (Peter): Node Editor: Shortcuts
                    if (e.keyCode == KeyCode.N) // Start Navigating (curve to origin / active Node)
                        curEditorState.navigate = true;

                    if (e.keyCode == KeyCode.LeftControl && curEditorState.activeNode != null) // Snap
                        curEditorState.activeNode.rect.position = new Vector2(Mathf.RoundToInt((curEditorState.activeNode.rect.position.x - curEditorState.panOffset.x) / 10) * 10 + curEditorState.panOffset.x,
                                                                               Mathf.RoundToInt((curEditorState.activeNode.rect.position.y - curEditorState.panOffset.y) / 10) * 10 + curEditorState.panOffset.y);
    #if UNITY_EDITOR
                    NodeEditorWindow.editor.Repaint();
    #endif
                    break;

                case EventType.KeyUp:

                    if (e.keyCode == KeyCode.N) // Stop Navigating
                        curEditorState.navigate = false;
    #if UNITY_EDITOR
                    NodeEditorWindow.editor.Repaint();
    #endif
                    break;
            }

            // Some features that need constant updating
            if (curEditorState.panWindow)
            { // Scroll everything with the current mouse delta
                curEditorState.panOffset += e.delta / 2 * curEditorState.zoom;
                for (int nodeCnt = 0; nodeCnt < curNodeCanvas.nodes.Count; nodeCnt++)
                    if (curNodeCanvas.nodes[nodeCnt])
                    {
                        curNodeCanvas.nodes[nodeCnt].rect.position += e.delta / 2 * curEditorState.zoom;
                    }
    #if UNITY_EDITOR
                NodeEditorWindow.editor.Repaint();
    #endif
            }

            if (curEditorState.dragNode && curEditorState.activeNode != null && GUIUtility.hotControl == 0)
            { // Drag the active node with the current mouse delta
                curEditorState.activeNode.rect.position += e.delta / 2 * curEditorState.zoom;
    #if UNITY_EDITOR
                NodeEditorWindow.editor.Repaint();
    #endif
            }
            else
                curEditorState.dragNode = false;
        }

        /// <summary>
        /// Proccesses late events. Called after GUI Functions, when they have higher priority in focus
        /// </summary>
        public static void LateEvents(List<Rect> ignoreInput)
        {
            Event e = Event.current;

            bool insideCanvas = curEditorState.canvasRect.Contains(e.mousePosition);
            for (int ignoreCnt = 0; ignoreCnt < ignoreInput.Count; ignoreCnt++)
            {
                if (ignoreInput[ignoreCnt].Contains(e.mousePosition))
                {
                    insideCanvas = false;
                    break;
                }
            }

            if (e.type == EventType.MouseDown && e.button == 0 &&
                curEditorState.activeNode != null && insideCanvas && GUIToScreenRect(curEditorState.activeNode.rect).Contains(e.mousePosition))
            { // Left click inside activeNode -> Drag Node
              // Because of hotControl we have to put it after the GUI Functions
                if (GUIUtility.hotControl == 0)
                { // We didn't clicked on GUI module, so we'll start dragging the node
                    curEditorState.dragNode = true;
                    // Because this is the delta from when it was last checked, we have to reset it each time
                    e.delta = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Context Click selection. Here you'll need to register your own using a string identifier
        /// </summary>
        public static void ContextCallback(object obj)
        {
            callbackObject cbObj = obj as callbackObject;
            curNodeCanvas = cbObj.canvas;
            curEditorState = cbObj.editor;

            switch (cbObj.message)
            {
                case "deleteNode":
                    if (cbObj.node != null)
                        cbObj.node.Delete();
                    break;

                case "duplicateNode":
                    if (cbObj.node != null)
                    {
                        ContextCallback(new callbackObject(cbObj.node.GetID, curNodeCanvas, curEditorState));
                        Node duplicatedNode = curNodeCanvas.nodes[curNodeCanvas.nodes.Count - 1];
                        curEditorState.activeNode = duplicatedNode;
                        curEditorState.dragNode = true;
                    }
                    break;

                default:
                    var createPos = ScreenToGUIPos(mousePos);
                    if (cbObj.nodeOutput != null && (curEditorState.connectMousePos - mousePos).sqrMagnitude < 50)
                        createPos = new Vector2(cbObj.nodeOutput.body.rect.xMax + 50, cbObj.nodeOutput.body.rect.yMin);

                    foreach (Node node in NodeTypes.nodes.Keys)
                    {
                        if (node.GetID == cbObj.message)
                        {
                            var newNode = node.Create(createPos);
                            newNode.InitBase();
                            if (cbObj.nodeOutput != null)
                            { // If nodeOutput is defined, link it to the first input of the same type
                                foreach (var input in newNode.Inputs)
                                {
                                    if (Node.CanApplyConnection(cbObj.nodeOutput, input))
                                    { // If it can connect (type is equals, it does not cause recursion, ...)
                                        Node.ApplyConnection(cbObj.nodeOutput, input);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
        }

        public class callbackObject
        {
            public string message;
            public NodeCanvas canvas;
            public NodeEditorState editor;
            public Node node;

            /// <summary>
            /// Output node to connect to automatically
            /// </summary>
		    public NodeOutput nodeOutput;

            public callbackObject(string Message, NodeCanvas nodecanvas, NodeEditorState editorState)
            {
                message = Message;
                canvas = nodecanvas;
                editor = editorState;
                node = null;
                nodeOutput = null;
            }

            public callbackObject(string Message, NodeCanvas nodecanvas, NodeEditorState editorState, Node Node)
            {
                message = Message;
                canvas = nodecanvas;
                editor = editorState;
                node = Node;
                nodeOutput = null;
            }

            public callbackObject(string Message, NodeCanvas nodecanvas, NodeEditorState editorState, Node Node, NodeOutput NodeOutput)
            {
                message = Message;
                canvas = nodecanvas;
                editor = editorState;
                node = Node;
                nodeOutput = NodeOutput;
            }
        }

        #endregion Input Events

        #region Save/Load

        /// <summary>
        /// Loads the editorStates found in the nodeCanvas asset file at path
        /// </summary>
        public static List<NodeEditorState> LoadEditorStates(string path)
        {
    #if UNITY_EDITOR
            if (String.IsNullOrEmpty(path))
                return null;
            UnityEngine.Object[] objects = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (objects.Length == 0)
                return null;

            // Obtain the editorStates in that asset file
            List<NodeEditorState> editorStates = new List<NodeEditorState>();
            for (int cnt = 0; cnt < objects.Length; cnt++)
            {
                if (objects[cnt].GetType() == typeof(NodeEditorState))
                    editorStates.Add(objects[cnt] as NodeEditorState);
            }

            UnityEditor.AssetDatabase.Refresh();
            return editorStates;
    #else
            return null;
    #endif
        }

        /// <summary>
        /// Saves the current node canvas as a new asset and links optional editorStates with it
        /// </summary>
        public static void SaveNodeCanvas(NodeCanvas nodeCanvas, string path, params NodeEditorState[] editorStates)
        {
    #if UNITY_EDITOR
            if (String.IsNullOrEmpty(path))
                return;
            string existingPath = UnityEditor.AssetDatabase.GetAssetPath(nodeCanvas);
            if (!String.IsNullOrEmpty(existingPath))
            {
                if (existingPath != path)
                {
                    UnityEditor.AssetDatabase.CopyAsset(existingPath, path);
                    UnityEditor.AssetDatabase.SaveAssets();
                    UnityEditor.AssetDatabase.Refresh();
                }
                return;
            }
            UnityEditor.AssetDatabase.CreateAsset(nodeCanvas, path);
            foreach (NodeEditorState editorState in editorStates)
                UnityEditor.AssetDatabase.AddObjectToAsset(editorState, nodeCanvas);
            for (int nodeCnt = 0; nodeCnt < nodeCanvas.nodes.Count; nodeCnt++)
            { // Add every node and every of it's inputs/outputs into the file.
              // Results in a big mess but there's no other way (like a hierarchy)
                Node node = nodeCanvas.nodes[nodeCnt];
                UnityEditor.AssetDatabase.AddObjectToAsset(node, nodeCanvas);
                for (int inCnt = 0; inCnt < node.Inputs.Count; inCnt++)
                    UnityEditor.AssetDatabase.AddObjectToAsset(node.Inputs[inCnt], node);
                for (int outCnt = 0; outCnt < node.Outputs.Count; outCnt++)
                    UnityEditor.AssetDatabase.AddObjectToAsset(node.Outputs[outCnt], node);
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
    #endif
        }

        /// <summary>
        /// Loads the NodeCanvas in the asset file at path
        /// </summary>
        public static NodeCanvas LoadNodeCanvas(string path)
        {
    #if UNITY_EDITOR
            if (String.IsNullOrEmpty(path))
                return null;
            UnityEngine.Object[] objects = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (objects.Length == 0)
                return null;
            NodeCanvas nodeCanvas = null;

            for (int cnt = 0; cnt < objects.Length; cnt++)
            { // We only have to search for the NodeCanvas itself in the mess, because it still holds references to all of it's nodes and their connections
                if (objects[cnt] && objects[cnt].GetType() == typeof(NodeCanvas))
                    nodeCanvas = objects[cnt] as NodeCanvas;
            }
            if (nodeCanvas == null)
                return null;

            UnityEditor.AssetDatabase.Refresh();

            return nodeCanvas;
    #else
            return null;
    #endif
        }

        #endregion Save/Load
    }
}