using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.MachAxle
{
    interface IBus
    {
        float Import(Port port);
        void Export(PortSpan portSpan, float result);

        void SetJournalCurveBaseIndex(int index);
        void SetJournalCurveLocalIndex(int index);
        void SetPortIndex(int index);
    }

    unsafe struct BaseBus : IBus
    {
        public Graph* graph;
        public float* externalInputs;
        public float* externalOutputs;
        public float* internalInputs;
        public float* internalOutputs;

        public float Import(Port port)
        {
            if (port.isExternal)
                return externalInputs[port.index];
            return internalInputs[port.index];
        }

        public void Export(PortSpan portSpan, float result)
        {
            var start = portSpan.start;
            for (int i = 0; i < portSpan.count; i++)
            {
                var         port     = graph->destinationPorts[start + i];
                float*      array    = port.isExternal ? externalOutputs : internalOutputs;
                BitField32* bitArray =
                    port.isExternal ? (BitField32*)graph->outputConstants.useAddAggregation.GetUnsafePtr() : (BitField32*)graph->temporaryConstants.useAddAggregation.GetUnsafePtr();
                int  index = port.index;
                bool add   = bitArray[index >> 5].IsSet(index & 0x1f);
                if (add)
                    array[index] += result;
                else
                    array[index] *= result;
            }
        }

        public void SetJournalCurveBaseIndex(int index)
        {
        }

        public void SetJournalCurveLocalIndex(int index)
        {
        }

        public void SetPortIndex(int index)
        {
        }
    }

    unsafe struct InstanceBus : IBus
    {
        public Graph* graph;
        public float* externalBaseInputs;
        public float* externalBaseOutputs;
        public float* internalBaseInputs;
        public float* internalBaseOutputs;

        public float* externalInstanceInputs;
        public float* externalInstanceOutputs;
        public float* internalInstanceInputs;
        public float* internalInstanceOutputs;

        public int groupIndex;

        public float Import(Port port)
        {
            var index = port.index;
            return port.type switch
                   {
                       CellType.External => externalBaseInputs[index],
                       CellType.Internal => internalBaseInputs[index],
                       CellType.InstancedExternal => externalInstanceInputs[index],
                       CellType.InstancedInternal => internalInstanceInputs[index],
                       _ => 0f,
                   };
        }

        public void Export(PortSpan portSpan, float result)
        {
            var start = portSpan.start;
            for (int i = 0; i < portSpan.count; i++)
            {
                var port = graph->destinationPorts[start + i];

                float*      array    = null;
                BitField32* bitArray = null;
                switch (port.type)
                {
                    case CellType.External:
                        array    = externalBaseOutputs;
                        bitArray = (BitField32*)graph->outputConstants.useAddAggregation.GetUnsafePtr();
                        break;
                    case CellType.Internal:
                        array    = internalBaseOutputs;
                        bitArray = (BitField32*)graph->temporaryConstants.useAddAggregation.GetUnsafePtr();
                        break;
                    case CellType.InstancedExternal:
                        array    = externalInstanceOutputs;
                        bitArray = (BitField32*)graph->instanceGroups[groupIndex].outputConstants.useAddAggregation.GetUnsafePtr();
                        break;
                    case CellType.InstancedInternal:
                        array    = internalInstanceOutputs;
                        bitArray = (BitField32*)graph->instanceGroups[groupIndex].temporaryConstants.useAddAggregation.GetUnsafePtr();
                        break;
                }

                int  index = port.index;
                bool add   = bitArray[index >> 5].IsSet(index & 0x1f);
                if (add)
                    array[index] += result;
                else
                    array[index] *= result;
            }
        }

        public void SetJournalCurveBaseIndex(int index)
        {
        }

        public void SetJournalCurveLocalIndex(int index)
        {
        }

        public void SetPortIndex(int index)
        {
        }
    }
}

