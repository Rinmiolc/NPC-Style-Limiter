// Copyright (c) 2026 rinmiolc
// Licensed under the GNU General Public License v3.0.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace NPCStyleLimiter
{
    public class Dialog_ManageConfigs : Window
    {
        public override Vector2 InitialSize => new Vector2(520f, 460f);

        private Vector2 scrollPosition = Vector2.zero;
        private string saveAsName = "";
        private string renameBuffer = "";
        private string renameTarget = null;

        private static readonly Color AccentColor = new Color(0.78f, 0.55f, 0.15f);
        private static readonly Color PanelBgColor = new Color(1f, 1f, 1f, 0.02f);
        private static readonly Color HoverRowColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color InactiveTextColor = new Color(0.55f, 0.6f, 0.62f);

        public Dialog_ManageConfigs()
        {
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = inRect.y;

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, curY, inRect.width, 30f),
                "NPCStyleLimiter_ProfileManager".Translate());
            Text.Font = GameFont.Small;
            curY += 35f;

            // Current active profile indicator
            string activeName = CustomizerMod.Settings.currentProfileName;
            if (!string.IsNullOrEmpty(activeName))
            {
                GUI.color = AccentColor;
                Widgets.Label(new Rect(inRect.x, curY, inRect.width, 22f),
                    "NPCStyleLimiter_CurrentProfile".Translate(activeName));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = InactiveTextColor;
                Widgets.Label(new Rect(inRect.x, curY, inRect.width, 22f),
                    "NPCStyleLimiter_NoActiveProfile".Translate());
                GUI.color = Color.white;
            }
            curY += 26f;

            // Separator
            Widgets.DrawRectFast(new Rect(inRect.x, curY, inRect.width, 1f), new Color(1f, 1f, 1f, 0.1f));
            curY += 8f;

            // "Save As" row
            Widgets.Label(new Rect(inRect.x, curY + 3f, 70f, 24f),
                "NPCStyleLimiter_SaveAs".Translate());
            Rect nameFieldRect = new Rect(inRect.x + 75f, curY, inRect.width - 155f, 24f);
            saveAsName = Widgets.TextField(nameFieldRect, saveAsName);
            Rect saveBtnRect = new Rect(inRect.xMax - 72f, curY, 72f, 24f);

            bool canSave = !string.IsNullOrEmpty(saveAsName.Trim());
            if (!canSave) GUI.color = Color.gray;
            if (Widgets.ButtonText(saveBtnRect, "NPCStyleLimiter_Save".Translate()) && canSave)
            {
                string trimName = saveAsName.Trim();
                CustomizerMod.Settings.SaveProfile(trimName);
                Messages.Message(
                    "NPCStyleLimiter_ProfileSaved".Translate(trimName),
                    MessageTypeDefOf.TaskCompletion, false);
                saveAsName = "";
            }
            GUI.color = Color.white;
            curY += 32f;

            // Separator
            Widgets.DrawRectFast(new Rect(inRect.x, curY, inRect.width, 1f), new Color(1f, 1f, 1f, 0.1f));
            curY += 8f;

            // Profile list (scrollable)
            List<string> profiles = CustomizerMod.Settings.ListProfiles();
            float listHeight = inRect.yMax - curY - 10f;
            Rect scrollOuterRect = new Rect(inRect.x, curY, inRect.width, listHeight);

            if (profiles.Count == 0)
            {
                Widgets.DrawRectFast(scrollOuterRect, PanelBgColor);
                TextAnchor originalAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = InactiveTextColor;
                Widgets.Label(scrollOuterRect,
                    "NPCStyleLimiter_NoProfilesFound".Translate());
                GUI.color = Color.white;
                Text.Anchor = originalAnchor;
                return;
            }

            float rowHeight = 36f;
            float viewHeight = profiles.Count * rowHeight;
            Rect scrollInnerRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, scrollInnerRect);

            // Calculate visible range for performance
            int firstVisible = Mathf.FloorToInt(scrollPosition.y / rowHeight);
            int lastVisible = Mathf.CeilToInt((scrollPosition.y + listHeight) / rowHeight);
            firstVisible = Mathf.Clamp(firstVisible, 0, profiles.Count - 1);
            lastVisible = Mathf.Clamp(lastVisible, 0, profiles.Count - 1);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                if (i >= profiles.Count) break;

                string profileName = profiles[i];
                float rowY = i * rowHeight;
                Rect rowRect = new Rect(0f, rowY, scrollInnerRect.width, rowHeight);

                bool isActive = profileName == CustomizerMod.Settings.currentProfileName;
                bool isRenaming = renameTarget == profileName;

                // Row background
                if (isActive)
                {
                    Widgets.DrawRectFast(rowRect, new Color(0.78f, 0.55f, 0.15f, 0.12f));
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawRectFast(rowRect, HoverRowColor);
                }
                else if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rowRect);
                }

                // Active indicator dot
                float curX = 4f;
                if (isActive)
                {
                    GUI.color = AccentColor;
                    Widgets.Label(new Rect(curX, rowY + 8f, 16f, 20f), "\u25CF");
                    GUI.color = Color.white;
                }
                curX += 20f;

                // Name label or rename text field
                float nameWidth = scrollInnerRect.width * 0.42f;

                if (isRenaming)
                {
                    Rect renameFieldRect = new Rect(curX, rowY + 5f, nameWidth - 10f, 26f);
                    renameBuffer = Widgets.TextField(renameFieldRect, renameBuffer);

                    Rect okRect = new Rect(curX + nameWidth, rowY + 5f, 40f, 26f);
                    Rect cancelRect = new Rect(curX + nameWidth + 44f, rowY + 5f, 50f, 26f);

                    bool canConfirm = !string.IsNullOrEmpty(renameBuffer.Trim());
                    if (!canConfirm) GUI.color = Color.gray;
                    if (Widgets.ButtonText(okRect, "NPCStyleLimiter_Confirm".Translate()) && canConfirm)
                    {
                        string trimNew = renameBuffer.Trim();
                        CustomizerMod.Settings.RenameProfile(renameTarget, trimNew);
                        Messages.Message(
                            "NPCStyleLimiter_ProfileRenamed".Translate(trimNew),
                            MessageTypeDefOf.TaskCompletion, false);
                        renameTarget = null;
                        renameBuffer = "";
                    }
                    GUI.color = Color.white;

                    if (Widgets.ButtonText(cancelRect, "NPCStyleLimiter_Cancel".Translate()))
                    {
                        renameTarget = null;
                        renameBuffer = "";
                    }
                }
                else
                {
                    // Normal name label
                    if (isActive) GUI.color = AccentColor;
                    TextAnchor prevAnchor = Text.Anchor;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(curX, rowY, nameWidth, rowHeight), profileName);
                    Text.Anchor = prevAnchor;
                    GUI.color = Color.white;

                    // Action buttons
                    float btnW = 56f;
                    float btnH = 24f;
                    float btnY = rowY + (rowHeight - btnH) / 2f;
                    float btnX;

                    if (isActive)
                    {
                        // Active profile: Rename only (Load/Delete grayed out)
                        btnX = scrollInnerRect.width - 176f;

                        Rect renameBtnRect = new Rect(btnX, btnY, btnW, btnH);
                        if (Widgets.ButtonText(renameBtnRect, "NPCStyleLimiter_Rename".Translate()))
                        {
                            renameTarget = profileName;
                            renameBuffer = profileName;
                        }

                        btnX += btnW + 4f;
                        GUI.color = Color.gray;
                        Rect loadBtnRect = new Rect(btnX, btnY, btnW, btnH);
                        Widgets.ButtonText(loadBtnRect, "NPCStyleLimiter_Load".Translate());

                        btnX += btnW + 4f;
                        Rect deleteBtnRect = new Rect(btnX, btnY, btnW, btnH);
                        Widgets.ButtonText(deleteBtnRect, "NPCStyleLimiter_Delete".Translate());
                        GUI.color = Color.white;
                    }
                    else
                    {
                        // Non-active profile: all buttons available
                        btnX = scrollInnerRect.width - 256f;

                        Rect renameBtnRect = new Rect(btnX, btnY, btnW, btnH);
                        if (Widgets.ButtonText(renameBtnRect, "NPCStyleLimiter_Rename".Translate()))
                        {
                            renameTarget = profileName;
                            renameBuffer = profileName;
                        }
                        btnX += btnW + 4f;

                        Rect loadBtnRect = new Rect(btnX, btnY, btnW, btnH);
                        if (Widgets.ButtonText(loadBtnRect, "NPCStyleLimiter_Load".Translate()))
                        {
                            CustomizerMod.Settings.LoadProfile(profileName);
                            Messages.Message(
                                "NPCStyleLimiter_ProfileLoaded".Translate(profileName),
                                MessageTypeDefOf.TaskCompletion, false);
                            Close();
                        }
                        btnX += btnW + 4f;

                        Rect deleteBtnRect = new Rect(btnX, btnY, btnW, btnH);
                        if (Widgets.ButtonText(deleteBtnRect, "NPCStyleLimiter_Delete".Translate()))
                        {
                            CustomizerMod.Settings.DeleteProfile(profileName);
                            Messages.Message(
                                "NPCStyleLimiter_ProfileDeleted".Translate(profileName),
                                MessageTypeDefOf.CautionInput, false);
                        }
                    }
                }
            }

            Widgets.EndScrollView();
        }
    }
}
