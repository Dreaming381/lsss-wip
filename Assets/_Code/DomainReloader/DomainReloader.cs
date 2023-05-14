using Unity.Collections;

namespace Lsss.DomainReloader
{
    struct Version
    {
        const int kVersion = 1;

        public int capture;
        public Version(bool _)
        {
            capture = kVersion;
        }
    }
}

