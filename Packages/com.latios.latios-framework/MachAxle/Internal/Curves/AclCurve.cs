using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.MachAxle
{
    // Todo: Optimize this to batch evaluations by shared inputs or even shared "time" values.
    struct AclCurve : ICurve
    {
        public int clipIndex;
        public int sourcePortsStart;
        public int sourcePortsCount;
        public int destinationPortSpansStart;
        public int destinationPortSpansCount;
        public int journalBaseIndex;

        public void Evaluate<T>(ref Graph graph, ref T bus) where T : unmanaged, IBus
        {
            bus.SetJournalCurveBaseIndex(journalBaseIndex);
            ref var clip             = ref graph.parameterClips[clipIndex];
            var     sourcePorts      = graph.sourcePorts.AsSpan().Slice(sourcePortsStart, sourcePortsCount);
            var     destinationPorts = graph.destinationPortSpans.AsSpan().Slice(destinationPortSpansStart, destinationPortSpansCount);
            for (int i = 0; i < sourcePortsCount; i++)
            {
                bus.SetJournalCurveLocalIndex(i);
                bus.SetPortIndex(0);
                float input  = bus.Import(sourcePorts[i]);
                float output = clip.SampleParameter(i, input);
                bus.SetPortIndex(1);
                bus.Export(destinationPorts[i], output);
            }
        }
    }
}

