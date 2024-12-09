using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Myri.Driver
{
    internal static class DriverSelection
    {
        public static bool useManagedDriver
        {
#if !UNITY_EDITOR
            get => false;
#else
            get => UnityEditor.EditorPrefs.GetBool(editorPrefName);
            private set => UnityEditor.EditorPrefs.SetBool(editorPrefName, value);
#endif
        }

#if UNITY_EDITOR
        static string editorPrefName = $"{UnityEditor.PlayerSettings.productName}_MyriEditorManagedDriver";

        [UnityEditor.MenuItem("Edit/Latios/Use Myri Editor Managed Driver")]
        public static void ToggleDriver()
        {
            var currentState = useManagedDriver;
            currentState     = !currentState;
            useManagedDriver = currentState;
            UnityEditor.Menu.SetChecked("Edit/Latios/Use Myri Editor Managed Driver", currentState);
        }
#endif
    }
}

