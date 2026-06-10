// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace NPCStyleLimiter
{
    public class Dialog_ManageConfigs : Window
    {
        public override Vector2 InitialSize => new Vector2(560f, 580f);

        private Vector2 scrollPosition = Vector2.zero;
        private string saveAsName = "";
        private string renameBuffer = "";
        private string renameTarget = null;
        private bool focusSaveField = false;

        // Modern UI Theme Colors
        private static readonly Color AccentColor = new Color(0.78f, 0.55f, 0.15f);
        private static readonly Color PanelBgColor = new Color(1f, 1f, 1f, 0.03f);
        private static readonly Color HoverRowColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color InactiveTextColor = new Color(0.55f, 0.6f, 0.62f);
        private static readonly Color CardBgColor = new Color(0f, 0f, 0f, 0.15f);

        public Dialog_ManageConfigs(bool focusSave = false)
        {
            this.focusSaveField = focusSave;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Widgets.Label(titleRect, "NPCStyleLimiter_ProfileManager".Translate());
            Text.Font = GameFont.Small;

            float curY = titleRect.yMax + 10f;

            // 1. Current Status Card
            Rect statusCard = new Rect(inRect.x, curY, inRect.width, 45f);
            Widgets.DrawRectFast(statusCard, CardBgColor);

            string statusLabel = "NPCStyleLimiter_CurrentProfileLabel".Translate();
            string activeName = CustomizerMod.Settings.currentProfileName ?? "Default";
            float sLabelWidth = Text.CalcSize(statusLabel).x;

            Rect statusContentRect = new Rect(statusCard.x + 12f, statusCard.y, statusCard.width - 24f, statusCard.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = InactiveTextColor;
            Widgets.Label(new Rect(statusContentRect.x, statusContentRect.y, sLabelWidth, statusContentRect.height), statusLabel);
            GUI.color = AccentColor;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(statusContentRect.x + sLabelWidth + 8f, statusContentRect.y, statusContentRect.width - sLabelWidth - 8f, statusContentRect.height), activeName);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            curY = statusCard.yMax + 15f;

            // 2. Save New Profile Card
            Rect saveCard = new Rect(inRect.x, curY, inRect.width, 80f);
            Widgets.DrawRectFast(saveCard, CardBgColor);
            
            Rect saveContentRect = saveCard.ContractedBy(12f);
            Widgets.Label(new Rect(saveContentRect.x, saveContentRect.y, 200f, 24f), "NPCStyleLimiter_SaveAs".Translate());
            
            Rect inputRect = new Rect(saveContentRect.x, saveContentRect.y + 28f, saveContentRect.width - 100f, 30f);
            GUI.SetNextControlName("SaveNameField");
            saveAsName = Widgets.TextField(inputRect, saveAsName);
            if (focusSaveField)
            {
                GUI.FocusControl("SaveNameField");
                focusSaveField = false;
            }

            Rect saveBtnRect = new Rect(inputRect.xMax + 8f, inputRect.y, 92f, 30f);
            bool canSave = !string.IsNullOrEmpty(saveAsName.Trim()) && !saveAsName.Trim().Equals("Default", StringComparison.OrdinalIgnoreCase);
            
            if (!canSave) GUI.color = new Color(1f, 1f, 1f, 0.3f);
            if (Widgets.ButtonText(saveBtnRect, "NPCStyleLimiter_Save".Translate()) && canSave)
            {
                DoSave(saveAsName.Trim());
            }
            GUI.color = Color.white;

            curY = saveCard.yMax + 15f;

            // 3. Profile List Section
            List<string> profiles = CustomizerMod.Settings.ListProfiles();
            if (!profiles.Any(p => p.Equals("Default", StringComparison.OrdinalIgnoreCase))) profiles.Insert(0, "Default");

            float listHeaderY = curY;
            Widgets.Label(new Rect(inRect.x + 5f, listHeaderY, 200f, 24f), "NPCStyleLimiter_StoredProfiles".Translate());
            curY += 28f;

            Rect listOuterRect = new Rect(inRect.x, curY, inRect.width, inRect.yMax - curY - 10f);
            Widgets.DrawRectFast(listOuterRect, new Color(0f, 0f, 0f, 0.1f));

            float rowHeight = 42f;
            float viewHeight = Math.Max(listOuterRect.height, profiles.Count * rowHeight);
            Rect scrollInnerRect = new Rect(0f, 0f, listOuterRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(listOuterRect, ref scrollPosition, scrollInnerRect);
            
            for (int i = 0; i < profiles.Count; i++)
            {
                string pName = profiles[i];
                Rect rowRect = new Rect(0f, i * rowHeight, scrollInnerRect.width, rowHeight);
                bool isDefault = pName.Equals("Default", StringComparison.OrdinalIgnoreCase);
                bool isActive = pName.Equals(CustomizerMod.Settings.currentProfileName, StringComparison.OrdinalIgnoreCase);
                bool isRenaming = renameTarget == pName;

                // Row highlights
                if (isActive) Widgets.DrawRectFast(rowRect, new Color(0.78f, 0.55f, 0.15f, 0.08f));
                else if (Mouse.IsOver(rowRect)) Widgets.DrawRectFast(rowRect, HoverRowColor);
                if (i % 2 == 1) Widgets.DrawLightHighlight(rowRect);

                // Entry content
                Rect entryRect = rowRect.ContractedBy(4f);
                if (isRenaming)
                {
                    DrawRenameField(entryRect, pName);
                }
                else
                {
                    DrawProfileRow(entryRect, pName, isActive, isDefault);
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawProfileRow(Rect rect, string name, bool isActive, bool isDefault)
        {
            float curX = rect.x + 8f;
            
            // Icon/Status
            if (isActive)
            {
                GUI.color = AccentColor;
                Widgets.Label(new Rect(curX, rect.y + 6f, 20f, 24f), "\u25CF");
                GUI.color = Color.white;
            }
            curX += 20f;

            // Name
            Text.Anchor = TextAnchor.MiddleLeft;
            string display = name;
            if (isDefault)
            {
                GUI.color = InactiveTextColor;
                display += " (" + "NPCStyleLimiter_ReadOnly".Translate() + ")";
            }
            else if (isActive)
            {
                GUI.color = AccentColor;
            }
            Widgets.Label(new Rect(curX, rect.y, rect.width * 0.45f, rect.height), display);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Buttons
            float btnW = 60f;
            float btnH = 26f;
            float btnY = rect.y + (rect.height - btnH) / 2f;
            float btnX = rect.xMax - 10f;

            // Load
            btnX -= btnW;
            if (isActive) GUI.color = new Color(1f, 1f, 1f, 0.3f);
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), "NPCStyleLimiter_Load".Translate()) && !isActive)
            {
                if (isDefault) CustomizerMod.Settings.ResetToDefaults();
                else CustomizerMod.Settings.LoadProfile(name);
                Messages.Message("NPCStyleLimiter_ProfileLoaded".Translate(name), MessageTypeDefOf.TaskCompletion, false);
                Close();
            }
            GUI.color = Color.white;

            if (!isDefault)
            {
                // Delete
                btnX -= (btnW + 4f);
                if (isActive) GUI.color = new Color(1f, 1f, 1f, 0.3f);
                if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), "NPCStyleLimiter_Delete".Translate()) && !isActive)
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDelete".Translate(name), () => {
                        CustomizerMod.Settings.DeleteProfile(name);
                    }, true));
                }
                GUI.color = Color.white;

                // Rename
                btnX -= (btnW + 4f);
                if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), "NPCStyleLimiter_Rename".Translate()))
                {
                    renameTarget = name;
                    renameBuffer = name;
                }
            }
        }

        private void DrawRenameField(Rect rect, string originalName)
        {
            float fieldW = rect.width * 0.6f;
            renameBuffer = Widgets.TextField(new Rect(rect.x + 28f, rect.y + 4f, fieldW, 28f), renameBuffer);
            
            float btnW = 45f;
            if (Widgets.ButtonText(new Rect(rect.x + 28f + fieldW + 6f, rect.y + 4f, btnW, 28f), "NPCStyleLimiter_OK".Translate()))
            {
                string trim = renameBuffer.Trim();
                if (!string.IsNullOrEmpty(trim) && trim != originalName)
                {
                    CustomizerMod.Settings.RenameProfile(originalName, trim);
                }
                renameTarget = null;
            }
            if (Widgets.ButtonText(new Rect(rect.x + 28f + fieldW + btnW + 10f, rect.y + 4f, btnW, 28f), "NPCStyleLimiter_Cancel".Translate()))
            {
                renameTarget = null;
            }
        }

        private void DoSave(string name)
        {
            if (CustomizerMod.Settings.ListProfiles().Contains(name))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("NPCStyleLimiter_ConfirmOverwrite".Translate(name), () =>
                {
                    CustomizerMod.Settings.SaveProfile(name);
                    Messages.Message("NPCStyleLimiter_ProfileSaved".Translate(name), MessageTypeDefOf.TaskCompletion, false);
                }));
            }
            else
            {
                CustomizerMod.Settings.SaveProfile(name);
                Messages.Message("NPCStyleLimiter_ProfileSaved".Translate(name), MessageTypeDefOf.TaskCompletion, false);
            }
        }
    }
}
