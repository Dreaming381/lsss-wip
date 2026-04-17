using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Myri
{
    internal struct ReadControlMessage
    {
        public UnsafeList<RealtimeToControlMessage> messages;
    }

    internal struct WriteControlMessage
    {
        // Note: Pipe transfers ownership because the IControl can outlive the owner,
        // so we make the controller responsible for disposal.
        public ControlToRealtimeMessage message;
    }

    internal struct ControlToRealtimeMessage
    {
        public int                  commandBufferId;
        public int                  retiredFeedbackId;
        public UnsafeList<MegaPipe> commandPipeList;
    }

    internal struct RealtimeToControlMessage
    {
        public int      feedbackBufferId;
        public int      retiredCommandId;
        public MegaPipe feedbackPipe;
    }
}

