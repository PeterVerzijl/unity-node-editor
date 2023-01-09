using System;
using UnityEngine;

namespace NodeEditor {
    public class NodeConditionType : ITypeDeclaration {
        public string name { get { return "Condition"; } }
        public Color col { get { return Color.green; } }
        public string InputKnob_TexPath { get { return "Textures/In_Knob.png"; } }
        public string OutputKnob_TexPath { get { return "Textures/Out_Knob.png"; } }
        public Type InputType { get { return typeof(Condition); } }
        public Type OutputType { get { return typeof(Condition); } }
    }

    [Serializable]
    public class Condition {
        public Condition() { }

        public bool value;
        public string trigger = "";
    }
}