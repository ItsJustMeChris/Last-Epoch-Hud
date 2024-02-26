using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Mod
{
    internal class Drawing
    {
        public static Texture2D lineTex = new Texture2D(1, 1);
        public static GUIStyle StringStyle { get; set; } = new GUIStyle(GUI.skin.label);

        static Vector2 ClampToScreen(Vector3 vecIn, Vector3 padding)
        {
            if (vecIn.z < 0)
            {
                vecIn *= -1;
            }

            return new Vector2(
                               Mathf.Clamp(vecIn.x, padding.x, Screen.width - padding.x),
                                              Mathf.Clamp(vecIn.y, padding.y, Screen.height - padding.y)
                                                         );
        }

        public static void DrawString(Vector3 worldPosition, string label, bool centered = true)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(worldPosition);
            screen.y = Screen.height - screen.y;
            // Clamp the label to the screen
            Vector2 position = ClampToScreen(screen, new Vector2(25, 25));

            var content = new GUIContent(label);
            var size = StringStyle.CalcSize(content);
            var upperLeft = centered ? position - size / 2f : position;
            GUI.Label(new Rect(upperLeft, size), content);
        }

        public static void DrawString(Vector3 worldPosition, string label, Color color, bool centered = true)
        {
            var backup = StringStyle.normal.textColor;
            StringStyle.normal.textColor = color;
            DrawString(worldPosition, label, centered);
            StringStyle.normal.textColor = backup;
        }

        public static void DrawLine(Vector3 worldA, Vector3 worldB, Color color, float width)
        {
            if (lineTex == null)
            {
                lineTex = new Texture2D(1, 1);
            }

            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;

            Vector3 screenA = Camera.main.WorldToScreenPoint(worldA);
            Vector3 screenB = Camera.main.WorldToScreenPoint(worldB);

            screenA.y = Screen.height - screenA.y;
            screenB.y = Screen.height - screenB.y;


            // Clamp points to screen with padding
            Vector2 pointA = ClampToScreen(screenA, new Vector2(25, 25));
            Vector2 pointB = ClampToScreen(screenB, new Vector2(25, 25));

            // Calculate angle and magnitude
            float angle = Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x) * 180f / Mathf.PI;
            float magnitude = (pointB - pointA).magnitude;

            // Apply color
            GUI.color = color;

            // Create matrix for rotation
            Matrix4x4 matrix = Matrix4x4.TRS(pointA, Quaternion.Euler(0, 0, angle), Vector3.one);

            // Apply the matrix
            GUI.matrix = matrix;

            // Draw the line
            GUI.DrawTexture(new Rect(0, -width / 2, magnitude, width), lineTex);

            // Revert GUI color and matrix to previous state
            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        public static Color ItemRarityToColor(string rarity)
        {
            var color = Color.white;

            if (rarity.Contains("Magic"))
            {
                color = Color.blue;
            }
            else if (rarity.Contains("Common"))
            {
                color = Color.white;
            }
            else if (rarity.Contains("Unique"))
            {
                color = Color.red;
            }
            else if (rarity.Contains("Rare"))
            {
                color = Color.yellow;
            }
            else if (rarity.Contains("Set"))
            {
                color = Color.green;
            }
            else if (rarity.Contains("Exalted"))
            {
                color = new Color(0.5f, 0, 0.5f);
            }

            return color;
        }

        public static Color AlignmentToColor(string alignment)
        {
            var color = Color.white;
            switch (alignment)
            {
                case "Good":
                    color = Color.green;
                    break;
                case "Evil":
                    color = Color.red;
                    break;
                case "Barrel":
                    color = Color.yellow;
                    break;
                case "HostileNeutral":
                    color = Color.blue;
                    break;
                case "FriendlyNeutral":
                    color = Color.cyan;
                    break;
                case "SummonedCorpse":
                    color = Color.magenta;
                    break;
            }

            return color;
        }
    }
}
