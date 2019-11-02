﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeatmapActionContainer : MonoBehaviour
{
    private List<BeatmapAction> beatmapActions = new List<BeatmapAction>();
    private static BeatmapActionContainer instance;
    [SerializeField] private GameObject moveableGridTransform;
    [SerializeField] private SelectionController selection;
    [SerializeField] private NodeEditorController nodeEditor;
    private List<BeatmapObjectContainerCollection> collections;

    private void Start()
    {
        collections = selection.collections.ToList();
        instance = this;
    }

    /// <summary>
    /// Adds a BeatmapAction to the stack.
    /// </summary>
    /// <param name="action">BeatmapAction to add.</param>
    public static void AddAction(BeatmapAction action)
    {
        instance.beatmapActions.RemoveAll(x => !x.Active);
        instance.beatmapActions.Add(action);
        Debug.Log($"Action of type {action.GetType().Name} added.");
    }

    public void Undo()
    {
        BeatmapAction lastActive = beatmapActions.LastOrDefault(x => x.Active);
        if (lastActive == null) return;
        Debug.Log($"Undid a {lastActive.GetType().Name}.");
        BeatmapActionParams param = new BeatmapActionParams(this);
        lastActive.Undo(param);
        lastActive.Active = false;
    }

    public void Redo()
    {
        BeatmapAction firstNotActive = beatmapActions.FirstOrDefault(x => !x.Active);
        if (firstNotActive == null) return;
        Debug.Log($"Redid a {firstNotActive.GetType().Name}.");
        BeatmapActionParams param = new BeatmapActionParams(this);
        firstNotActive.Redo(param);
        firstNotActive.Active = true;
    }

    public class BeatmapActionParams
    {
        public List<BeatmapObjectContainerCollection> collections;
        public SelectionController selection;
        public NodeEditorController nodeEditor;
        public BeatmapActionParams(BeatmapActionContainer container)
        {
            collections = container.collections;
            nodeEditor = container.nodeEditor;
        }
    }
}
