using Unity.Entities;

namespace Latios.Myri.Authoring
{
    public static class ListenerProfileBlobberAPIExtensions
    {
        public static BlobAssetReference<ListenerProfileBlob> BuildAndRegisterListenerProfileBlob<T>(this IBaker baker, T builder) where T : IListenerProfileBuilder
        {
            var context = new ListenerProfileBuildContext();
            context.Initialize();
            builder.BuildProfile(ref context);
            var blob = context.ComputeBlobAndDispose();
            baker.AddBlobAsset(ref blob, out _);
            return blob;
        }
    }
}

