using System.Text;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    public partial class HudUpdateSystem : SubSystem
    {
        struct CachedShipBaseHealth : IComponentData
        {
            public float health;
        }

        StringBuilder m_healthBuilder      = new StringBuilder();
        StringBuilder m_bulletCountBuilder = new StringBuilder();
        StringBuilder m_factionsBuilder    = new StringBuilder();

        EntityQuery m_shipsQuery;

        protected override void OnCreate()
        {
            m_shipsQuery                                                             = Fluent.WithAll<ShipTag>(true).WithAll<FactionMember>(true).IncludeDisabledEntities().Build();
            worldBlackboardEntity.AddComponentData(new CachedShipBaseHealth { health = 0f });
        }

        protected override void OnUpdate()
        {
            Hud hud = null;
            Entities.ForEach((Hud hudRef) =>
            {
                hud = hudRef;
            }).WithoutBurst().Run();

            if (hud == null)
                return;

            bool   playerFound = false;
            float  healthValue = 0f;
            Entity wbe         = worldBlackboardEntity;
            Entities.WithAll<PlayerTag>().ForEach((in ShipHealth health, in ShipBaseHealth baseHealth, in ShipReloadTime bullets, in ShipBoostTank boost, in ShipSpeedStats stats) =>
            {
                playerFound = true;

                healthValue = health.health;
                SystemAPI.SetComponent(wbe,
                                       new CachedShipBaseHealth { health = baseHealth.baseHealth });

                m_bulletCountBuilder.Clear();
                m_bulletCountBuilder.Append(bullets.bulletsRemaining);
                hud.bulletCount.SetText(m_bulletCountBuilder);

                float3 localScale       = hud.boostBar.localScale;
                localScale.y            = boost.boost / stats.boostCapacity;
                hud.boostBar.localScale = localScale;
            }).WithoutBurst().Run();

            m_healthBuilder.Clear();
            FixedString64Bytes healthString = default;
            healthString.Append(healthValue);
            healthString.Append('/');
            healthString.Append(worldBlackboardEntity.GetComponentData<CachedShipBaseHealth>().health);
            m_healthBuilder.Append(in healthString);
            hud.health.SetText(m_healthBuilder);

            if (playerFound)
            {
                hud.blackFadeControl = math.saturate(hud.blackFadeControl - math.rcp(hud.blackFadeOutTime) * SystemAPI.Time.DeltaTime);
            }
            else
            {
                var queues = sceneBlackboardEntity.GetCollectionComponent<SpawnQueues>(true);
                CompleteDependency();
                if (queues.playerQueue.Count == 0)
                {
                    //Player has been dequeued and will spawn soon. Fade to black.
                    hud.blackFadeControl = math.saturate(hud.blackFadeControl + math.rcp(hud.blackFadeInTime) * SystemAPI.Time.DeltaTime);
                }
            }
            hud.blackFade.color = new UnityEngine.Color(0f, 0f, 0f, hud.blackFadeControl);

            //m_factionsBuilder.Clear();
            NativeList<FixedString64Bytes> factionNames  = new NativeList<FixedString64Bytes>(Allocator.TempJob);
            NativeList<int>                factionCounts = new NativeList<int>(Allocator.TempJob);
            Entities.WithAll<FactionTag>().ForEach((Entity entity, in Faction faction) =>
            {
                var factionMember = new FactionMember { factionEntity = entity };
                m_shipsQuery.SetSharedComponentFilter(factionMember);
                factionCounts.Add(m_shipsQuery.CalculateEntityCount() + faction.remainingReinforcements);
                factionNames.Add(faction.name);

                //Todo: StringBuilder extension for FixedString?
                //foreach (var c in faction.name)
                //{
                //m_factionsBuilder.Append((char)c.value);
                //}

                //m_factionsBuilder.Append('\t');
                //m_factionsBuilder.Append(faction.remainingReinforcements + liveCount);
                //m_factionsBuilder.Append('\n');
            }).WithoutBurst().Run();

            m_factionsBuilder.Clear();
            foreach (var n in factionNames)
            {
                m_factionsBuilder.Append(in n);
                m_factionsBuilder.Append('\t');
                m_factionsBuilder.Append('0');
                m_factionsBuilder.Append('\n');
            }
            hud.factions.SetText(m_factionsBuilder);
            var preferredWidth = hud.factions.GetPreferredValues().x;
            var realWidth      = hud.factions.rectTransform.rect.width;
            int ratio          = (int)(100f * preferredWidth / realWidth);
            m_factionsBuilder.Clear();
            for (int i = 0; i < factionNames.Length; i++)
            {
                var n = factionNames[i];
                m_factionsBuilder.Append(in n);
                m_factionsBuilder.Append('<');
                m_factionsBuilder.Append('p');
                m_factionsBuilder.Append('o');
                m_factionsBuilder.Append('s');
                m_factionsBuilder.Append('=');
                m_factionsBuilder.Append(ratio);
                m_factionsBuilder.Append('%');
                m_factionsBuilder.Append('>');
                m_factionsBuilder.Append(factionCounts[i]);
                m_factionsBuilder.Append('\n');
            }
            hud.factions.SetText(m_factionsBuilder);
            factionNames.Dispose();
            factionCounts.Dispose();
        }
    }
}

