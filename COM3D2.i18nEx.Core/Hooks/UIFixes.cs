﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx.Harmony;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace COM3D2.i18nEx.Core.Hooks
{
    internal static class UIFixes
    {
        private static Harmony instance;
        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized)
                return;

            instance = HarmonyWrapper.PatchAll(typeof(UIFixes), "horse.coder.i18nex.ui_fixes");

            initialized = true;
        }

        [HarmonyPatch(typeof(SceneNetorareCheck), "Start")]
        [HarmonyPostfix]
        public static void LocalizeNTRScene(GameObject ___toggleParent)
        {
            Core.Logger.LogInfo("Fixing NTR check scene.");

            void Localize(string item)
            {
                var result = UTY.GetChildObject(___toggleParent, $"{item}/Result"); //.GetComponent<UILabel>();
                var title = UTY.GetChildObject(___toggleParent, $"{item}/Title"); //.GetComponent<UILabel>();

                var resultLoc = result.AddComponent<Localize>();
                resultLoc.SetTerm($"SceneNetorareCheck/{item}_Result");

                var titleLoc = title.AddComponent<Localize>();
                titleLoc.SetTerm($"SceneNetorareCheck/{item}_Title");
            }

            Localize("Toggle_LockUserDraftMaid");
            Localize("Toggle_IsComPlayer");
        }

        private delegate void TranslateInfo(ref string text);

        [HarmonyPatch(typeof(SystemShortcut), "OnClick_Info")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LocalizeInfoText(IEnumerable<CodeInstruction> instructions)
        {
            bool hasText = false;
            foreach (var codeInstruction in instructions)
            {
                if (codeInstruction.opcode == OpCodes.Callvirt && codeInstruction.operand is MethodInfo minfo &&
                    minfo.Name == "get_SysDlg")
                    hasText = true;
                else if (hasText)
                {
                    hasText = false;
                    Core.Logger.LogInfo(
                        $"Got operand: {codeInstruction.opcode} with operand: {codeInstruction.operand}");

                    int index = -1;

                    if (OpCodes.Ldloc_0.Value <= codeInstruction.opcode.Value &&
                        codeInstruction.opcode.Value <= OpCodes.Ldloc_3.Value)
                        index = codeInstruction.opcode.Value - OpCodes.Ldloc_0.Value;
                    else if (codeInstruction.opcode == OpCodes.Ldloc_S || codeInstruction.opcode == OpCodes.Ldloc)
                        index = (int)codeInstruction.operand;

                    if (index < 0)
                    {
                        Core.Logger.LogError($"Failed to patch info text localization! Please report this!");
                        yield return codeInstruction;
                        continue;
                    }

                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldloca, index);
                    yield return HarmonyWrapper.EmitDelegate<TranslateInfo>((ref string text) =>
                    {
                        if (LocalizationManager.TryGetTranslation("System/GameInfo_Description", out string tl))
                            text = string.Format(tl, Product.gameTitle, GameUty.GetBuildVersionText(),
                                                 GameUty.GetGameVersionText(), GameUty.GetLegacyGameVersionText());
                    });
                    yield return new CodeInstruction(OpCodes.Call,
                                                     AccessTools.PropertyGetter(
                                                         typeof(GameMain), nameof(GameMain.Instance)));
                    yield return new CodeInstruction(OpCodes.Callvirt,
                                                     AccessTools.PropertyGetter(
                                                         typeof(GameMain), nameof(GameMain.SysDlg)));
                }

                yield return codeInstruction;
            }
        }
    }
}