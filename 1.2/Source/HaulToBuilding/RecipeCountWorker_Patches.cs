using System.Collections.Generic;
using System.Linq;
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
            harm.Patch(AccessTools.Method(typeof(RecipeWorkerCounter), "CountProducts"),
                transpiler: new HarmonyMethod(typeof(RecipeCountWorker_Patches), "Transpiler"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
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
            var label3 = (Label) list[idx3 - 1].operand;
            list.InsertRange(idx2 + 2, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldloca_S, 2),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(RecipeCountWorker_Patches), "GetContentsOfBuilding")),
                new CodeInstruction(OpCodes.Brtrue_S, label3)
            });
            return list;
        }

        public static bool HasBuilding(Bill_Production bill)
        {
            return GameComponent_ExtraBillData.Instance.GetData(bill).LookInStorage != null;
        }

        public static bool GetContentsOfBuilding(Bill_Production bill, ref int num)
        {
            var storage = GameComponent_ExtraBillData.Instance.GetData(bill).LookInStorage;
            if (storage == null) return false;
            num += storage.slotGroup.HeldThings
                .Where(outerThing => bill.recipe.WorkerCounter.CountValidThing(outerThing.GetInnerIfMinified(), bill,
                    bill.recipe.products[0].thingDef)).Sum(outerThing => outerThing.GetInnerIfMinified().stackCount);
            return true;
        }
    }
}