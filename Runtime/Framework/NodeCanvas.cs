using System.Collections.Generic;
using UnityEngine;

namespace NodeEditor {
    public class NodeCanvas : ScriptableObject
    { // Just contains the nodes; an associated NodeEditorState holds the actual state now
        public NodeCanvas parent;
        public List<NodeCanvas> childs = new List<NodeCanvas>();

        public List<Node> nodes = new List<Node>();
    }
}