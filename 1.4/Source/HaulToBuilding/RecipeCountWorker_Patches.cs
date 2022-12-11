using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HaulToBuilding
{
    public class RecipeCountWorker_Patches
    {
        public static void DoPatches(Harmony harm)
        {
            try
            {
                harm.Patch(AccessTools.Method(typeof(RecipeWorkerCounter), "CountProducts"),
                    transpiler: new HarmonyMethod(typeof(RecipeCountWorker_Patches), "TranspilerCore"));
            }
            catch (Exception e)
            {
                Log.Error($"Got error while patching RecipeCountWorker.CountProducts: {e.Message}\n{e.StackTrace}");
            }
            MethodInfo iwMethod = AccessTools.Method(
                "ImprovedWorkbenches.RecipeWorkerCounter_CountProducts_Detour:CountAdditionalProducts");
            if (iwMethod != null)
            {
                try
                {
                    harm.Patch(iwMethod,
                        transpiler: new HarmonyMethod(typeof(RecipeCountWorker_Patches), "TranspilerIW"));
                }
                catch (Exception e)
                {
                    Log.Error($"Got error while patching ImprovedWorkbenches.RecipeWorkerCounter_CountProducts_Detour."
                        + $"CountAdditionalProducts: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        public static IEnumerable<CodeInstruction> TranspilerCore(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            return Transpiler(instructions, generator, true);
        }
        public static IEnumerable<CodeInstruction> TranspilerIW(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            return Transpiler(instructions, generator, false);
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, bool isCore)
        {
            var list = instructions.ToList();
            var info1 = AccessTools.Field(typeof(Bill_Production), "includeFromZone");
            var idx1 = list.FindIndex(ins => ins.LoadsField(info1));
            var label1 = (Label) list[idx1 + 1].operand;
            list.InsertRange(idx1 + 2, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RecipeCountWorker_Patches), "HasBuilding")),
                new CodeInstruction(OpCodes.Brtrue, label1)
            });
            var idx2 = list.FindIndex(idx1 + 1, ins => ins.LoadsField(info1));
            var label2 = (Label) list[idx2 + 1].operand;
            var idx3 = list.FindIndex(ins => ins.labels.Contains(label2));
            CodeInstruction ins = list[idx3 - (isCore ? 1 : 5)];
            if(ins.opcode != (isCore ? OpCodes.Br_S : OpCodes.Leave_S))
                throw new Exception("Unexpected instruction when searching for label");
            var label3 = (Label) ins.operand;
            list.InsertRange(idx2 + 2, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                isCore ? new CodeInstruction(OpCodes.Ldloc_1) : new CodeInstruction(OpCodes.Ldloc_S, 5),
                new CodeInstruction(OpCodes.Ldloca_S, isCore ? 2 : 3),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(RecipeCountWorker_Patches), "GetContentsOfBuilding")),
                new CodeInstruction(OpCodes.Brtrue, label3)
            });
            return list;
        }

        public static bool HasBuilding(Bill_Production bill)
        {
            return GameComponent_ExtraBillData.Instance.GetData(bill).LookInStorage != null;
        }

        public static bool GetContentsOfBuilding(RecipeWorkerCounter counter, Bill_Production bill, ThingDef def, ref int num)
        {
            var storage = GameComponent_ExtraBillData.Instance.GetData(bill).LookInStorage;
            if (storage == null) return false;
            num += storage.slotGroup.HeldThings
                .Where(outerThing => counter.CountValidThing(outerThing.GetInnerIfMinified(), bill,
                    def)).Sum(outerThing => outerThing.GetInnerIfMinified().stackCount);
            return true;
        }
    }
}