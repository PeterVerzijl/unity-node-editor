namespace NodeEditor {

    /// <summary>
    /// Abstract base class for conditional nodes
    /// </summary>
    public abstract class ConditionNode : Node {
        /// <summary>
        /// The boolean conditional value that this node results in
        /// </summary>
        public abstract bool Value { get; set; }

        /// <summary>
        /// The function to check the condition of the condition node
        /// </summary>
        /// <returns>Weither the condition is true.</returns>
        public abstract bool Check();
    }
}