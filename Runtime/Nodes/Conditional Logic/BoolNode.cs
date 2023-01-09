using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using NodeEditor;

[System.Serializable]
[Node(false, "Conditional Logic/Bool Node")]
public class BoolNode : ConditionNode {

    /// <summary>
    /// The ID for this node
    /// </summary>
    public override string GetID { get { return ID; } }

    private string ID;

    /// <summary>
    /// The boolean that this node returns
    /// </summary>
    [SerializeField]
    private bool _value = true;

    public override bool Value {
        get { return _value; }
        set { _value = value; }
    }

    /// <summary>
    /// The name of the bool node
    /// </summary>
    public string nodeName = "";

    /// <summary>
    /// Node constructor
    /// </summary>
    /// <param name="pos">The position given by the base</param>
    /// <returns>A new instance of node</returns>
    public override Node Create(Vector2 pos) {
        BoolNode node = CreateInstance<BoolNode>();
        node.ID = "Bool Node " + node.GetInstanceID();
        node.rect = new Rect(pos.x, pos.y, 150, 130);
        node.CreateOutput("Value", "Condition");
        return node;
    }

    /// <summary>
    /// What does the node need to draw?
    /// </summary>
    public override void NodeGUI() {
        GUILayout.BeginHorizontal();
        Outputs[0].DisplayLayout();
        GUILayout.BeginVertical();
        Value = GUILayout.Toggle(Value, " " + Value.ToString());
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
#if UNITY_EDITOR
        EditorGUILayout.LabelField("Name");
#endif
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle("textField");
        style.fixedHeight = 20;
        nodeName = EditorGUILayout.TextField(nodeName, style, GUILayout.Height(130));
#endif
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Return boolean state
    /// </summary>
    /// <returns>The value of boolean</returns>
    public override bool Check() {
        return Value;
    }
}