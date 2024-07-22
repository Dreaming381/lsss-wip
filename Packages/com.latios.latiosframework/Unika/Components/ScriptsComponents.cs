using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Unika
{
    public interface IUnikaInterface
    {
    }

    public interface IUnikaScript
    {
    }

    public struct UnikaScripts : IBufferElementData
    {
        internal ScriptHeader header;
    }
}

