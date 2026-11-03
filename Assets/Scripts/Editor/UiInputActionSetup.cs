using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    public static class UiInputActionSetup
    {
        private const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
        private const string ReferenceFolder = "Assets/UI/Phase4/UIActionReferences";
        // Serialized slot names from the project's installed Input System 1.20.0.
        private static readonly string[] Slots = {
            "m_PointAction", "m_LeftClickAction", "m_RightClickAction", "m_MiddleClickAction",
            "m_ScrollWheelAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction",
            "m_TrackedDevicePositionAction", "m_TrackedDeviceOrientationAction"
        };
        private static readonly string[] Actions = {
            "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel", "Navigate", "Submit", "Cancel",
            "TrackedDevicePosition", "TrackedDeviceOrientation"
        };

        [MenuItem("Tools/Zhuzbai/Repair Input References")]
        public static void RepairScenes()
        {
            U.RequireEditMode();
            foreach (string path in new[] { U.Menu, U.Level })
                if (!File.Exists(path)) throw new FileNotFoundException("Run Apply Main Menu and Game UI first; scene is missing.", path);
            foreach (string path in new[] { U.Menu, U.Level }) U.EditScene(path, scene => {
                var systems = U.All<EventSystem>(scene);
                var modules = U.All<InputSystemUIInputModule>(scene);
                if (systems.Length != 1 || modules.Length != 1 || modules[0].gameObject != systems[0].gameObject)
                    throw new InvalidOperationException(scene.name + ": expected one existing EventSystem with InputSystemUIInputModule. Run Apply Main Menu and Game UI first.");
                Configure(modules[0]);
                Validate(modules[0], scene.name);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("Persistent UI action references repaired and saved in MainMenu and MainLevel. PlayerControls and gameplay/menu/audio objects were not rebuilt.");
        }

        public static void Configure(InputSystemUIInputModule module)
        {
            U.RequireEditMode();
            if (module == null) throw new ArgumentNullException(nameof(module));
            var asset = LoadUiAsset();
            var references = Actions.Select(name => PersistentReference(asset, name)).ToArray();
            bool enabled = module.enabled;
            // Disable hooks before editing serialized references, and restore the prior enabled state.
            // The public setter uses SwapAction: same action => no-op, even for a different wrapper.
            // SerializedObject is the Editor assignment path and does not silently retain scene wrappers.
            module.enabled = false;
            try
            {
                var data = new SerializedObject(module);
                Property(data, "m_ActionsAsset").objectReferenceValue = asset;
                for (int i = 0; i < Slots.Length; i++) Property(data, Slots[i]).objectReferenceValue = references[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            finally { module.enabled = enabled; }
            EditorUtility.SetDirty(module);
            if (PrefabUtility.IsPartOfPrefabInstance(module)) PrefabUtility.RecordPrefabInstancePropertyModifications(module);
            Validate(module, module.gameObject.scene.name);
        }

        [MenuItem("Tools/Zhuzbai/Test Input References")]
        public static void TestReferenceRepair()
        {
            U.RequireEditMode();
            var preview = EditorSceneManager.NewPreviewScene();
            var temporary = new List<InputActionReference>();
            try
            {
                var root = new GameObject("UI reference repair regression", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(root, preview);
                var module = root.AddComponent<InputSystemUIInputModule>();
                module.enabled = false;
                var asset = LoadUiAsset();
                var data = new SerializedObject(module);
                Property(data, "m_ActionsAsset").objectReferenceValue = asset;
                for (int i = 0; i < Slots.Length; i++)
                {
                    var reference = InputActionReference.Create(asset.FindAction("UI/" + Actions[i], true));
                    temporary.Add(reference);
                    Property(data, Slots[i]).objectReferenceValue = reference;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                // Reproduce 1.20.0's same-action setter no-op before checking the fix.
                module.point = PersistentReference(asset, "Point");
                if (module.point != temporary[0]) throw new InvalidOperationException("Expected Input System 1.20.0 same-action wrapper behavior was not reproduced.");
                Configure(module);
                Validate(module, "Preview regression");
                var first = new SerializedObject(module);
                var saved = Slots.Select(slot => Property(first, slot).objectReferenceValue).ToArray();
                Configure(module);
                var second = new SerializedObject(module);
                for (int i = 0; i < Slots.Length; i++)
                    if (Property(second, Slots[i]).objectReferenceValue != saved[i])
                        throw new InvalidOperationException("Repeated UI repair replaced a persistent action reference.");
                if (module.enabled) throw new InvalidOperationException("UI repair did not preserve the prior module-enabled state.");
                Debug.Log("UI reference regression passed: same-action setter issue reproduced, all ten references repaired to assets, repeated repair reused them, and module enabled state preserved.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                foreach (var reference in temporary) if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
            }
        }

        private static InputActionAsset LoadUiAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (asset == null || !AssetDatabase.Contains(asset) || !EditorUtility.IsPersistent(asset))
                throw new InvalidOperationException("Missing persistent UI action asset: " + ActionsPath);
            foreach (string name in Actions)
                if (asset.FindAction("UI/" + name) == null)
                    throw new InvalidOperationException("Missing UI/" + name + " in " + ActionsPath + ". PlayerControls is gameplay-only and must not replace this UI asset.");
            return asset;
        }

        private static InputActionReference PersistentReference(InputActionAsset asset, string name)
        {
            InputAction action = asset.FindAction("UI/" + name, true);
            // Prefer the importer's existing saved subassets; ignore transient and scene-local wrappers.
            var imported = AssetDatabase.LoadAllAssetsAtPath(ActionsPath).OfType<InputActionReference>()
                .Where(reference => reference != null && reference.asset == asset && reference.action != null &&
                    reference.action.id == action.id && AssetDatabase.Contains(reference) && EditorUtility.IsPersistent(reference))
                .OrderBy(reference => (reference.hideFlags & HideFlags.HideInHierarchy) != 0).FirstOrDefault();
            if (imported != null) return imported;

            // Fallback for importer configurations that do not expose action-reference subassets.
            // A deterministic path reuses the same GUID on every repair; never edit the .inputactions JSON.
            U.Folder(ReferenceFolder);
            string path = ReferenceFolder + "/" + name + ".asset";
            var referenceAsset = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (referenceAsset == null)
            {
                if (File.Exists(path)) throw new IOException("UI reference asset path is occupied by another asset: " + path);
                referenceAsset = InputActionReference.Create(action);
                AssetDatabase.CreateAsset(referenceAsset, path);
            }
            else if (referenceAsset.action == null || referenceAsset.asset != asset || referenceAsset.action.id != action.id)
            {
                referenceAsset.Set(action);
                EditorUtility.SetDirty(referenceAsset);
            }
            return referenceAsset;
        }

        public static void Validate(InputSystemUIInputModule module, string sceneName)
        {
            if (module == null) throw new InvalidOperationException(sceneName + ": UI input module is missing.");
            var expectedAsset = LoadUiAsset();
            if (module.actionsAsset != expectedAsset)
                throw new InvalidOperationException(sceneName + ": UI module actionsAsset must be " + ActionsPath + ", not PlayerControls or a transient clone.");
            var data = new SerializedObject(module);
            for (int i = 0; i < Slots.Length; i++)
            {
                string slot = sceneName + "/EventSystem." + Slots[i] + " (UI/" + Actions[i] + ")";
                var reference = Property(data, Slots[i]).objectReferenceValue as InputActionReference;
                if (reference == null) throw new InvalidOperationException(slot + ": reference is missing. Run Repair UI Action References.");
                var action = reference.action;
                if (action == null) throw new InvalidOperationException(slot + ": saved action ID does not resolve. Run Repair UI Action References.");
                if (reference.asset != expectedAsset || action.id != expectedAsset.FindAction("UI/" + Actions[i], true).id)
                    throw new InvalidOperationException(slot + ": reference resolves to the wrong asset/action. Run Repair UI Action References.");
                if (!AssetDatabase.Contains(reference) || !EditorUtility.IsPersistent(reference) ||
                    string.IsNullOrEmpty(AssetDatabase.GetAssetPath(reference)))
                    throw new InvalidOperationException(slot + ": action is valid, but its wrapper is scene-local or transient, not a persistent asset. Run Repair UI Action References.");
            }
        }

        private static SerializedProperty Property(SerializedObject data, string name) =>
            data.FindProperty(name) ?? throw new InvalidOperationException("Input System UI field was not found: " + name + ". This setup targets Input System 1.20.0.");
    }
}
