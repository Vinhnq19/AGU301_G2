using Assets._Game.Scripts.Data;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Networking;
using NUnit.Framework;
using UnityEngine;

namespace DungeonBuilder.Tests
{
    public sealed class ResourceStoreTests
    {
        [Test]
        public void Constructor_ContainsEveryResourceExceptMax()
        {
            var store = new ResourceStore();

            Assert.That(store.GetSnapshot().Keys, Is.EquivalentTo(ResourceTypeUtility.All));
            Assert.That(store.GetSnapshot().ContainsKey(ResourceType.MAX), Is.False);
            Assert.That(store.GetSnapshot().ContainsKey(ResourceType.Coin), Is.True);
            Assert.That(store.GetSnapshot().ContainsKey(ResourceType.Tokken), Is.True);
        }

        [Test]
        public void Crud_UpdatesAndResetsValidResource()
        {
            var store = new ResourceStore();

            Assert.That(store.TrySet(ResourceType.Wood, 10), Is.True);
            Assert.That(store.TryAdd(ResourceType.Wood, 5), Is.True);
            Assert.That(store.GetAmount(ResourceType.Wood), Is.EqualTo(15));
            Assert.That(store.TryReset(ResourceType.Wood), Is.True);
            Assert.That(store.GetAmount(ResourceType.Wood), Is.Zero);
        }

        [Test]
        public void Mutations_RejectInvalidNegativeAndOverflowValues()
        {
            var store = new ResourceStore();

            Assert.That(store.TrySet(ResourceType.MAX, 1), Is.False);
            Assert.That(store.TrySet(ResourceType.Wood, -1), Is.False);
            Assert.That(store.TryAdd(ResourceType.Wood, -1), Is.False);
            Assert.That(store.TrySet(ResourceType.Wood, int.MaxValue), Is.True);
            Assert.That(store.TryAdd(ResourceType.Wood, 1), Is.False);
            Assert.That(store.GetAmount(ResourceType.Wood), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void SharedResourceManager_RejectsMutationWhenNotServer()
        {
            GameObject gameObject = new("ResourceServiceTest");
            try
            {
                SharedResourceManager service = gameObject.AddComponent<SharedResourceManager>();

                Assert.That(service.TrySet(ResourceType.Wood, 10), Is.False);
                Assert.That(service.TryAdd(ResourceType.Wood, 10), Is.False);
                Assert.That(service.TrySpend(new[] { new ResourceCost(ResourceType.Wood, 1) }), Is.False);
                Assert.That(service.TryReset(ResourceType.Wood), Is.False);
                Assert.That(service.TryResetAll(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TrySpend_IsAtomicWhenAnyResourceIsMissing()
        {
            var store = new ResourceStore();
            store.TrySet(ResourceType.Wood, 100);
            store.TrySet(ResourceType.Ore, 40);
            ResourceCost[] costs =
            {
                new(ResourceType.Wood, 100),
                new(ResourceType.Ore, 50)
            };

            Assert.That(store.TrySpend(costs), Is.False);
            Assert.That(store.GetAmount(ResourceType.Wood), Is.EqualTo(100));
            Assert.That(store.GetAmount(ResourceType.Ore), Is.EqualTo(40));
        }

        [Test]
        public void TrySpend_DeductsEveryResourceWhenAffordable()
        {
            var store = new ResourceStore();
            store.TrySet(ResourceType.Wood, 100);
            store.TrySet(ResourceType.Ore, 50);
            ResourceCost[] costs =
            {
                new(ResourceType.Wood, 80),
                new(ResourceType.Ore, 30)
            };

            Assert.That(store.TrySpend(costs), Is.True);
            Assert.That(store.GetAmount(ResourceType.Wood), Is.EqualTo(20));
            Assert.That(store.GetAmount(ResourceType.Ore), Is.EqualTo(20));
        }

        [Test]
        public void TrySpend_AggregatesDuplicateCosts()
        {
            var store = new ResourceStore();
            store.TrySet(ResourceType.Wood, 30);
            ResourceCost[] costs =
            {
                new(ResourceType.Wood, 10),
                new(ResourceType.Wood, 20)
            };

            Assert.That(store.CanAfford(costs), Is.True);
            Assert.That(store.TrySpend(costs), Is.True);
            Assert.That(store.GetAmount(ResourceType.Wood), Is.Zero);
        }
    }
}
