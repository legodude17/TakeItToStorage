using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulToBuilding
{
    internal class Toils_Recipe_Patches
    {
        public static void DoPatches(Harmony harm)
        {
            try
            {
                harm.Patch(typeof(Toils_Recipe).GetNestedType("<>c__DisplayClass3_0", BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic | BindingFlags.Static)
                        .GetMethod("<FinishRecipeAndStartStoringProduct>b__0", BindingFlags.Instance |
                                                                               BindingFlags.Public |
                                                                               BindingFlags.NonPublic |
                                                                               BindingFlags.Static),
                    transpiler: new HarmonyMethod(typeof(Toils_Recipe_Patches), "Transpiler"));
            }
            catch (Exception e)
            {
                Log.Error(
                    $"Got error while patching Toils_Recipe.<>c_DisplayClass3_0.<FinishRecipeAndStartStoringProduct>b__0: {e.Message}\n{e.StackTrace}");
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var list = instructions.ToList();
            var info1 = AccessTools.Field(typeof(BillStoreModeDefOf), "SpecificStockpile");
            var idx = list.FindIndex(ins => ins.LoadsField(info1));
            var label1 = (Label) list[idx + 1].operand;
            var idx2 = list.FindIndex(ins => ins.labels.Contains(label1));
            var label2 = (Label) list[idx2 - 1].operand;
            list[idx2].labels.Remove(label1);
            var list2 = new[]
            {
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldloc_S, 6),
                new CodeInstruction(OpCodes.Ldloca_S, 9),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(Toils_Recipe_Patches), "FindCell")),
                new CodeInstruction(OpCodes.Brtrue_S, label2)
            };
            list2[0].labels.Add(label1);
            list.InsertRange(idx2, list2);
            return list;
        }

        public static bool FindCell(Pawn pawn, List<Thing> things, ref IntVec3 cell)
        {
            if (pawn.CurJob.bill.GetStoreMode() == HaulToBuildingDefOf.StorageBuilding)
            {
                StoreUtility.TryFindBestBetterStoreCellForIn(things[0], pawn, pawn.Map, 0, pawn.Faction,
                    GameComponent_ExtraBillData.Instance.GetData(pawn.CurJob.bill).Storage.GetSlotGroup(), out cell);
                return true;
            }

            if (pawn.CurJob.bill.GetStoreMode() == HaulToBuildingDefOf.Nearest)
            {
                var slotGroup = pawn.Map
                    .haulDestinationManager.AllGroupsListForReading.Where(group => !group.parent.Accepts(things[0]))
                    .OrderBy(
                        group => group.CellsList.Any()
                            ? group.CellsList.OrderBy(c => c.DistanceToSquared(pawn.Position)).First()
                                .DistanceToSquared(pawn.Position)
                            : float.MaxValue).FirstOrDefault();
                if (slotGroup != null)
                    StoreUtility.TryFindBestBetterStoreCellForIn(things[0], pawn, pawn.Map, 0, pawn.Faction,
                        slotGroup, out cell);
                return true;
            }

            return false;
        }
    }
}