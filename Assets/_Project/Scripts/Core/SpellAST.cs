using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Abstract Syntax Tree (AST) del Hechizo.
    /// Representa la estructura semantica final extraida de los trazos.
    /// </summary>
    [Serializable]
    public class SpellAST
    {
        public bool HasRing;
        public RingNode Ring;
    }

    [Serializable]
    public class RingNode
    {
        public SigilNode Sigil;
        public List<SignNode> Signs = new List<SignNode>();
    }

    [Serializable]
    public class SigilNode
    {
        public int ClassIndex;
        public float Confidence;
    }

    [Serializable]
    public class SignNode
    {
        public int ClassIndex;
        public float Confidence;
    }
}
