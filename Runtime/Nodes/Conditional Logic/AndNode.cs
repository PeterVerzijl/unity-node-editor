using UnityEngine;

#if UNITY_EDITOR

using UnityEditor;

#endif

using NodeEditor;

[System.Serializable]
[Node(false, "Conditional Logic/And Node")]
public class AndNode : ConditionNode
{
    /// <summary>
    /// The ID for this node
    /// </summary>
    public override string GetID { get { return ID; } }

    private string ID;

    /// <summary>
    /// The text of the question node
    /// </summary>
    public ConditionNode conditionA;

    /// <summary>
    /// A condition that needs to be met for a conversation to be true.
    /// </summary>
    public ConditionNode conditionB;

    /// <summary>
    /// The boolean that this node returns
    /// </summary>
    [SerializeField]
    private bool _value = true;

    public override bool Value
    {
        get { return _value; }
        set { _value = value; }
    }

    /// <summary>
    /// Node constructor
    /// </summary>
    /// <param name="pos">The position given by the base</param>
    /// <returns>A new instance of node</returns>
    public override Node Create(Vector2 pos)
    {
        AndNode node = CreateInstance<AndNode>();
        node.ID = "And Node " + node.GetInstanceID();
        node.rect = new Rect(pos.x, pos.y, 100, 70);
        node.CreateInput("Condition B", "Condition");
        node.CreateInput("Condition A", "Condition");
        node.CreateOutput("Result", "Condition");
        return node;
    }

    /// <summary>
    /// What does the node need to draw?
    /// </summary>
    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();

        // A
        if (Inputs[0].connections.Count > 0 && Inputs[0].connections[0].body != null)
        {
            conditionA = Inputs[0].connections[0].body as ConditionNode;
            GUILayout.Label(conditionA.Check().ToString());
        }
        else
            try
            {
#if UNITY_EDITOR
                conditionA = EditorGUILayout.ObjectField(conditionA, typeof(ConditionNode), false, GUILayout.ExpandWidth(true)) as ConditionNode;
#endif
            }
            catch (ExitGUIException e)
            {
                Debug.Log(e);
            }
        InputKnob(0);

        // B
        if (Inputs[1].connections.Count > 0 && Inputs[1].connections[0].body != null)
        {
            conditionB = Inputs[1].connections[0].body as ConditionNode;
            GUILayout.Label(conditionB.Check().ToString());
        }
        else
            try
            {
#if UNITY_EDITOR
                conditionB = EditorGUILayout.ObjectField(conditionB, typeof(ConditionNode), false, GUILayout.ExpandWidth(true)) as ConditionNode;
#endif
            }
            catch (ExitGUIException e)
            {
                Debug.Log(e);
            }
        InputKnob(1);

        GUILayout.EndVertical();
        GUILayout.BeginVertical();

        Outputs[0].DisplayLayout(new GUIContent(Value.ToString()));

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        // This is SUPER important since else the text in the nodes WILL reset after reload.
        // Making all your hard work for nothing when you restart unity :{
        if (GUI.changed)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    /// <summary>
    /// Weither both condition a and b are true
    /// </summary>
    /// <returns>If both are true</returns>
    public override bool Check()
    {
        // Check if we have both values
        if (conditionA != null && conditionB != null)
            return Value = (conditionA.Check() && conditionB.Check());
        // Else return false
        return Value = false;
    }
}