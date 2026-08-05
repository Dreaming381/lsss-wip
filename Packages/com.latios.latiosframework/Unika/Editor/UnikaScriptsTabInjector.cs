using Latios.Unika;
using TabView = Unity.Entities.Editor.TabView;
using Unity.Entities;
using Unity.Entities.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

// Warning: This whole implementation is a hack around Unity's inextensible Entities editor tooling.

namespace Latios.Unika.Editor
{
    /// <summary>
    /// Injects a Unika Scripts tab into the built-in Entity Inspector's TabView without modifying
    /// com.unity.entities source. EntityEditor.EntityInspectorContent.Tabs is a hardcoded array
    /// baked at compile time, so instead this appends a tab at runtime to the already-built
    /// TabView control via its internal Internal_AddTab, the same mechanism TabContent itself
    /// uses to self-register. Requires InternalsVisibleTo("Latios.Unika.Editor") from
    /// Unity.Entities.Editor (see Editor/EditorInternals/InternalsVisibleTo.cs).
    /// </summary>
    [InitializeOnLoad]
    static class UnikaScriptsTabInjector
    {
        static UnikaScriptsTabInjector() => Subscribe();

        [DidReloadScripts]
        static void OnReload() => Subscribe();

        static void Subscribe()
        {
            // Selection changes and Unity's own caching/rebuilding of EntityEditor/TabView
            // instances don't expose a reliable single event to hook (EntityEditor delays its
            // own content generation, and re-selecting a previously-seen object doesn't always
            // rebuild the TabView on a predictable schedule). Polling every frame is simple and
            // cheap - TryInject is a no-op as soon as HasTab finds the tab already present, and
            // does nothing at all when no open Inspector is showing an entity with Unika scripts.
            EditorApplication.update -= TryInject;
            EditorApplication.update += TryInject;

            // The tab's read-only state and its PropertyElement's own EditorApplication.isPlaying
            // driven behavior are only evaluated when the tab is built - a tab built while stopped
            // would otherwise stay frozen read-only forever, even after pressing Play. Strip it on
            // every play-mode transition and let the poll above rebuild it fresh.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange _) => RemoveInjectedTabs();

        static void RemoveInjectedTabs()
        {
            foreach (var editor in UnityEngine.Resources.FindObjectsOfTypeAll<EntityEditor>())
            {
                var root = editor.CreateInspectorGUI();
                if (root == null || root.Q(className: "tab-view") is not TabView tabView)
                    continue;

                TabContent ourContent = null;
                foreach (var child in tabView.Children())
                {
                    if (child is TabContent tc && tc.TabName == UnikaScriptsTabContent.TabName)
                    {
                        ourContent = tc;
                        break;
                    }
                }
                if (ourContent == null)
                    continue;

                var header = FindHeader(tabView);
                var label  = header != null? FindLabel(header, UnikaScriptsTabContent.TabName) : null;

                // If our tab is currently active, switch away first so TabView isn't left pointing
                // at a removed index - mirrors what Internal_AddTab itself does on structural changes.
                if (label != null && FindIndex(header, label) == tabView.value)
                    tabView.SetValueWithoutNotify(0);

                label?.RemoveFromHierarchy();
                ourContent.RemoveFromHierarchy();
            }
        }

        static void TryInject()
        {
            foreach (var editor in UnityEngine.Resources.FindObjectsOfTypeAll<EntityEditor>())
            {
                var context = editor.m_InspectorContext;
                if (!context.TargetExists() || !context.EntityManager.HasComponent<UnikaScripts>(context.Entity))
                    continue;

                // Returns the cached root if already built; safe to call repeatedly.
                var root = editor.CreateInspectorGUI();
                if (root == null || root.Q(className: "tab-view") is not TabView tabView)
                    continue;

                var header = FindHeader(tabView);
                if (header == null || FindLabel(header, UnikaScriptsTabContent.TabName) != null)
                    continue;

                tabView.Internal_AddTab(UnikaScriptsTabContent.Build(context));

                // Just-added tab's header label is now the last child.
                var index = header.hierarchy.childCount - 1;
                var label = FindLabel(header, UnikaScriptsTabContent.TabName);
                if (label != null)
                    InterceptClick(label, tabView, index);
            }
        }

        // TabViewDrawer (the built-in ITabContent[]/TabView binding, Unity.Entities.Editor's
        // Common/Controls/TabView/TabViewDrawer.cs) registers a single ChangeEvent<int> handler
        // on the TabView that closes over arrays sized to the original 3 built-in tabs. Every
        // tab header's click handler (installed by TabView.AddTab, used for every tab including
        // ours) sets TabView.value on click, which fires that ChangeEvent - so clicking our tab
        // (index 3) crashes that handler with an out-of-range index into its stale length-3
        // arrays. We can't remove that handler (no reference to it), so instead we intercept the
        // click in the TrickleDown phase - which runs before the target element's own bubble-phase
        // handler - stop it from reaching Unity's handler, and drive the switch ourselves via the
        // public SetValueWithoutNotify, which updates the same visual state without sending the
        // event that crashes.
        static void InterceptClick(VisualElement label, TabView tabView, int index)
        {
            label.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopImmediatePropagation();
                tabView.SetValueWithoutNotify(index);
            }, TrickleDown.TrickleDown);
        }

        // TabView.Tabs (the public getter) reads a backing list that only the Tabs *setter*
        // populates - Internal_AddTab appends directly to the header/content VisualElements
        // without touching it, so it never reflects tabs added this way. Look at the rendered
        // header instead, walking the raw hierarchy since TabView overrides contentContainer
        // to point at its content area (so Children()/Q() alone would miss the header).
        static VisualElement FindHeader(VisualElement tabView)
        {
            VisualElement header = null;
            void Walk(VisualElement ve)
            {
                if (header != null)
                    return;
                if (ve.ClassListContains("tab-view__tab-header"))
                {
                    header = ve;
                    return;
                }
                foreach (var child in ve.hierarchy.Children())
                    Walk(child);
            }
            Walk(tabView);
            return header;
        }

        static Label FindLabel(VisualElement header, string tabName)
        {
            foreach (var child in header.hierarchy.Children())
            {
                if (child is Label label && label.text == tabName)
                    return label;
            }
            return null;
        }

        static int FindIndex(VisualElement header, VisualElement label)
        {
            var i = 0;
            foreach (var child in header.hierarchy.Children())
            {
                if (child == label)
                    return i;
                i++;
            }
            return -1;
        }
    }
}

