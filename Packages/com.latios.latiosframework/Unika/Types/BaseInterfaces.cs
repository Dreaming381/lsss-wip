using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Unika
{
    public interface IScript
    {
        public Entity entity { get; }

        public EntityScriptCollection allScripts { get; }

        public int indexInEntity { get; }

        public byte userByte { get; set; }

        public bool userFlagA { get; set; }

        public bool userFlagB { get; set; }

        // Should be explicit implementations only
        public ScriptRef ToRef();
    }

    public interface IScriptTyped : IScript
    {
        public Script ToScript();

        bool TryCastInit(in Script script);
    }
}

