using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.GUI;

namespace Mod
{
    internal class Menu
    {

        private static bool guiVisible = false;
        private const float resizeGripSize = 20.0f;
        private static bool isResizing = false;

        public static bool npcDrawingsDropdown = false;
        public static bool npcClassificationsDropdown = false;
        public static bool itemDrawingsDropdown = false;

        public static void DrawModWindow(int windowID)
        {
            GUILayout.BeginVertical();

            npcDrawingsDropdown = GUILayout.Toggle(npcDrawingsDropdown, "NPC Drawings:", "button");
            if (npcDrawingsDropdown)
            {
                foreach (KeyValuePair<string, bool> entry in Settings.npcDrawings)
                {
                    bool result = GUILayout.Toggle(entry.Value, entry.Key);
                    if (result != entry.Value)
                    {
                        Settings.npcDrawings[entry.Key] = result;
                    }
                }
            }

            npcClassificationsDropdown = GUILayout.Toggle(npcClassificationsDropdown, "NPC Classifications:", "button");
            if (npcClassificationsDropdown)
            {
                foreach (KeyValuePair<string, bool> entry in Settings.npcClassifications)
                {
                    bool result = GUILayout.Toggle(entry.Value, entry.Key);
                    if (result != entry.Value)
                    {
                        Settings.npcClassifications[entry.Key] = result;
                    }
                }
            }

            itemDrawingsDropdown = GUILayout.Toggle(itemDrawingsDropdown, "Item Drawings:", "button");
            if (itemDrawingsDropdown)
            {
                foreach (KeyValuePair<string, bool> entry in Settings.itemDrawings)
                {
                    bool result = GUILayout.Toggle(entry.Value, entry.Key);
                    if (result != entry.Value)
                    {
                        Settings.itemDrawings[entry.Key] = result;
                    }
                }
            }

            Settings.useLootFilter = GUILayout.Toggle(Settings.useLootFilter, "Use Loot Filter");
            Settings.mapHack = GUILayout.Toggle(Settings.mapHack, "Map Hack");

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("* This requires a rezone (at the moment)");
            GUILayout.EndHorizontal();

            GUILayout.Label("Draw Distance: " + Settings.drawDistance.ToString("F1"));
            Settings.drawDistance = GUILayout.HorizontalSlider(Settings.drawDistance, 0.0f, 100.0f);

            GUILayout.Label("Auto Health Potion HP Threshold: " + Settings.autoHealthPotion.ToString("F1"));
            Settings.autoHealthPotion = GUILayout.HorizontalSlider(Settings.autoHealthPotion, 0.0f, 100.0f);

            GUILayout.EndVertical();

            Rect resizeGripRect = new Rect(windowRect.width - resizeGripSize, windowRect.height - resizeGripSize, resizeGripSize, resizeGripSize);
            GUI.Box(resizeGripRect, "");

            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            ProcessResizing(resizeGripRect, windowID);
        }


        private static void ProcessResizing(Rect resizeGripRect, int windowID)
        {
            Event currentEvent = Event.current;
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    // Check if the mouse is within the resize grip area
                    if (resizeGripRect.Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use(); // Mark the event as used
                        isResizing = true; // Set a flag indicating that we're resizing
                    }
                    break;

                case EventType.MouseUp:
                    isResizing = false; // Clear the resizing flag on mouse up
                    break;

                case EventType.MouseDrag:
                    if (isResizing)
                    {
                        // Directly adjust windowRect for resizing
                        windowRect.width += currentEvent.delta.x;
                        windowRect.height += currentEvent.delta.y;
                        // Enforce minimum size constraints
                        windowRect.width = Mathf.Max(windowRect.width, 250);
                        windowRect.height = Mathf.Max(windowRect.height, 200);
                        currentEvent.Use();
                    }
                    break;
            }
        }


        public static Rect windowRect = new Rect(20, 20, 250, 700);

        public static void OnGUI()
        {
            if (guiVisible)
            {
                windowRect = GUI.Window(0, windowRect, (WindowFunction)DrawModWindow, "LaSt EpOP");
            }
        }

        public static void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                guiVisible = !guiVisible;
            }
        }
    }
}
