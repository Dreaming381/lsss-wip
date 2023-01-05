using UnityEngine;

[UnityEditor.InitializeOnLoad]
static class CreateMenuCleanup
{
    static CreateMenuCleanup()
    {
        //UnityEditor.EditorApplication.delayCall += RemoveMenuItems;
        RemoveMenuItems();
    }

    private static void RemoveMenuItems()
    {
        //UnityEngine.Debug.Log("Running removal script");

        System.Reflection.MethodInfo removeMenuItemMethod = typeof(UnityEditor.Menu).GetMethod("RemoveMenuItem",
                                                                                               System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (removeMenuItemMethod == null)
        {
            Debug.LogError("Method 'RemoveMenuItem' not found!");
            return;
        }

        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/2D");
        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/Text");
        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/Scene Template");
        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/Scene Template From Scene");
        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/Scene Template Pipeline");
        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/Lens Flare");
        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/Avatar Mask");
        RemoveMenuItem(removeMenuItemMethod, "Assets/Create/Physic Material");
    }

    private static void RemoveMenuItem(System.Reflection.MethodInfo removeMenuItemMethod, string name)
    {
        removeMenuItemMethod.Invoke(null, new object[] { name });
    }
}

