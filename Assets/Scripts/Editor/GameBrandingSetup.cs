using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    public static class GameBrandingSetup
    {
        public const string ProductName = "Darius Game";
        public const string CompanyName = "Darius Studio";

        [MenuItem("Tools/Zhuzbai/Setup Game Branding")]
        public static void Apply()
        {
            U.RequireEditMode();
            U.EditScene(U.Menu, scene => {
                var controllers = U.All<GameMenuController>(scene);
                if (controllers.Length != 1) throw new InvalidOperationException("Expected one existing MainMenu controller. Branding never rebuilds the menu.");
                var main = controllers[0].transform.Find("Safe Area/Main") as RectTransform;
                if (main == null || main.Find("Title") == null)
                    throw new InvalidOperationException("Existing MainMenu title was not found at Safe Area/Main/Title.");
                ConfigureTitle(main);
            });
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("DARIUS GAME branding applied and saved. Product: Darius Game. Company: Darius Studio. Rebuild the standalone player to update its application window title.");
        }

        internal static void ConfigureTitle(RectTransform main)
        {
            // The shared helper reuses the named title object; no duplicate labels or effects.
            var title = GameMenuSetup.Label(main, "Title", "DARIUS\nGAME",
                new Vector2(0, -104), new Vector2(680, 190), 78, new Color32(255, 211, 103, 255));
            title.alignment = TextAnchor.MiddleCenter;
            title.lineSpacing = 0.95f;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 52;
            title.resizeTextMaxSize = 78;
            var shadow = title.GetComponent<Shadow>();
            if (shadow == null) shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(8, 18, 35, 220);
            shadow.effectDistance = new Vector2(4, -4);
            EditorUtility.SetDirty(title);
            EditorUtility.SetDirty(shadow);
        }

        internal static void ConfigurePlayerSettings()
        {
            // Unity persists these through Player Settings. Product Name supplies the player window title.
            if (PlayerSettings.productName != ProductName) PlayerSettings.productName = ProductName;
            if (PlayerSettings.companyName != CompanyName) PlayerSettings.companyName = CompanyName;
        }

        [MenuItem("Tools/Zhuzbai/Validate Game Branding")]
        public static void Validate()
        {
            U.RequireEditMode();
            if (PlayerSettings.productName != ProductName || PlayerSettings.companyName != CompanyName)
                throw new InvalidOperationException("Incorrect Product/Company Name. Run Apply Darius Game Branding.");
            U.EditScene(U.Menu, scene => {
                var controllers = U.All<GameMenuController>(scene);
                if (controllers.Length != 1) throw new InvalidOperationException("Expected one MainMenu controller.");
                var main = controllers[0].transform.Find("Safe Area/Main");
                var title = main != null ? main.Find("Title") : null;
                var text = title != null ? title.GetComponent<Text>() : null;
                if (text == null || text.text != "DARIUS\nGAME" || text.alignment != TextAnchor.MiddleCenter || text.fontSize != 78)
                    throw new InvalidOperationException("Main menu title branding/layout is missing. Run Apply Darius Game Branding.");
            }, false);
            Debug.Log("DARIUS GAME branding validated: menu title and Unity Product/Company Name.");
        }
    }
}
