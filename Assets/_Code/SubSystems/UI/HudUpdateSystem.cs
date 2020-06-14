using System.Text;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public class HudUpdateSystem : SubSystem
    {
        StringBuilder m_healthBuilder      = new StringBuilder();
        StringBuilder m_bulletCountBuilder = new StringBuilder();
        StringBuilder m_factionsBuilder    = new StringBuilder();

        EntityQuery m_shipsQuery;

        protected override void OnCreate()
        {
            m_shipsQuery = Fluent.WithAll<ShipTag>(true).WithAll<FactionMember>(true).IncludeDisabled().Build();
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

            bool playerFound = false;
            Entities.WithAll<PlayerTag>().ForEach((in ShipHealth health, in ShipReloadTime bullets, in ShipBoostTank boost, in ShipSpeedStats stats) =>
            {
                playerFound = true;

                m_healthBuilder.Clear();
                m_healthBuilder.Append(health.health);
                hud.health.SetText(m_healthBuilder);

                m_bulletCountBuilder.Clear();
                m_bulletCountBuilder.Append(bullets.bulletsRemaining);
                hud.bulletCount.SetText(m_bulletCountBuilder);

                float3 localScale       = hud.boostBar.localScale;
                localScale.y            = boost.boost / stats.boostCapacity;
                hud.boostBar.localScale = localScale;
            }).WithoutBurst().Run();

            if (playerFound)
            {
                hud.blackFadeControl = math.saturate(hud.blackFadeControl - math.rcp(hud.blackFadeOutTime) * Time.DeltaTime);
            }
            else
            {
                var queues = sceneGlobalEntity.GetCollectionComponent<SpawnQueues>(true);
                CompleteDependency();
                if (queues.playerQueue.Count == 0)
                {
                    //Player has been dequeued and will spawn soon. Fade to black.
                    hud.blackFadeControl = math.saturate(hud.blackFadeControl + math.rcp(hud.blackFadeInTime) * Time.DeltaTime);
                }
            }
            hud.blackFade.color = new UnityEngine.Color(0f, 0f, 0f, hud.blackFadeControl);

            m_factionsBuilder.Clear();
            Entities.WithAll<FactionTag>().ForEach((Entity entity, in Faction faction) =>
            {
                var factionMember = new FactionMember { factionEntity = entity };
                m_shipsQuery.SetSharedComponentFilter(factionMember);
                int liveCount = m_shipsQuery.CalculateEntityCount();

                //Todo: StringBuilder extension for FixedString?
                foreach (var c in faction.name)
                {
                    m_factionsBuilder.Append((char)c.value);
                }

                m_factionsBuilder.Append('\t');
                m_factionsBuilder.Append(faction.remainingReinforcements + liveCount);
                m_factionsBuilder.Append('\n');
            }).WithoutBurst().Run();
            hud.factions.SetText(m_factionsBuilder);
        }
    }
}

