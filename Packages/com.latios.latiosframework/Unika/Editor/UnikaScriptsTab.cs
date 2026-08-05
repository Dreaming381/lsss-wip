using System.Collections.Generic;
using Latios.Unika;
using Unity.Entities;
using Unity.Entities.Editor;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Warning: This Editor is vibe-coded. Editor UI scripting isn't really something I enjoy.
// Hopefully this is useful. If you want to make it better, a pull request would be great!

namespace Latios.Unika.Editor
{
    /// <summary>
    /// Builds the content for the Unika scripts tab injected into the built-in Entity Inspector
    /// (see UnikaScriptsTabInjector). One foldout per script, mirroring ComponentsTab's own rules:
    /// PropertyElement bound to the script's boxed contents (Unity.Entities.UI, accessible via
    /// InternalsVisibleTo from Editor/UIInternals/InternalsVisibleTo.cs) drives the field widgets,
    /// and the fields container is disabled outside Play mode the same way
    /// ComponentElementBase.SetReadonly disables a component's foldout content.
    /// </summary>
    static class UnikaScriptsTabContent
    {
        public const string TabName = "Unika Scripts";

        // Foldout expanded/collapsed state per script instanceId, preserved across the play-mode
        // triggered rebuilds in UnikaScriptsTabInjector (otherwise every foldout would collapse
        // each time Play is pressed).
        static readonly Dictionary<int, bool> s_FoldoutExpanded = new Dictionary<int, bool>();

        public static TabContent Build(EntityInspectorContext context)
        {
            var tabContent = new TabContent { TabName = TabName };

            if (!context.TargetExists() || !context.EntityManager.HasComponent<UnikaScripts>(context.Entity))
            {
                tabContent.Add(new Label("No Unika scripts on this entity."));
                return tabContent;
            }

            var buffer = context.EntityManager.GetBuffer<UnikaScripts>(context.Entity);
            foreach (var view in buffer.AllScripts(context.Entity).managedView)
                tabContent.Add(BuildScriptFoldout(view, context));

            return tabContent;
        }

        static Foldout BuildScriptFoldout(ManagedScriptView view, EntityInspectorContext context)
        {
            var instanceId = view.instanceId;

            var foldout = new Foldout
            {
                text  = $"{view.scriptType.Name} ID:{instanceId}",
                value = s_FoldoutExpanded.TryGetValue(instanceId, out var expanded) && expanded,
            };
            foldout.RegisterValueChangedCallback(evt => s_FoldoutExpanded[instanceId] = evt.newValue);

            // Same header the built-in ComponentsTab uses: lighter section background, bold text,
            // and a near-black top border between consecutive scripts. The dropdown arrow is
            // Foldout's own built-in visual, already present.
            Unity.Entities.Editor.Resources.Templates.Inspector.ComponentHeader.AddStyles(foldout);
            foldout.AddToClassList(Unity.Entities.Editor.UssClasses.Inspector.Component.Header);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            const float kThreeSpacebarsWidth = 20f;

            var userFlagAField = new Toggle("Flag A") {
                value          = view.userFlagA, style = { marginRight = kThreeSpacebarsWidth }
            };
            userFlagAField.RegisterValueChangedCallback(evt => view.userFlagA = evt.newValue);
            row.Add(userFlagAField);

            var userFlagBField = new Toggle("Flag B") {
                value          = view.userFlagB, style = { marginRight = kThreeSpacebarsWidth }
            };
            userFlagBField.RegisterValueChangedCallback(evt => view.userFlagB = evt.newValue);
            row.Add(userFlagBField);

            // userByte is last and flex-grows to fill the remaining row width, rather than being
            // sized to its own content - IntegerField otherwise visibly jitters wider/narrower as
            // digits are typed, which looks broken for a field whose only valid range is [0, 255].
            var userByteField = new IntegerField("User Byte") {
                value         = view.userByte, style = { flexGrow = 1, flexBasis = 0 }
            };
            userByteField.RegisterValueChangedCallback(evt => view.userByte = (byte)Mathf.Clamp(evt.newValue, 0, 255));
            row.Add(userByteField);

            foldout.Add(row);

            var boxed   = view.contents;
            var content = new PropertyElement();
            if (EditorApplication.isPlaying)
                content.userData = content;
            content.AddContext(context);
            content.SetTarget(boxed);
            content.OnChanged += (element, _) => view.contents = element.GetTarget<object>();
            foldout.Add(content);

            // Value boxes within this script's PropertyElement-generated fields start at a
            // consistent horizontal position, mirroring ComponentsTab.Build's own root-level call.
            // Scoped to just the PropertyElement (not the whole foldout) so it doesn't also stretch
            // the compact userByte/flags row's labels to match - that row is deliberately narrow.
            content.RegisterCallback<GeometryChangedEvent, VisualElement>(
                (_, elem) => StylingUtility.AlignInspectorLabelWidth(elem), content);

            // Same indent ComponentsTab applies to a component's expanded content.
            foldout.contentContainer.AddToClassList(Unity.Entities.Editor.UssClasses.Inspector.Component.Container);
            foldout.contentContainer.SetEnabled(!context.IsReadOnly);

            return foldout;
        }
    }
}

