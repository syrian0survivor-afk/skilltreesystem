using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkillTreeSystem
{
    [Serializable]
    public class SkillTreeExport
    {
        public string schemaVersion;
        public string generatedFrom;
        public SkillTreeModelNotes modelNotes;
        public List<SkillTreeDefinition> trees;
    }

    [Serializable]
    public class SkillTreeModelNotes
    {
        public bool edgesAreUndirected;
        public string skillActiveRule;
        public string pipePuzzleRule;
        public string prereqFillRule;
    }

    [Serializable]
    public class SkillTreeDefinition
    {
        public string treeName;
        public List<SkillTreeNode> nodes;
        public List<SkillTreeEdge> edges;
        public List<SkillTreeGate> gates;
        public string rootNodeId;
        public SkillTreeJunctionTypes junctionTypes;
    }

    [Serializable]
    public class SkillTreeNode
    {
        public string id;
        public string name;
        public string type;
        public string styleHint;
        public SkillTreeUiRect ui;
    }

    [Serializable]
    public class SkillTreeUiRect
    {
        public float x;
        public float y;
        public float w;
        public float h;
    }

    [Serializable]
    public class SkillTreeEdge
    {
        public string id;
        public List<string> endpoints;
        public bool switchable;
        public SkillTreePipePuzzle pipePuzzle;
        public string styleHint;
        public string label;
    }

    [Serializable]
    public class SkillTreePipePuzzle
    {
        public string kind;
        public List<string> tileTypes;
        public int rotationStepDegrees;
        public string pathId;
        public string note;
    }

    [Serializable]
    public class SkillTreeGate
    {
        public string id;
        public string attachedToNodeId;
        public string type;
        public int requiredPrereqUpgrades;
        public string countsToward;
        public SkillTreeGateFillRule fillRule;
    }

    [Serializable]
    public class SkillTreeGateFillRule
    {
        public string source;
        public bool countsStackedRanks;
    }

    [Serializable]
    public class SkillTreeJunctionTypes
    {
        public SkillTreeJunctionType junction_split;
        public SkillTreeJunctionType junction_multi_split_T_2of3;
    }

    [Serializable]
    public class SkillTreeJunctionType
    {
        public string rule;
    }
}
