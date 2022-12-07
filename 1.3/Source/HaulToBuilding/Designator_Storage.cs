using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulToBuilding
{
    public class Designator_Storage : Designator
    {
        private readonly Bill_Production bill;
        private readonly ExtraBillData extraData;
        private bool dragging;
        private Mode mode;

        private string oldText;
        private Vector2 windowPos;

        public Designator_Storage(Bill_Production bill)
        {
            this.bill = bill;
            icon = TexStorage.StorageSelection;
            extraData = GameComponent_ExtraBillData.Instance.GetData(bill);
            windowPos = HaulToBuildingMod.Settings.Window.Pos;
            useMouseIcon = true;
        }

        public override int DraggableDimensions => 2;

        public override void Deselected()
        {
            base.Deselected();
            Find.Selector.Select(bill?.billStack?.billGiver, true, false);
            InspectPaneUtility.OpenTab(typeof(ITab_Bills));
        }

        private void DoWindow(Event ev)
        {
            Find.WindowStack.ImmediateWindow(2145132, new Rect(windowPos, new Vector2(300f, 480f)), WindowLayer.GameUI, delegate
            {
                var rect = new Rect(0f, 0f, 300f, 480f).ContractedBy(10f);
                var listing = new Listing_Standard();
                listing.Begin(rect);
                var section = listing.BeginSection(Dialog_BillConfig_Patches.TakeFromSubdialogHeight * 3);
                if (section.ButtonText(mode == Mode.TakeFrom ? "HaulToBuilding.Designate.TakeFrom".Translate() : extraData.TakeFromText())) mode = Mode.TakeFrom;

                if (section.ButtonText("HaulToBuilding.Clear"))
                {
                    var oldText = extraData.TakeFromText();
                    extraData.TakeFrom.Clear();
                    GameComponent_ExtraBillData.Instance.SetData(bill, extraData);
                    if (oldText != extraData.TakeFromText()) Messages.Message($"{oldText} -> {extraData.TakeFromText()}", MessageTypeDefOf.TaskCompletion);
                }

                listing.EndSection(section);
                listing.Gap(24);

                section = listing.BeginSection(Dialog_BillConfig_Patches.TakeFromSubdialogHeight * 3);
                if (section.ButtonText(mode == Mode.TakeTo ? "HaulToBuilding.Designate.TakeTo".Translate() : Utils.TakeToText(bill, extraData, out _))) mode = Mode.TakeTo;

                if (section.ButtonText("HaulToBuilding.Clear"))
                {
                    var oldText = Utils.TakeToText(bill, extraData, out _);
                    bill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
                    extraData.Storage = null;
                    GameComponent_ExtraBillData.Instance.SetData(bill, extraData);
                    if (oldText != Utils.TakeToText(bill, extraData, out _))
                        Messages.Message($"{oldText} -> {Utils.TakeToText(bill, extraData, out _)}", MessageTypeDefOf.TaskCompletion);
                }

                listing.EndSection(section);
                listing.Gap(24);

                if (bill.repeatMode == BillRepeatModeDefOf.TargetCount)
                {
                    section = listing.BeginSection(Dialog_BillConfig_Patches.TakeFromSubdialogHeight * 3);
                    if (section.ButtonText(mode == Mode.LookIn ? "HaulToBuilding.Designate.LookIn".Translate() : bill.LookInText(extraData))) mode = Mode.LookIn;

                    if (section.ButtonText("HaulToBuilding.Clear"))
                    {
                        var oldText = bill.LookInText(extraData);
                        bill.includeFromZone = null;
                        extraData.LookInStorage = null;
                        GameComponent_ExtraBillData.Instance.SetData(bill, extraData);
                        if (oldText != bill.LookInText(extraData)) Messages.Message($"{oldText} -> {bill.LookInText(extraData)}", MessageTypeDefOf.TaskCompletion);
                    }

                    listing.EndSection(section);
                }

                listing.End();

                if (ev.isMouse)
                    switch (ev.type)
                    {
                        case EventType.MouseDown:
                            dragging = true;
                            ev.Use();
                            return;
                        case EventType.MouseUp when dragging:
                            dragging = false;
                            HaulToBuildingMod.Settings.Window.Pos.x = windowPos.x;
                            HaulToBuildingMod.Settings.Window.Pos.y = windowPos.y;
                            ev.Use();
                            break;
                        case EventType.MouseDrag when dragging:
                            windowPos += Event.current.delta;
                            ev.Use();
                            break;
                    }
            });
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (c.GetSlotGroup(Map) is {parent: var parent})
            {
                BeginDesignate();
                Designate(parent);
                FinishDesignate();
            }
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            BeginDesignate();
            foreach (var c in cells)
                if (c.GetSlotGroup(Map) is {parent: var parent})
                    Designate(parent);
            FinishDesignate();
        }

        public override void DrawMouseAttachments()
        {
            base.DrawMouseAttachments();
            DoWindow(Event.current);
        }

        public override void RenderHighlight(List<IntVec3> dragCells)
        {
            DesignatorUtility.RenderHighlightOverSelectableThings(this, dragCells);
            DesignatorUtility.RenderHighlightOverSelectableCells(this, dragCells);
        }

        public override void DesignateThing(Thing t)
        {
            if (t is ISlotGroupParent parent)
            {
                BeginDesignate();
                Designate(parent);
                FinishDesignate();
            }
        }

        private void BeginDesignate()
        {
            switch (mode)
            {
                case Mode.TakeFrom:
                    oldText = extraData.TakeFromText();
                    break;
                case Mode.LookIn:
                    oldText = bill.LookInText(extraData);
                    break;
                case Mode.TakeTo:
                    oldText = Utils.TakeToText(bill, extraData, out _);
                    break;
            }
        }

        private void FinishDesignate()
        {
            if (oldText is null) return;
            switch (mode)
            {
                case Mode.TakeFrom:
                    if (oldText != Utils.TakeToText(bill, extraData, out _))
                        Messages.Message($"{oldText} -> {Utils.TakeToText(bill, extraData, out _)}", MessageTypeDefOf.TaskCompletion);
                    break;
                case Mode.LookIn:
                    if (oldText != bill.LookInText(extraData)) Messages.Message($"{oldText} -> {bill.LookInText(extraData)}", MessageTypeDefOf.TaskCompletion);
                    break;
                case Mode.TakeTo:
                    if (oldText != Utils.TakeToText(bill, extraData, out _))
                        Messages.Message($"{oldText} -> {Utils.TakeToText(bill, extraData, out _)}", MessageTypeDefOf.TaskCompletion);
                    break;
            }

            oldText = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Designate(ISlotGroupParent parent)
        {
            switch (mode)
            {
                case Mode.TakeFrom:
                    oldText = extraData.TakeFromText();
                    if (extraData.TakeFrom.Contains(parent)) break;
                    extraData.TakeFrom.Add(parent);
                    GameComponent_ExtraBillData.Instance.SetData(bill, extraData);
                    if (oldText != Utils.TakeToText(bill, extraData, out _))
                        Messages.Message($"{oldText} -> {Utils.TakeToText(bill, extraData, out _)}", MessageTypeDefOf.TaskCompletion);
                    break;
                case Mode.LookIn:
                    oldText = bill.LookInText(extraData);
                    switch (parent)
                    {
                        case Zone_Stockpile stockpile:
                            bill.includeFromZone = stockpile;
                            break;
                        case Building_Storage building:
                            extraData.LookInStorage = building;
                            GameComponent_ExtraBillData.Instance.SetData(bill, extraData);
                            break;
                    }

                    if (oldText != bill.LookInText(extraData)) Messages.Message($"{oldText} -> {bill.LookInText(extraData)}", MessageTypeDefOf.TaskCompletion);

                    break;
                case Mode.TakeTo:
                    oldText = Utils.TakeToText(bill, extraData, out _);
                    switch (parent)
                    {
                        case Zone_Stockpile stockpile:
                            bill.SetStoreMode(BillStoreModeDefOf.SpecificStockpile,
                                stockpile);
                            break;
                        case Building_Storage building:
                            bill.SetStoreMode(HaulToBuildingDefOf.StorageBuilding);
                            extraData.Storage = building;
                            GameComponent_ExtraBillData.Instance.SetData(bill, extraData);
                            break;
                    }

                    if (oldText != Utils.TakeToText(bill, extraData, out _))
                        Messages.Message($"{oldText} -> {Utils.TakeToText(bill, extraData, out _)}", MessageTypeDefOf.TaskCompletion);

                    break;
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => loc.GetSlotGroup(Map)?.parent is { } parent ? CanDesignate(parent) : false;

        public override AcceptanceReport CanDesignateThing(Thing t) => t is ISlotGroupParent parent ? CanDesignate(parent) : false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AcceptanceReport CanDesignate(ISlotGroupParent parent) => mode switch
        {
            Mode.TakeFrom => parent.ValidTakeFrom(bill) ? true : new AcceptanceReport("IncompatibleLower".Translate()),
            Mode.LookIn => parent.ValidLookIn(bill) ? true : new AcceptanceReport("IncompatibleLower".Translate()),
            Mode.TakeTo => parent.ValidTakeTo(bill) ? true : new AcceptanceReport("IncompatibleLower".Translate()),
            _ => false
        };

        private enum Mode
        {
            None,
            TakeFrom,
            LookIn,
            TakeTo
        }
    }
}