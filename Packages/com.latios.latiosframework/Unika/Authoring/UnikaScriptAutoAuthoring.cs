using Unity.Entities;

namespace Latios.Unika.Authoring
{
    /// <summary>
    /// A base class for a script authoring component whose fields and Bake() method are generated
    /// automatically from the script's fields by a source generator. Inherit this instead of
    /// UnikaScriptAuthoring&lt;T&gt; to have the authoring component stay in sync with the script
    /// as it evolves. You may still add your own additional fields directly to your partial class,
    /// and override OnAutoBake() to add custom baking logic.
    /// </summary>
    /// <typeparam name="T">The type of script this authoring component generates</typeparam>
    public abstract class UnikaScriptAutoAuthoring<T> : UnikaScriptAuthoring<T> where T : unmanaged, IUnikaScript, IUnikaScriptGen
    {
        /// <summary>
        /// Returns true by default. Override this if your authoring component's validity depends on
        /// additional fields you have added.
        /// </summary>
        public override bool IsValid() => true;

        /// <summary>
        /// Override this method to add additional baking logic beyond what is auto-generated from the
        /// script's fields. Examples include adding tag components, setting TransformUsageFlags, or baking
        /// a blob asset and assigning it to a field on the script.
        /// This is called after the auto-generated fields have been copied onto the script, and before
        /// the script is committed to the baker (assigned to toAssign).
        /// </summary>
        /// <param name="baker">The baker being used to bake the script</param>
        /// <param name="script">The script data, already populated from this authoring component's auto-generated fields</param>
        /// <param name="toAssign">An object to assign the userByte and flags to</param>
        /// <param name="smartPostProcessTarget">A baking-only entity from which you can receive a reference to the script in an ISmartPostProcessItem
        /// before the script is merged into the scripts buffer</param>
        protected virtual void OnAutoBake(IBaker baker, ref T script, ref AuthoredScriptAssignment toAssign, Entity smartPostProcessTarget)
        {
        }
    }
}

