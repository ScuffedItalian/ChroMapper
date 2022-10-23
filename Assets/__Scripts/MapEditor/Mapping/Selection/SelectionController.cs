﻿using System;
using System.Collections.Generic;
using System.Linq;
using Beatmap.Base;
using Beatmap.Base.Customs;
using Beatmap.Enums;
using Beatmap.Helper;
using Beatmap.V2.Customs;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Big boi master class for everything Selection.
/// </summary>
public class SelectionController : MonoBehaviour, CMInput.ISelectingActions, CMInput.IModifyingSelectionActions
{
    public static HashSet<BaseObject> SelectedObjects = new HashSet<BaseObject>();
    public static HashSet<BaseObject> CopiedObjects = new HashSet<BaseObject>();
    private static float copiedBpm = 100;

    public static Action<BaseObject> ObjectWasSelectedEvent;
    public static Action SelectionChangedEvent;
    public static Action<IEnumerable<BaseObject>> SelectionPastedEvent;

    private static SelectionController instance;

    [SerializeField] private AudioTimeSyncController atsc;
    [SerializeField] private Material selectionMaterial;
    [SerializeField] private Transform moveableGridTransform;
    [SerializeField] private Color selectedColor;
    [SerializeField] private Color copiedColor;
    [SerializeField] private TracksManager tracksManager;
    [SerializeField] private EventPlacement eventPlacement;

    [SerializeField] private CreateEventTypeLabels labels;
    private bool shiftInPlace;

    private bool shiftInTime;

    public static Color SelectedColor => instance.selectedColor;
    public static Color CopiedColor => instance.copiedColor;

    // Use this for initialization
    private void Start()
    {
        instance = this;
        SelectedObjects.Clear();
    }

    public void OnPaste(InputAction.CallbackContext context)
    {
        if (context.performed) Paste();
    }

    public void OnOverwritePaste(InputAction.CallbackContext context)
    {
        if (context.performed) Paste(true, true);
    }

    public void OnDeleteObjects(InputAction.CallbackContext context)
    {
        if (context.performed) Delete();
    }

    public void OnCopy(InputAction.CallbackContext context)
    {
        if (context.performed) Copy();
    }

    public void OnCut(InputAction.CallbackContext context)
    {
        if (context.performed) Copy(true);
    }

    public void OnShiftingMovement(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        var movement = context.ReadValue<Vector2>();

        if (shiftInPlace) ShiftSelection(Mathf.RoundToInt(movement.x), Mathf.RoundToInt(movement.y));

        if (shiftInTime) MoveSelection(movement.y * (1f / atsc.GridMeasureSnapping));
    }

    public void OnActivateShiftinTime(InputAction.CallbackContext context) => shiftInTime = context.performed;

    public void OnActivateShiftinPlace(InputAction.CallbackContext context) => shiftInPlace = context.performed;

    public void OnDeselectAll(InputAction.CallbackContext context)
    {
        if (context.performed) DeselectAll();
    }

    #region Utils

    /// <summary>
    ///     Does the user have any selected objects?
    /// </summary>
    public static bool HasSelectedObjects() => SelectedObjects.Count > 0;

    /// <summary>
    ///     Does the user have any copied objects?
    /// </summary>
    public static bool HasCopiedObjects() => CopiedObjects.Count > 0;

    /// <summary>
    ///     Returns true if the given container is selected, and false if it's not.
    /// </summary>
    /// <param name="container">Container to check.</param>
    public static bool IsObjectSelected(BaseObject container) => SelectedObjects.Contains(container);

    /// <summary>
    ///     Shows what types of object groups are in the passed in group of objects through output parameters.
    /// </summary>
    /// <param name="objects">Enumerable group of objects</param>
    /// <param name="hasNoteOrObstacle">Whether or not an object is in the note or obstacle group</param>
    /// <param name="hasEvent">Whether or not an object is in the event group</param>
    /// <param name="hasBpmChange">Whether or not an object is in the bpm change group</param>
    public static void GetObjectTypes(IEnumerable<BaseObject> objects, out bool hasNoteOrObstacle, out bool hasEvent,
        out bool hasBpmChange)
    {
        hasNoteOrObstacle = false;
        hasEvent = false;
        hasBpmChange = false;
        foreach (var obj in objects)
        {
            switch (obj.ObjectType)
            {
                case ObjectType.Note:
                case ObjectType.Obstacle:
                case ObjectType.CustomNote:
                    hasNoteOrObstacle = true;
                    break;
                case ObjectType.Event:
                case ObjectType.CustomEvent:
                    hasEvent = true;
                    break;
                case ObjectType.BpmChange:
                    hasBpmChange = true;
                    break;
            }
        }
    }

    /// <summary>
    ///     Invokes a callback for all objects between a time by group
    /// </summary>
    /// <param name="start">Start time in beats</param>
    /// <param name="start">End time in beats</param>
    /// <param name="hasNoteOrObstacle">Whether or not to include the note or obstacle group</param>
    /// <param name="hasEvent">Whether or not to include the event group</param>
    /// <param name="hasBpmChange">Whether or not to include the bpm change group</param>
    /// <param name="callback">Callback with an object container and the collection it belongs to</param>
    public static void ForEachObjectBetweenTimeByGroup(float start, float end, bool hasNoteOrObstacle, bool hasEvent,
        bool hasBpmChange, Action<BeatmapObjectContainerCollection, BaseObject> callback)
    {
        var clearTypes = new List<ObjectType>();
        if (hasNoteOrObstacle)
        {
            clearTypes.AddRange(new[]
            {
                ObjectType.Note, ObjectType.Obstacle, ObjectType.CustomNote
            });
            if (Settings.Instance.Load_MapV3)
            {
                clearTypes.AddRange(new[] { ObjectType.Arc, ObjectType.Chain });
            }
        }

        if (hasNoteOrObstacle && !hasEvent)
            clearTypes.Add(ObjectType.Event); //for rotation events
        if (hasEvent)
        {
            clearTypes.AddRange(new[]
            {
                ObjectType.Event, ObjectType.CustomEvent, ObjectType.BpmChange
            });
        }

        var epsilon = 1f / Mathf.Pow(10, Settings.Instance.TimeValueDecimalPrecision);
        foreach (var type in clearTypes)
        {
            var collection = BeatmapObjectContainerCollection.GetCollectionForType(type);
            if (collection == null) continue;

            foreach (var toCheck in collection.LoadedObjects.Where(x =>
                x.Time > start - epsilon && x.Time < end + epsilon))
            {
                if (!hasEvent && toCheck is BaseEvent mapEvent &&
                    !mapEvent
                        .IsLaneRotationEvent()) //Includes only rotation events when neither of the two objects are events
                {
                    continue;
                }

                callback?.Invoke(collection, toCheck);
            }
        }
    }

    #endregion

    #region Selection

    /// <summary>
    ///     Select an individual container.
    /// </summary>
    /// <param name="container">The container to select.</param>
    /// <param name="addsToSelection">Whether or not previously selected objects will deselect before selecting this object.</param>
    /// <param name="addActionEvent">If an action event to undo the selection should be made</param>
    public static void Select(BaseObject obj, bool addsToSelection = false, bool automaticallyRefreshes = true,
        bool addActionEvent = true)
    {
        if (!addsToSelection)
            DeselectAll(); //This SHOULD deselect every object unless you otherwise specify, but it aint working.
        var collection = BeatmapObjectContainerCollection.GetCollectionForType(obj.ObjectType);

        if (!collection.LoadedObjects.Contains(obj))
            return;

        SelectedObjects.Add(obj);
        if (collection.LoadedContainers.TryGetValue(obj, out var container))
            container.SetOutlineColor(instance.selectedColor);
        if (addActionEvent)
        {
            ObjectWasSelectedEvent.Invoke(obj);
            SelectionChangedEvent?.Invoke();
        }
    }

    /// <summary>
    ///     Selects objects between 2 objects, sorted by group.
    /// </summary>
    /// <param name="first">The beatmap object at the one end of the selection.</param>
    /// <param name="second">The beatmap object at the other end of the selection</param>
    /// <param name="addsToSelection">Whether or not previously selected objects will deselect before selecting this object.</param>
    /// <param name="addActionEvent">If an action event to undo the selection should be made</param>
    public static void SelectBetween(BaseObject first, BaseObject second, bool addsToSelection = false,
        bool addActionEvent = true)
    {
        if (!addsToSelection)
            DeselectAll(); //This SHOULD deselect every object unless you otherwise specify, but it aint working.
        if (first.Time > second.Time)
            (first, second) = (second, first);
        GetObjectTypes(new[] { first, second }, out var hasNoteOrObstacle, out var hasEvent, out var hasBpmChange);
        ForEachObjectBetweenTimeByGroup(first.Time, second.Time, hasNoteOrObstacle, hasEvent, hasBpmChange,
            (collection, beatmapObject) =>
            {
                if (SelectedObjects.Contains(beatmapObject)) return;
                SelectedObjects.Add(beatmapObject);
                if (collection.LoadedContainers.TryGetValue(beatmapObject, out var container))
                    container.SetOutlineColor(instance.selectedColor);
                if (addActionEvent) ObjectWasSelectedEvent.Invoke(beatmapObject);
            });
        if (addActionEvent)
            SelectionChangedEvent?.Invoke();
    }

    /// <summary>
    ///     Deselects a container if it is currently selected
    /// </summary>
    /// <param name="obj">The container to deselect, if it has been selected.</param>
    public static void Deselect(BaseObject obj, bool removeActionEvent = true)
    {
        SelectedObjects.Remove(obj);
        if (BeatmapObjectContainerCollection.GetCollectionForType(obj.ObjectType).LoadedContainers.TryGetValue(obj, out var container)
            && container != null)
        {
            container.OutlineVisible = false;
        }
        if (removeActionEvent) SelectionChangedEvent?.Invoke();
    }

    /// <summary>
    ///     Deselect all selected objects.
    /// </summary>
    public static void DeselectAll(bool removeActionEvent = true)
    {
        foreach (var obj in SelectedObjects.ToArray()) Deselect(obj, false);
        if (removeActionEvent) SelectionChangedEvent?.Invoke();
    }

    /// <summary>
    ///     Can be very taxing. Use sparringly.
    /// </summary>
    internal static void RefreshSelectionMaterial(bool triggersAction = true)
    {
        foreach (var data in SelectedObjects)
        {
            var collection = BeatmapObjectContainerCollection.GetCollectionForType(data.ObjectType);
            if (collection.LoadedContainers.TryGetValue(data, out var con))
            {
                con.OutlineVisible = true;
                con.SetOutlineColor(instance.selectedColor);
            }
        }
        //if (triggersAction) BeatmapActionContainer.AddAction(new SelectionChangedAction(SelectedObjects));
    }

    #endregion

    #region Manipulation

    /// <summary>
    ///     Deletes and clears the current selection.
    /// </summary>
    public void Delete(bool triggersAction = true)
    {
        IEnumerable<BaseObject> objects = SelectedObjects.ToArray();
        if (triggersAction) BeatmapActionContainer.AddAction(new SelectionDeletedAction(objects));
        DeselectAll();
        foreach (var con in objects)
            BeatmapObjectContainerCollection.GetCollectionForType(con.ObjectType).DeleteObject(con, false, false);
    }

    /// <summary>
    ///     Copies the current selection for later Pasting.
    /// </summary>
    /// <param name="cut">Whether or not to delete the original selection after copying them.</param>
    public void Copy(bool cut = false)
    {
        if (!HasSelectedObjects()) return;
        CopiedObjects.Clear();
        var firstTime = SelectedObjects.OrderBy(x => x.Time).First().Time;
        foreach (var data in SelectedObjects)
        {
            var collection = BeatmapObjectContainerCollection.GetCollectionForType(data.ObjectType);
            if (collection.LoadedContainers.TryGetValue(data, out var con)) con.SetOutlineColor(instance.copiedColor);
            var copy = BeatmapFactory.Clone(data);
            copy.Time -= firstTime;
            CopiedObjects.Add(copy);
        }

        if (cut) Delete();
        var bpmChanges =
            BeatmapObjectContainerCollection.GetCollectionForType<BPMChangeGridContainer>(ObjectType.BpmChange);
        var lastBpmChange = bpmChanges.FindLastBpm(atsc.CurrentBeat);
        copiedBpm = lastBpmChange?.Bpm ?? atsc.Song.BeatsPerMinute;
    }

    /// <summary>
    ///     Pastes any copied objects into the map, selecting them immediately.
    /// </summary>
    public void Paste(bool triggersAction = true, bool overwriteSection = false)
    {
        DeselectAll();

        // Set up stuff that we need
        var pasted = new List<BaseObject>();
        var collections = new Dictionary<ObjectType, BeatmapObjectContainerCollection>();

        // Grab the last BPM Change to warp distances between copied objects and maintain BPM.
        var bpmChanges =
            BeatmapObjectContainerCollection.GetCollectionForType<BPMChangeGridContainer>(ObjectType.BpmChange);

        var lowerValue = new V2BpmChange(atsc.CurrentBeat - 0.01f, 420);
        var upperValue = new V2BpmChange(atsc.CurrentBeat, 69);

        var lastBpmChangeBeforePaste = bpmChanges.FindLastBpm(atsc.CurrentBeat);

        // This first loop creates copy of the data to be pasted.
        foreach (var data in CopiedObjects)
        {
            if (data == null) continue;

            upperValue.Time = atsc.CurrentBeat + data.Time;

            var bpmChangeView = bpmChanges.LoadedObjects.GetViewBetween(lowerValue, upperValue);

            var bpmTime = data.Time * (copiedBpm / (lastBpmChangeBeforePaste?.Bpm ?? copiedBpm));

            if (bpmChangeView.Any())
            {
                var firstBpmChange = bpmChangeView.First() as BaseBpmEvent;

                bpmTime = firstBpmChange.Time - atsc.CurrentBeat;

                for (var i = 0; i < bpmChangeView.Count - 1; i++)
                {
                    var leftBpm = bpmChangeView.ElementAt(i) as BaseBpmEvent;
                    var rightBpm = bpmChangeView.ElementAt(i + 1) as BaseBpmEvent;

                    bpmTime += (rightBpm.Time - leftBpm.Time) * (copiedBpm / leftBpm.Bpm);
                }

                var lastBpmChange = bpmChangeView.Last() as BaseBpmEvent;
                bpmTime += (atsc.CurrentBeat + data.Time - lastBpmChange.Time) * (copiedBpm / lastBpmChange.Bpm);
            }

            var newTime = bpmTime + atsc.CurrentBeat;

            var newData = BeatmapFactory.Clone(data);
            newData.Time = newTime;

            if (!collections.TryGetValue(newData.ObjectType, out var collection))
            {
                collection = BeatmapObjectContainerCollection.GetCollectionForType(newData.ObjectType);
                collections.Add(newData.ObjectType, collection);
            }

            pasted.Add(newData);
        }

        var totalRemoved = new List<BaseObject>();

        // We remove conflicting objects with our to-be-pasted objects.
        foreach (var kvp in collections)
        {
            kvp.Value.RemoveConflictingObjects(pasted.Where(x => x.ObjectType == kvp.Key), out var conflicting);
            totalRemoved.AddRange(conflicting);
        }

        // While we're at it, we will also overwrite the entire section if we have to.
        if (overwriteSection)
        {
            var start = pasted.First().Time;
            var end = pasted.First().Time;
            foreach (var beatmapObject in pasted)
            {
                if (start > beatmapObject.Time)
                    start = beatmapObject.Time;
                if (end < beatmapObject.Time)
                    end = beatmapObject.Time;
            }

            GetObjectTypes(pasted, out var hasNoteOrObstacle, out var hasEvent, out var hasBpmChange);
            var toRemove = new List<(BeatmapObjectContainerCollection, BaseObject)>();
            ForEachObjectBetweenTimeByGroup(start, end, hasNoteOrObstacle, hasEvent, hasBpmChange,
                (collection, beatmapObject) =>
                {
                    if (pasted.Contains(beatmapObject)) return;
                    toRemove.Add((collection, beatmapObject));
                });
            foreach (var pair in toRemove)
            {
                var collection = pair.Item1;
                var beatmapObject = pair.Item2;
                collection.DeleteObject(beatmapObject, false);
                totalRemoved.Add(beatmapObject);
            }
        }

        // We then spawn our pasted objects into the map and select them.
        foreach (var data in pasted)
        {
            collections[data.ObjectType].SpawnObject(data, false, false);
            Select(data, true, false, false);
        }

        foreach (var collection in collections.Values)
        {
            collection.RefreshPool();

            if (collection is BPMChangeGridContainer con) con.RefreshModifiedBeat();
        }

        if (CopiedObjects.Any(x => x is BaseEvent e && e.IsLaneRotationEvent())) tracksManager.RefreshTracks();
        if (triggersAction) BeatmapActionContainer.AddAction(new SelectionPastedAction(pasted, totalRemoved));
        SelectionPastedEvent?.Invoke(pasted);
        SelectionChangedEvent?.Invoke();
        RefreshSelectionMaterial(false);

        if (eventPlacement.objectContainerCollection.PropagationEditing != EventGridContainer.PropMode.Off)
        {
            eventPlacement.objectContainerCollection.PropagationEditing =
                eventPlacement.objectContainerCollection.PropagationEditing;
        }

        Debug.Log("Pasted!");
    }

    public void MoveSelection(float beats, bool snapObjects = false)
    {
        var allActions = new List<BeatmapAction>();
        foreach (var data in SelectedObjects)
        {
            var collection = BeatmapObjectContainerCollection.GetCollectionForType(data.ObjectType);
            var original = BeatmapFactory.Clone(data);

            collection.LoadedObjects.Remove(data);
            data.Time += beats;
            if (snapObjects)
                data.Time = Mathf.Round(beats / (1f / atsc.GridMeasureSnapping)) * (1f / atsc.GridMeasureSnapping);
            if (data is BaseSlider slider)
            {
                slider.TailTime += beats;
                if (snapObjects)
                    slider.TailTime = Mathf.Round(beats / (1f / atsc.GridMeasureSnapping)) * (1f / atsc.GridMeasureSnapping);
            }
            collection.LoadedObjects.Add(data);

            if (collection.LoadedContainers.TryGetValue(data, out var con)) con.UpdateGridPosition();

            if (collection is NoteGridContainer notesContainer)
            {
                notesContainer.RefreshSpecialAngles(original, false, false);
                notesContainer.RefreshSpecialAngles(data, false, false);
            }

            allActions.Add(new BeatmapObjectModifiedAction(data, data, original, "", true));
        }

        BeatmapActionContainer.AddAction(new ActionCollectionAction(allActions, true, true,
            "Shifted a selection of objects."));
        BeatmapObjectContainerCollection.RefreshAllPools();
    }

    public void ShiftSelection(int leftRight, int upDown)
    {
        var allActions = SelectedObjects.AsParallel().Select(data =>
        {
            var original = BeatmapFactory.Clone(data);
            if (data is BaseNote note)
            {
                if (note.CustomCoordinate is null)
                {
                    if (note.PosX >= 1000)
                    {
                        note.PosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (note.PosX < 1000) note.PosX = 1000;
                    }
                    else if (note.PosX <= -1000)
                    {
                        note.PosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (note.PosX > -1000) note.PosX = -1000;
                    }
                    else
                    {
                        note.PosX += leftRight;
                        if (Settings.Instance.VanillaOnlyShift) note.PosX = Mathf.Clamp(note.PosX, 0, 3);
                    }

                    note.PosY += upDown;
                    if (Settings.Instance.VanillaOnlyShift) note.PosY = Mathf.Clamp(note.PosY, 0, 2);
                }
                else
                {
                    var pos = (Vector2)note.CustomCoordinate;
                    pos[0] += 1f / atsc.GridMeasureSnapping * leftRight; 
                    pos[1] += 1f / atsc.GridMeasureSnapping * upDown;
                }
            }
            else if (data is BaseObstacle obstacle)
            {
                if (!obstacle.IsNoodleExtensions())
                {
                    if (obstacle.PosX >= 1000)
                    {
                        obstacle.PosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (obstacle.PosX < 1000) obstacle.PosX = 1000;
                    }
                    else if (obstacle.PosX <= -1000)
                    {
                        obstacle.PosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (obstacle.PosX > -1000) obstacle.PosX = -1000;
                    }
                    else
                    {
                        obstacle.PosX += leftRight;
                    }
                }
                else
                {
                    if (obstacle.CustomCoordinate != null)
                    {
                        var pos = (Vector2)obstacle.CustomCoordinate;
                        pos[0] += 1f / atsc.GridMeasureSnapping * leftRight;
                        pos[1] += 1f / atsc.GridMeasureSnapping * upDown;
                    }
                }
            }
            else if (data is BaseEvent e)
            {
                var events = eventPlacement.objectContainerCollection;
                if (eventPlacement.objectContainerCollection.PropagationEditing == EventGridContainer.PropMode.Light)
                {
                    var max = events.platformDescriptor.LightingManagers[events.EventTypeToPropagate].ControllingLights
                        .Select(x => x.LightID).Max();

                    var curId = e.CustomLightID != null ? e.CustomLightID[0] : 0;
                    var newId = Math.Min(curId + leftRight, max);
                    if (newId < 1)
                        e.CustomLightID = null;
                    else
                        e.CustomLightID = new [] { newId };
                }
                else if (eventPlacement.objectContainerCollection.PropagationEditing == EventGridContainer.PropMode.Prop)
                {
                    var oldId = (e.CustomLightID != null
                        ? labels.LightIdsToPropId(events.EventTypeToPropagate, e.CustomLightID)
                        : null) ?? -1;
                    var max = events.platformDescriptor.LightingManagers[events.EventTypeToPropagate].LightsGroupedByZ
                        .Length;
                    var newId = Math.Min(oldId + leftRight, max - 1);

                    if (newId < 0)
                    {
                        e.CustomLightID = null;
                    }
                    else
                    {
                        e.CustomLightID = labels.PropIdToLightIds(events.EventTypeToPropagate, newId);
                    }
                }
                else
                {
                    var oldType = e.Type;

                    var modified = labels.EventTypeToLaneId(e.Type);

                    modified += leftRight;

                    if (modified < 0) modified = 0;

                    var laneCount = labels.MaxLaneId();

                    if (modified > laneCount) modified = laneCount;

                    e.Type = labels.LaneIdToEventType(modified);

                    if (e.CustomLightID != null)
                    {
                        var editorID = labels.LightIDToEditor(oldType, e.CustomLightID[0]);
                        e.CustomLightID = new [] { labels.EditorToLightID(e.Type, editorID) };
                    }

                    if (e.CustomLightID is { Length: 0 })
                    {
                        e.CustomLightID = null;
                    }
                }

                if (data.CustomData?.Count <= 0) data.CustomData = null;
            }
            else if (data is BaseSlider slider)
            {
                if (slider.CustomCoordinate is null)
                {
                    if (slider.PosX >= 1000)
                    {
                        slider.PosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (slider.PosX < 1000) slider.PosX = 1000;
                    }
                    else if (slider.PosX <= -1000)
                    {
                        slider.PosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (slider.PosX > -1000) slider.PosX = -1000;
                    }
                    else
                    {
                        slider.PosX += leftRight;
                        if (Settings.Instance.VanillaOnlyShift) slider.PosX = Mathf.Clamp(slider.PosX, 0, 3);
                    }

                    slider.PosY += upDown;
                    if (Settings.Instance.VanillaOnlyShift) slider.PosY = Mathf.Clamp(slider.PosY, 0, 2);
                }
                else
                {
                    var pos = (Vector2)slider.CustomCoordinate;
                    pos[0] += 1f / atsc.GridMeasureSnapping * leftRight; 
                    pos[1] += 1f / atsc.GridMeasureSnapping * upDown;
                }
                
                if (slider.CustomTailCoordinate is null)
                {
                    if (slider.TailPosX >= 1000)
                    {
                        slider.TailPosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (slider.TailPosX < 1000) slider.TailPosX = 1000;
                    }
                    else if (slider.TailPosX <= -1000)
                    {
                        slider.TailPosX += Mathf.RoundToInt(1f / atsc.GridMeasureSnapping * 1000 * leftRight);
                        if (slider.TailPosX > -1000) slider.TailPosX = -1000;
                    }
                    else
                    {
                        slider.TailPosX += leftRight;
                        if (Settings.Instance.VanillaOnlyShift) slider.TailPosX = Mathf.Clamp(slider.TailPosX, 0, 3);
                    }

                    slider.TailPosY += upDown;
                    if (Settings.Instance.VanillaOnlyShift) slider.TailPosY = Mathf.Clamp(slider.TailPosY, 0, 2);
                }
                else
                {
                    var pos = (Vector2)slider.CustomTailCoordinate;
                    pos[0] += 1f / atsc.GridMeasureSnapping * leftRight; 
                    pos[1] += 1f / atsc.GridMeasureSnapping * upDown;
                }
            }
            return new BeatmapObjectModifiedAction(data, data, original, "", true);
        }).ToList();

        BeatmapActionContainer.AddAction(
            new ActionCollectionAction(allActions, true, true, "Shifted a selection of objects."), true);
        tracksManager.RefreshTracks();
    }

    /// <summary>
    ///     Applies objects to the loaded <see cref="BeatSaberMap" />. Should be done before saving the map.
    /// </summary>
    public static void RefreshMap()
    {
        if (BeatSaberSongContainer.Instance.Map != null)
        {
            var newObjects = new Dictionary<ObjectType, IEnumerable<BaseObject>>();
            foreach (int num in Enum.GetValues(typeof(ObjectType)))
            {
                var type = (ObjectType)num;
                var collection = BeatmapObjectContainerCollection.GetCollectionForType(type);
                if (collection is null) continue;
                newObjects.Add(type, collection.GrabSortedObjects());
            }

            if (Settings.Instance.Load_Notes)
            {
                BeatSaberSongContainer.Instance.Map.Notes =
                    newObjects[ObjectType.Note].Cast<BaseNote>().ToList();
            }

            if (Settings.Instance.Load_Obstacles)
            {
                BeatSaberSongContainer.Instance.Map.Obstacles =
                    newObjects[ObjectType.Obstacle].Cast<BaseObstacle>().ToList();
            }

            if (Settings.Instance.Load_Events)
            {
                BeatSaberSongContainer.Instance.Map.Events =
                    newObjects[ObjectType.Event].Cast<BaseEvent>().ToList();
            }

            if (Settings.Instance.Load_Others)
            {
                BeatSaberSongContainer.Instance.Map.BpmChanges =
                    newObjects[ObjectType.BpmChange].Cast<BaseBpmChange>().ToList();
                BeatSaberSongContainer.Instance.Map.CustomEvents = newObjects[ObjectType.CustomEvent]
                    .Cast<BaseCustomEvent>().ToList();
            }

            if (Settings.Instance.Load_MapV3)
            {
                BeatSaberSongContainer.Instance.Map.Arcs =
                    newObjects[ObjectType.Arc].Cast<BaseArc>().ToList();
                BeatSaberSongContainer.Instance.Map.Chains =
                    newObjects[ObjectType.Chain].Cast<BaseChain>().ToList();
            }
        }
    }

    #endregion
}
