﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using UnityEngine;
using Verse;

namespace CombatExtended
{
    public enum SourceSelection
    {
        Ranged,
        Melee,
        Ammo,
        Minified,
        Generic,
        All // All things, won't include generics, can include minified/able now.
    }

    [StaticConstructorOnStartup]
    public class Dialog_ManageLoadouts : Window
    {
        #region Fields

        private static int[] _dropOptions2 = new int[] { 0, 1 };

        private static Texture2D
        //_arrowBottom = ContentFinder<Texture2D>.Get("UI/Icons/arrowBottom"),
        //_arrowDown = ContentFinder<Texture2D>.Get("UI/Icons/arrowDown"),
        //_arrowTop = ContentFinder<Texture2D>.Get("UI/Icons/arrowTop"),
        //_arrowUp = ContentFinder<Texture2D>.Get("UI/Icons/arrowUp"),
        _darkBackground = SolidColorMaterials.NewSolidColorTexture(0f, 0f, 0f, .2f),
        //_iconEdit = ContentFinder<Texture2D>.Get("UI/Icons/edit"),
        _iconClear = ContentFinder<Texture2D>.Get("UI/Icons/clear"),
        _iconAmmo = ContentFinder<Texture2D>.Get("UI/Icons/ammo"),
        _iconRanged = ContentFinder<Texture2D>.Get("UI/Icons/ranged"),
        _iconMelee = ContentFinder<Texture2D>.Get("UI/Icons/melee"),
        _iconMinified = ContentFinder<Texture2D>.Get("UI/Icons/minified"),
        _iconGeneric = ContentFinder<Texture2D>.Get("UI/Icons/generic"),
        _iconAll = ContentFinder<Texture2D>.Get("UI/Icons/all"),
        _iconAmmoAdd = ContentFinder<Texture2D>.Get("UI/Icons/ammoAdd"),
        _iconEditAttachments = ContentFinder<Texture2D>.Get("UI/Icons/gear"),
        _iconSearch = ContentFinder<Texture2D>.Get("UI/Icons/search"),
        _iconMove = ContentFinder<Texture2D>.Get("UI/Icons/move"),
        _iconPickupDrop = ContentFinder<Texture2D>.Get("UI/Icons/loadoutPickupDrop"),
        _iconDropExcess = ContentFinder<Texture2D>.Get("UI/Icons/loadoutDropExcess");

        //TODO: 1.5
        //private static Regex validNameRegex = Outfit.ValidNameRegex;

        private static Regex validNameRegex = new Regex("^.*$");
        private Vector2 _availableScrollPosition = Vector2.zero;
        private const float _barHeight = 24f;
        private Vector2 _countFieldSize = new Vector2(40f, 24f);
        private Loadout _currentLoadout;
        private LoadoutSlot _draggedSlot;
        private bool _dragging;
        private string _filter = "";
        private const float _iconSize = 16f;
        private const float _margin = 6f;
        private const float _rowHeight = 28f;
        private const float _topAreaHeight = 30f;
        private Vector2 _slotScrollPosition = Vector2.zero;
        private List<SelectableItem> _source;
        private List<LoadoutGenericDef> _sourceGeneric;
        private SourceSelection _sourceType = SourceSelection.Ranged;
        private readonly List<ThingDef> _allSuitableDefs;
        private readonly List<LoadoutGenericDef> _allDefsGeneric;
        private readonly List<SelectableItem> _selectableItems;

        #endregion Fields

        #region Constructors

        public Dialog_ManageLoadouts(Loadout loadout)
        {
            CurrentLoadout = null;
            if (loadout != null && !loadout.defaultLoadout)
            {
                CurrentLoadout = loadout;
            }
            _allSuitableDefs = DefDatabase<ThingDef>.AllDefs.Where(td => !td.IsMenuHidden() && IsSuitableThingDef(td)).ToList();
            _allDefsGeneric = DefDatabase<LoadoutGenericDef>.AllDefs.OrderBy(g => g.label).ToList();
            _selectableItems = new List<SelectableItem>();
            List<ThingDef> suitableMapDefs = Find.CurrentMap.listerThings.AllThings.Where((Thing thing) => !thing.PositionHeld.Fogged(thing.MapHeld) && !thing.GetInnerIfMinified().def.Minifiable).Select((Thing thing) => thing.def).Distinct().Intersect(_allSuitableDefs).ToList();
            foreach (var td in _allSuitableDefs)
            {
                _selectableItems.Add(new SelectableItem()
                {
                    thingDef = td,
                    isGreyedOut = (suitableMapDefs.Find((ThingDef def) => def == td) == null)
                });
            }
            SetSource(SourceSelection.Ranged);
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            //doCloseButton = true; //Close button is awkward
            closeOnClickedOutside = true;
            Utility_Loadouts.UpdateColonistCapacities();
        }

        #endregion Constructors

        #region Properties

        public Loadout CurrentLoadout
        {
            get
            {
                return _currentLoadout;
            }
            set
            {
                if (Compatibility.Multiplayer.InMultiplayer && _currentLoadout != null)
                {
                    SyncedSetName(_currentLoadout, _currentLoadout.label);
                }
                _currentLoadout = value;
            }
        }

        public LoadoutSlot Dragging
        {
            get
            {
                if (_dragging)
                {
                    return _draggedSlot;
                }
                return null;
            }
            set
            {
                if (value == null)
                {
                    _dragging = false;
                }
                else
                {
                    _dragging = true;
                }
                _draggedSlot = value;
            }
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(1000, 700);
            }
        }

        #endregion Properties

        #region Methods

        private bool IsSuitableThingDef(ThingDef td)
        {
            return (td.thingClass != typeof(Corpse) &&
                    !td.IsBlueprint && !td.IsFrame &&
                    td != ThingDefOf.ActiveDropPod &&
                    td.thingClass != typeof(MinifiedThing) &&
                    td.thingClass != typeof(UnfinishedThing) &&
                    !td.destroyOnDrop &&
                    td.category == ThingCategory.Item)
                   ||
                   td.Minifiable;
        }
        public override void DoWindowContents(Rect canvas)
        {
            // fix weird zooming bug
            Text.Font = GameFont.Small;

            const int BUTTON_COUNT = 6;
            const float BUTTON_STRETCH_FACTOR = 0.8f / (float)BUTTON_COUNT;
            float buttonWidth = canvas.width * BUTTON_STRETCH_FACTOR;

            // SET UP RECTS
            // top buttons
            Rect selectRect = new Rect(0f, 0f, buttonWidth, _topAreaHeight);
            Rect newRect = new Rect(selectRect.xMax + _margin, 0f, buttonWidth, _topAreaHeight);
            Rect copyRect = new Rect(newRect.xMax + _margin, 0f, buttonWidth, _topAreaHeight);
            Rect deleteRect = new Rect(copyRect.xMax + _margin, 0f, buttonWidth, _topAreaHeight);
            Rect loadRect = new Rect(deleteRect.xMax + _margin, 0f, buttonWidth, _topAreaHeight);
            Rect saveRect = new Rect(loadRect.xMax + _margin, 0f, buttonWidth, _topAreaHeight);

            // main areas
            Rect nameRect = new Rect(
                0f,
                _topAreaHeight + _margin * 2,
                (canvas.width - _margin) / 2f,
                24f);

            Rect slotListRect = new Rect(
                0f,
                nameRect.yMax + _margin,
                (canvas.width - _margin) / 2f,
                canvas.height - _topAreaHeight - nameRect.height - _barHeight * 2 - _margin * 5);

            Rect weightBarRect = new Rect(slotListRect.xMin, slotListRect.yMax + _margin, slotListRect.width, _barHeight);
            Rect bulkBarRect = new Rect(weightBarRect.xMin, weightBarRect.yMax + _margin, weightBarRect.width, _barHeight);

            Rect sourceButtonRect = new Rect(
                slotListRect.xMax + _margin,
                _topAreaHeight + _margin * 2,
                (canvas.width - _margin) / 2f,
                24f);

            Rect selectionRect = new Rect(
                slotListRect.xMax + _margin,
                sourceButtonRect.yMax + _margin,
                (canvas.width - _margin) / 2f,
                canvas.height - 24f - _topAreaHeight - _margin * 3);

            LoadoutManager.SortLoadouts();
            List<Loadout> loadouts = LoadoutManager.Loadouts.Where(l => !l.defaultLoadout).ToList();

            // DRAW CONTENTS
            // buttons
            // select loadout
            if (Widgets.ButtonText(selectRect, "CE_SelectLoadout".Translate()))
            {

                List<FloatMenuOption> options = new List<FloatMenuOption>();

                if (loadouts.Count == 0)
                {
                    options.Add(new FloatMenuOption("CE_NoLoadouts".Translate(), null));
                }
                else
                {
                    for (int i = 0; i < loadouts.Count; i++)
                    {
                        int local_i = i;
                        options.Add(new FloatMenuOption(loadouts[i].LabelCap, delegate
                        {
                            CurrentLoadout = loadouts[local_i];
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
            // create loadout
            if (Widgets.ButtonText(newRect, "CE_NewLoadout".Translate()))
            {
                CurrentLoadout = NewLoadout();
            }
            // copy loadout
            if (CurrentLoadout != null && Widgets.ButtonText(copyRect, "CE_CopyLoadout".Translate()))
            {
                CurrentLoadout = CopyLoadout(CurrentLoadout);
            }
            // delete loadout
            if (loadouts.Any(l => l.canBeDeleted) && Widgets.ButtonText(deleteRect, "CE_DeleteLoadout".Translate()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();

                for (int i = 0; i < loadouts.Count; i++)
                {
                    int local_i = i;

                    // don't allow deleting the default loadout
                    if (!loadouts[i].canBeDeleted)
                    {
                        continue;
                    }
                    options.Add(new FloatMenuOption(loadouts[i].LabelCap,
                                                    delegate
                    {
                        if (CurrentLoadout == loadouts[local_i])
                        {
                            CurrentLoadout = null;
                        }
                        RemoveLoadout(loadouts[local_i]);
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            // load loadout
            if (Widgets.ButtonText(loadRect, "CE_LoadLoadout".Translate()))
            {
                Find.WindowStack.Add(new LoadLoadoutDialog("loadout", (fileInfo, dialog) =>
                {
                    Log.Message($"Loading loadout from file '{fileInfo.FullName}'...");
                    var mySerializer = new XmlSerializer(typeof(LoadoutConfig));
                    using var myFileStream = new FileStream(fileInfo.FullName, FileMode.Open);
                    LoadoutConfig loadoutConfig = (LoadoutConfig)mySerializer.Deserialize(myFileStream);
                    CurrentLoadout = Loadout.FromConfig(loadoutConfig, out List<string> unloadableDefNames);
                    // Report any LoadoutSlots (i.e. ThingDefs) that could not be loaded.
                    if (unloadableDefNames.Count > 0)
                    {
                        Messages.Message(
                            "CE_MissingLoadoutSlots".Translate(String.Join(", ", unloadableDefNames)),
                            null, MessageTypeDefOf.RejectInput);
                    }
                    AddLoadoutExpose(CurrentLoadout);
                    dialog.Close();
                }));
            }

            // save loadout
            if (CurrentLoadout != null && Widgets.ButtonText(saveRect, "CE_SaveLoadout".Translate()))
            {
                Find.WindowStack.Add(new SaveLoadoutDialog("loadout", (fileInfo, dialog) =>
                {
                    Log.Message($"Saving loadout '{CurrentLoadout.label}' to file '{fileInfo.FullName}'...");
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(LoadoutConfig));
                    using TextWriter writer = new StreamWriter(fileInfo.FullName);
                    xmlSerializer.Serialize(writer, CurrentLoadout.ToConfig());
                    dialog.Close();
                }, CurrentLoadout.label));
            }

            // draw notification if no loadout selected
            if (CurrentLoadout == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.grey;
                Widgets.Label(canvas, "CE_NoLoadoutSelected".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                // and stop further drawing
                return;
            }

            // name
            DrawNameField(nameRect);

            // source selection
            DrawSourceSelection(sourceButtonRect);

            // selection area
            DrawSlotSelection(selectionRect);

            // current slots
            DrawSlotList(slotListRect);

            // bars
            if (CurrentLoadout != null)
            {
                Utility_Loadouts.DrawBar(weightBarRect, CurrentLoadout.Weight, Utility_Loadouts.medianWeightCapacity, "CE_Weight".Translate(), CurrentLoadout.GetWeightTip());
                Utility_Loadouts.DrawBar(bulkBarRect, CurrentLoadout.Bulk, Utility_Loadouts.medianBulkCapacity, "CE_Bulk".Translate(), CurrentLoadout.GetBulkTip());
                // draw text overlays on bars
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                var currentBulk = CE_StatDefOf.CarryBulk.ValueToString(CurrentLoadout.Bulk, CE_StatDefOf.CarryBulk.toStringNumberSense);
                var capacityBulk = CE_StatDefOf.CarryBulk.ValueToString(Utility_Loadouts.medianBulkCapacity, CE_StatDefOf.CarryBulk.toStringNumberSense);
                Widgets.Label(bulkBarRect, currentBulk + "/" + capacityBulk);
                Widgets.Label(weightBarRect, CurrentLoadout.Weight.ToString("0.#") + "/" + Utility_Loadouts.medianWeightCapacity.ToStringMass());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            // done!
        }

        public void DrawSourceSelection(Rect canvas)
        {
            Rect button = new Rect(canvas.xMin, canvas.yMin + (canvas.height - 24f) / 2f, 24f, 24f);

            // Ranged weapons
            GUI.color = _sourceType == SourceSelection.Ranged ? GenUI.MouseoverColor : Color.white;
            if (Widgets.ButtonImage(button, _iconRanged))
            {
                SetSource(SourceSelection.Ranged);
            }
            TooltipHandler.TipRegion(button, "CE_SourceRangedTip".Translate());
            button.x += 24f + _margin;

            // Melee weapons
            GUI.color = _sourceType == SourceSelection.Melee ? GenUI.MouseoverColor : Color.white;
            if (Widgets.ButtonImage(button, _iconMelee))
            {
                SetSource(SourceSelection.Melee);
            }
            TooltipHandler.TipRegion(button, "CE_SourceMeleeTip".Translate());
            button.x += 24f + _margin;

            // Ammo
            GUI.color = _sourceType == SourceSelection.Ammo ? GenUI.MouseoverColor : Color.white;
            if (Widgets.ButtonImage(button, _iconAmmo))
            {
                SetSource(SourceSelection.Ammo);
            }
            TooltipHandler.TipRegion(button, "CE_SourceAmmoTip".Translate());
            button.x += 24f + _margin;

            // Minified
            GUI.color = _sourceType == SourceSelection.Minified ? GenUI.MouseoverColor : Color.white;
            if (Widgets.ButtonImage(button, _iconMinified))
            {
                SetSource(SourceSelection.Minified);
            }
            TooltipHandler.TipRegion(button, "CE_SourceMinifiedTip".Translate());
            button.x += 24f + _margin;

            // Generic
            GUI.color = _sourceType == SourceSelection.Generic ? GenUI.MouseoverColor : Color.white;
            if (Widgets.ButtonImage(button, _iconGeneric))
            {
                SetSource(SourceSelection.Generic);
            }
            TooltipHandler.TipRegion(button, "CE_SourceGenericTip".Translate());
            button.x += 24f + _margin;

            // All
            GUI.color = _sourceType == SourceSelection.All ? GenUI.MouseoverColor : Color.white;
            if (Widgets.ButtonImage(button, _iconAll))
            {
                SetSource(SourceSelection.All);
            }
            TooltipHandler.TipRegion(button, "CE_SourceAllTip".Translate());

            // filter input field
            Rect filter = new Rect(canvas.xMax - 75f, canvas.yMin + (canvas.height - 24f) / 2f, 75f, 24f);
            DrawFilterField(filter);
            TooltipHandler.TipRegion(filter, "CE_SourceFilterTip".Translate());

            // search icon
            button.x = filter.xMin - _margin * 2 - _iconSize;
            GUI.DrawTexture(button, _iconSearch);
            TooltipHandler.TipRegion(button, "CE_SourceFilterTip".Translate());

            // reset color
            GUI.color = Color.white;
        }

        public void FilterSource(string filter)
        {
            // reset source
            SetSource(_sourceType, true);

            // filter
            _source = _source.Where(td => td.thingDef.label.ToUpperInvariant().Contains(_filter.ToUpperInvariant())).ToList();
        }

        public void SetSource(SourceSelection source, bool preserveFilter = false)
        {
            _sourceGeneric = _allDefsGeneric;
            if (!preserveFilter)
            {
                _filter = "";
            }

            switch (source)
            {
                case SourceSelection.Ranged:
                    _source = _selectableItems.Where(row => row.thingDef.IsRangedWeapon).ToList();
                    _sourceType = SourceSelection.Ranged;
                    break;

                case SourceSelection.Melee:
                    _source = _selectableItems.Where(row => row.thingDef.IsMeleeWeapon).ToList();
                    _sourceType = SourceSelection.Melee;
                    break;

                case SourceSelection.Ammo:
                    _source = _selectableItems.Where(row => row.thingDef is AmmoDef).ToList();
                    _sourceType = SourceSelection.Ammo;
                    break;

                case SourceSelection.Minified:
                    _source = _selectableItems.Where(row => row.thingDef.Minifiable).ToList();
                    _sourceType = SourceSelection.Minified;
                    break;

                case SourceSelection.Generic:
                    _sourceType = SourceSelection.Generic;
                    initGenericVisibilityDictionary();
                    break;

                case SourceSelection.All:
                default:
                    _source = _selectableItems;
                    _sourceType = SourceSelection.All;
                    break;
            }

            if (!_source.NullOrEmpty())
            {
                _source = _source.OrderBy(td => td.thingDef.label).ToList();
            }
        }

        private void DrawCountField(Rect canvas, LoadoutSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            int countInt = slot.count;
            string buffer = countInt.ToString();
            Widgets.TextFieldNumeric<int>(canvas, ref countInt, ref buffer);
            TooltipHandler.TipRegion(canvas, "CE_CountFieldTip".Translate(slot.count));
            if (slot.count != countInt)
            {
                if (Compatibility.Multiplayer.InMultiplayer)
                {
                    SetSlotCount(CurrentLoadout, CurrentLoadout.Slots.IndexOf(slot), countInt);
                }
                else
                {
                    slot.count = countInt;
                }
            }
        }

        private void DrawFilterField(Rect canvas)
        {
            string filter = GUI.TextField(canvas, _filter);
            if (filter != _filter)
            {
                _filter = filter;
                FilterSource(_filter);
            }
        }

        private void DrawNameField(Rect canvas)
        {
            string label = GUI.TextField(canvas, CurrentLoadout.label);
            if (validNameRegex.IsMatch(label))
            {
                CurrentLoadout.label = label;
            }
        }

        private void DrawSlot(Rect row, LoadoutSlot slot, bool slotDraggable = true)
        {
            // set up rects
            // dragging handle (square) | label (fill) | count (50px) | delete (iconSize)
            Rect draggingHandle = new Rect(row);
            draggingHandle.width = row.height;

            Rect labelRect = new Rect(row);
            if (slotDraggable)
            {
                labelRect.xMin = draggingHandle.xMax;
            }
            labelRect.xMax = row.xMax - _countFieldSize.x - _iconSize - 2 * _margin;

            Rect countRect = new Rect(
                row.xMax - _countFieldSize.x - _iconSize - 2 * _margin,
                row.yMin + (row.height - _countFieldSize.y) / 2f,
                _countFieldSize.x,
                _countFieldSize.y);

            Rect ammoRect = new Rect(
                countRect.xMin - _iconSize - _margin,
                row.yMin + (row.height - _iconSize) / 2f,
                _iconSize, _iconSize);

            Rect countModeRect = new Rect(
                ammoRect.xMin - _iconSize - _margin,
                row.yMin + (row.height - _iconSize) / 2f,
                _iconSize, _iconSize);

            Rect editAttachmentsRect = new Rect(
                countModeRect.xMin - _iconSize - _margin,
                row.yMin + (row.height - _iconSize) / 2f,
                _iconSize, _iconSize);

            Rect deleteRect = new Rect(countRect.xMax + _margin, row.yMin + (row.height - _iconSize) / 2f, _iconSize, _iconSize);

            // prepare attachment config
            if (slot.isWeaponPlatform && Widgets.ButtonImage(editAttachmentsRect, _iconEditAttachments))
            {
                RocketGUI.GUIUtility.DropDownMenu<int>((i) =>
                {
                    if (i == 0)
                    {
                        return "CE_AttachmentsEditLoadout".Translate();
                    }
                    if (i == 1)
                    {
                        return "CE_AttachmentsClearLoadout".Translate();
                    }
                    throw new NotImplementedException();
                },
                (i) =>
                {
                    if (i == 0)
                    {
                        if (Find.WindowStack.IsOpen<Window_AttachmentsEditor>())
                        {
                            Find.WindowStack.TryRemove(typeof(Window_AttachmentsEditor), true);
                        }
                        else
                        {
                            Find.WindowStack.Add(new Window_AttachmentsEditor(slot.weaponPlatformDef, slot.attachmentLinks, (links) =>
                            {
                                if (links != null)
                                {
                                    slot.attachments.Clear();
                                    slot.attachments.AddRange(links.Select(l => l.attachment));
                                }
                            }));
                        }
                    }
                    if (i == 1)
                    {
                        slot.attachments.Clear();
                    }
                }, _dropOptions2);
            }

            // dragging on dragHandle
            if (slotDraggable)
            {
                TooltipHandler.TipRegion(draggingHandle, "CE_DragToReorder".Translate());
                GUI.DrawTexture(draggingHandle, _iconMove);

                if (Mouse.IsOver(draggingHandle) && Input.GetMouseButtonDown(0))
                {
                    Dragging = slot;
                }
            }

            // interactions (main row rect)
            if (!Mouse.IsOver(deleteRect))
            {
                Widgets.DrawHighlightIfMouseover(row);
                TooltipHandler.TipRegion(row, slot.genericDef != null ? slot.genericDef.GetWeightAndBulkTip(slot.count) : slot.thingDef.GetWeightAndBulkTip(slot.count));
            }

            // label
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(labelRect, slot.LabelCap);
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;

            // easy ammo adder, ranged weapons only
            if (slot.thingDef != null && slot.thingDef.IsRangedWeapon)
            {
                // make sure there's an ammoset defined
                AmmoSetDef ammoSet = ((slot.thingDef.GetCompProperties<CompProperties_AmmoUser>() == null) ? null : slot.thingDef.GetCompProperties<CompProperties_AmmoUser>().ammoSet);

                bool? temp = !((((ammoSet == null) ? null : ammoSet.ammoTypes)).NullOrEmpty());

                if (temp ?? false)
                {
                    if (Widgets.ButtonImage(ammoRect, _iconAmmoAdd))
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        int magazineSize = (slot.thingDef.GetCompProperties<CompProperties_AmmoUser>() == null) ? 0 : slot.thingDef.GetCompProperties<CompProperties_AmmoUser>().magazineSize;

                        foreach (AmmoLink link in ((ammoSet == null) ? null : ammoSet.ammoTypes))
                        {
                            options.Add(new FloatMenuOption(link.ammo.LabelCap, delegate
                            {
                                AddLoadoutSlotSpecific(CurrentLoadout, link.ammo, (magazineSize <= 1 ? link.ammo.defaultAmmoCount : magazineSize));
                            }));
                        }
                        // Add in the generic for this gun.
                        LoadoutGenericDef generic = DefDatabase<LoadoutGenericDef>.GetNamed("GenericAmmo-" + slot.thingDef.defName);
                        if (generic != null)
                        {
                            options.Add(new FloatMenuOption(generic.LabelCap, delegate
                        {
                            AddLoadoutSlotGeneric(CurrentLoadout, generic);
                        }));
                        }

                        Find.WindowStack.Add(new FloatMenu(options, "CE_AddAmmoFor".Translate(slot.thingDef.LabelCap)));
                    }
                }
            }

            // count
            DrawCountField(countRect, slot);

            // toggle count mode
            if (slot.genericDef != null)
            {
                Texture2D curModeIcon = slot.countType == LoadoutCountType.dropExcess ? _iconDropExcess : _iconPickupDrop;
                string tipString = slot.countType == LoadoutCountType.dropExcess ? "CE_DropExcess".Translate() : "CE_PickupMissingAndDropExcess".Translate();
                if (Widgets.ButtonImage(countModeRect, curModeIcon))
                {
                    if (Compatibility.Multiplayer.InMultiplayer)
                    {
                        ChangeCountType(CurrentLoadout, CurrentLoadout.Slots.IndexOf(slot));
                    }
                    else
                    {
                        slot.countType = slot.countType == LoadoutCountType.dropExcess ? LoadoutCountType.pickupDrop : LoadoutCountType.dropExcess;
                    }
                }
                TooltipHandler.TipRegion(countModeRect, tipString);
            }

            // delete
            if (Mouse.IsOver(deleteRect))
            {
                GUI.DrawTexture(row, TexUI.HighlightTex);
            }
            if (Widgets.ButtonImage(deleteRect, _iconClear))
            {
                RemoveSlot(CurrentLoadout, CurrentLoadout.Slots.IndexOf(slot));
            }
            TooltipHandler.TipRegion(deleteRect, "CE_DeleteFilter".Translate());
        }

        //TODO: Research if the UI list of items can be cached (That's the Unity UI list)...
        private void DrawSlotList(Rect canvas)
        {
            // set up content canvas
            Rect viewRect = new Rect(0f, 0f, canvas.width, _rowHeight * CurrentLoadout.SlotCount + 1);

            // create some extra height if we're dragging
            if (Dragging != null)
            {
                viewRect.height += _rowHeight;
            }

            // leave room for scrollbar if necessary
            if (viewRect.height > canvas.height)
            {
                viewRect.width -= 16f;
            }

            // darken whole area
            GUI.DrawTexture(canvas, _darkBackground);

            Widgets.BeginScrollView(canvas, ref _slotScrollPosition, viewRect);
            int i = 0;
            float curY = 0f;
            for (; i < CurrentLoadout.SlotCount; i++)
            {
                // create row rect
                Rect row = new Rect(0f, curY, viewRect.width, _rowHeight);
                curY += _rowHeight;

                // if we're dragging, and currently on this row, and this row is not the row being dragged - draw a ghost of the slot here
                if (Dragging != null && Mouse.IsOver(row) && Dragging != CurrentLoadout.Slots[i])
                {
                    // draw ghost
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    DrawSlot(row, Dragging);
                    GUI.color = Color.white;

                    // catch mouseUp
                    if (Input.GetMouseButtonUp(0))
                    {
                        if (Compatibility.Multiplayer.InMultiplayer)
                        {
                            MoveSlot(CurrentLoadout, CurrentLoadout.Slots.IndexOf(Dragging), i);
                        }
                        else
                        {
                            CurrentLoadout.MoveSlot(Dragging, i);
                        }
                        Dragging = null;
                    }

                    // ofset further slots down
                    row.y += _rowHeight;
                    curY += _rowHeight;
                }

                // alternate row background
                if (i % 2 == 0)
                {
                    GUI.DrawTexture(row, _darkBackground);
                }

                // draw the slot - grey out if draggin this, but only when dragged over somewhere else
                if (Dragging == CurrentLoadout.Slots[i] && !Mouse.IsOver(row))
                {
                    GUI.color = new Color(.6f, .6f, .6f, .4f);
                }
                DrawSlot(row, CurrentLoadout.Slots[i], CurrentLoadout.SlotCount > 1);
                GUI.color = Color.white;
            }

            // if we're dragging, create an extra invisible row to allow moving stuff to the bottom
            if (Dragging != null)
            {
                Rect row = new Rect(0f, curY, viewRect.width, _rowHeight);

                if (Mouse.IsOver(row))
                {
                    // draw ghost
                    GUI.color = new Color(.7f, .7f, .7f, .5f);
                    DrawSlot(row, Dragging);
                    GUI.color = Color.white;

                    // catch mouseUp
                    if (Input.GetMouseButtonUp(0))
                    {
                        if (Compatibility.Multiplayer.InMultiplayer)
                        {
                            MoveSlot(CurrentLoadout, CurrentLoadout.Slots.IndexOf(Dragging), CurrentLoadout.Slots.Count - 1);
                        }
                        else
                        {
                            CurrentLoadout.MoveSlot(Dragging, CurrentLoadout.Slots.Count - 1);
                        }
                        Dragging = null;
                    }
                }
            }

            // cancel drag when mouse leaves the area, or on mouseup.
            if (!Mouse.IsOver(viewRect) || Input.GetMouseButtonUp(0))
            {
                Dragging = null;
            }

            Widgets.EndScrollView();
        }

        private void DrawSlotSelection(Rect canvas)
        {
            int count = _sourceType == SourceSelection.Generic ? _sourceGeneric.Count : _source.Count;
            GUI.DrawTexture(canvas, _darkBackground);

            if ((_sourceType != SourceSelection.Generic && _source.NullOrEmpty()) || (_sourceType == SourceSelection.Generic && _sourceGeneric.NullOrEmpty()))
            {
                return;
            }

            Rect viewRect = new Rect(canvas);
            viewRect.width -= 16f;
            viewRect.height = count * _rowHeight;

            Widgets.BeginScrollView(canvas, ref _availableScrollPosition, viewRect.AtZero());
            int startRow = (int)Math.Floor((decimal)(_availableScrollPosition.y / _rowHeight));
            startRow = (startRow < 0) ? 0 : startRow;
            int endRow = startRow + (int)(Math.Ceiling((decimal)(canvas.height / _rowHeight)));
            endRow = (endRow > count) ? count : endRow;
            for (int i = startRow; i < endRow; i++)
            {
                // gray out weapons not in stock
                Color baseColor = GUI.color;
                if (_sourceType == SourceSelection.Generic)
                {
                    if (GetVisibleGeneric(_sourceGeneric[i]))
                    {
                        GUI.color = Color.gray;
                    }
                }
                else
                {
                    if (_source[i].isGreyedOut)
                    {
                        GUI.color = Color.gray;
                    }
                }

                Rect row = new Rect(0f, i * _rowHeight, canvas.width, _rowHeight);
                Rect labelRect = new Rect(row);
                if (_sourceType == SourceSelection.Generic)
                {
                    TooltipHandler.TipRegion(row, _sourceGeneric[i].GetWeightAndBulkTip());
                }
                else
                {
                    TooltipHandler.TipRegion(row, _source[i].thingDef.GetWeightAndBulkTip());
                }

                labelRect.xMin += _margin;
                if (i % 2 == 0)
                {
                    GUI.DrawTexture(row, _darkBackground);
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                if (_sourceType == SourceSelection.Generic)
                {
                    Widgets.Label(labelRect, _sourceGeneric[i].LabelCap);
                }
                else
                {
                    Widgets.Label(labelRect, _source[i].thingDef.LabelCap);
                }
                Text.WordWrap = true;
                Text.Anchor = TextAnchor.UpperLeft;

                Widgets.DrawHighlightIfMouseover(row);
                if (Widgets.ButtonInvisible(row))
                {
                    if (_sourceType == SourceSelection.Generic)
                    {
                        AddLoadoutSlotGeneric(CurrentLoadout, _sourceGeneric[i]);
                    }
                    else
                    {
                        AddLoadoutSlotSpecific(CurrentLoadout, _source[i].thingDef);
                    }
                }
                // revert to original color
                GUI.color = baseColor;
            }
            Widgets.EndScrollView();
        }

        public override void Close(bool doCloseSound = true)
        {
            base.Close(doCloseSound);

            if (Compatibility.Multiplayer.InMultiplayer && CurrentLoadout != null)
            {
                SyncedSetName(CurrentLoadout, CurrentLoadout.label);
            }
        }

        [Compatibility.Multiplayer.SyncMethod]
        private static void SyncedSetName(Loadout loadout, string name) => loadout.label = name;

        [Compatibility.Multiplayer.SyncMethod]
        private static void AddLoadoutSlotGeneric(Loadout loadout, LoadoutGenericDef generic) => loadout.AddSlot(new LoadoutSlot(generic));

        [Compatibility.Multiplayer.SyncMethod]
        private static void AddLoadoutSlotSpecific(Loadout loadout, ThingDef def, int count = 1)
        => loadout.AddSlot(new LoadoutSlot(def, count));

        // We prefer syncing loadout and slot index, as it's faster than iterating over all of the loadouts first to find the current one.
        // We don't have direct reference to Loadout from LoadoutSlot, so we can't really speed up the SyncWorker for it.
        [Compatibility.Multiplayer.SyncMethod]
        private static void RemoveSlot(Loadout loadout, int index)
        {
            if (index >= 0)
            {
                loadout.RemoveSlot(index);
            }
        }

        [Compatibility.Multiplayer.SyncMethod]
        private static void SetSlotCount(Loadout loadout, int index, int count)
        {
            if (index >= 0)
            {
                loadout.Slots[index].count = count;
            }
        }

        [Compatibility.Multiplayer.SyncMethod]
        private static Loadout NewLoadout()
        {
            Loadout loadout = new Loadout();
            loadout.AddBasicSlots();
            LoadoutManager.AddLoadout(loadout);

            // In synced methods, it always returns the default value (as it can't run the method at the time)
            // so we switch the loadout for the user who added a new one
            if (Compatibility.Multiplayer.IsExecutingCommandsIssuedBySelf)
            {
                Find.WindowStack.WindowOfType<Dialog_ManageLoadouts>().CurrentLoadout = loadout;
            }
            return loadout;
        }

        [Compatibility.Multiplayer.SyncMethod]
        private static Loadout CopyLoadout(Loadout loadout)
        {
            var copy = loadout.Copy();
            LoadoutManager.AddLoadout(copy);

            if (Compatibility.Multiplayer.IsExecutingCommandsIssuedBySelf)
            {
                Find.WindowStack.WindowOfType<Dialog_ManageLoadouts>().CurrentLoadout = copy;
            }
            return copy;
        }

        [Compatibility.Multiplayer.SyncMethod]
        private static void RemoveLoadout(Loadout loadout)
        {
            if (Compatibility.Multiplayer.InMultiplayer)
            {
                Find.WindowStack.WindowOfType<Dialog_ManageLoadouts>().CurrentLoadout = null;
            }
            LoadoutManager.RemoveLoadout(loadout);
        }

        /// <summary>
        /// Sync the <see cref="Loadout"/> by using <see cref="IExposable"/> interface
        /// We need to use it in places where we haven't called <see cref="LoadoutManager.AddLoadout(Loadout)"/> yet on that specific loadout.
        /// </summary>
        /// <param name="loadout">A specific <see cref="Loadout"/> which we want to sync</param>
        [Compatibility.Multiplayer.SyncMethod(exposeParameters = new[] { 0 })]
        private static void AddLoadoutExpose(Loadout loadout) => LoadoutManager.AddLoadout(loadout);

        [Compatibility.Multiplayer.SyncMethod]
        private static void MoveSlot(Loadout loadout, int index, int moveIndex)
        {
            if (index >= 0)
            {
                var slot = loadout.Slots[index];
                loadout.MoveSlot(slot, moveIndex);
            }
        }

        [Compatibility.Multiplayer.SyncMethod]
        private static void ChangeCountType(Loadout loadout, int index)
        {
            if (index >= 0)
            {
                var slot = loadout.Slots[index];
                slot.countType = slot.countType == LoadoutCountType.dropExcess ? LoadoutCountType.pickupDrop : LoadoutCountType.dropExcess;
            }
        }

        #endregion Methods

        #region ListDrawOptimization

        /* This region is used by DrawSlotSelection and setup by SetSource when Generics type is chosen.
         * Purpose is to spread the load out over time, instead of checking the state of every generic per frame, only one generic is tested in a given frame.
         * and then some time (frames) are allowed to pass before the next check.
         *
         * The reason is that checking the existence of a generic requires that we consider all possible things.
         * A well written generic won't be too hard to test on a given frame.
         */

        static readonly Dictionary<LoadoutGenericDef, VisibilityCache> genericVisibility = new Dictionary<LoadoutGenericDef, VisibilityCache>();
        const int advanceTicks = 1; //  GenTicks.TicksPerRealSecond / 4;

        /// <summary>
        /// Purpose is to handle deciding if a generic's state (something on the map or not) should be checked or not based on current frame.
        /// </summary>
        /// <param name="def"></param>
        /// <returns></returns>
        private bool GetVisibleGeneric(LoadoutGenericDef def)
        {
            if (GenTicks.TicksAbs >= genericVisibility[def].ticksToRecheck)
            {
                genericVisibility[def].ticksToRecheck = GenTicks.TicksAbs + (advanceTicks * genericVisibility[def].position);
                genericVisibility[def].check = Find.CurrentMap.listerThings.AllThings.Find(x => def.lambda(x.GetInnerIfMinified().def) && !x.def.Minifiable) == null;
            }

            return genericVisibility[def].check;
        }

        private void initGenericVisibilityDictionary()
        {
            int tick = GenTicks.TicksAbs;
            int position = 1;
            foreach (LoadoutGenericDef def in _sourceGeneric)
            {
                if (!genericVisibility.ContainsKey(def))
                {
                    genericVisibility.Add(def, new VisibilityCache());
                }
                genericVisibility[def].ticksToRecheck = tick;
                genericVisibility[def].check = Find.CurrentMap.listerThings.AllThings.Find(x => def.lambda(x.GetInnerIfMinified().def) && !x.def.Minifiable) == null;
                genericVisibility[def].position = position;
                position++;
                tick += advanceTicks;
            }
        }

        private class VisibilityCache
        {
            public int ticksToRecheck = 0;
            public bool check = true;
            public int position = 0;
        }

        #endregion GenericDrawOptimization

        private class SelectableItem
        {
            public ThingDef thingDef;
            public bool isGreyedOut;
        }
    }
}
