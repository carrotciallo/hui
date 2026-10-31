using System;
using NUnit.Framework;
using UnityEngine;

public sealed class RunStateTests
{
    RunState state;

    [SetUp]
    public void SetUp() => state = new RunState();

    [Test]
    public void KeysAccumulateAndConsume()
    {
        state.AddKey();
        state.AddKey();
        Assert.AreEqual(2, state.Keys);

        Assert.IsTrue(state.TryConsumeKey());
        Assert.IsTrue(state.TryConsumeKey());
        Assert.IsFalse(state.TryConsumeKey());
        Assert.AreEqual(0, state.Keys);
    }

    [Test]
    public void LightingPortalIsIdempotent()
    {
        Assert.IsTrue(state.LightPortal(2));
        Assert.IsFalse(state.LightPortal(2));
        Assert.AreEqual(1, state.LitPortalCount);
        Assert.IsTrue(state.IsPortalLit(2));
        Assert.IsFalse(state.IsPortalLit(3));
    }

    [Test]
    public void BMassDefaultsToOneAndNeverGoesNegative()
    {
        Assert.AreEqual(1, state.BMass);

        state.SetBMass(3);
        Assert.AreEqual(3, state.BMass);

        state.SetBMass(-1);
        Assert.AreEqual(0, state.BMass);
    }

    [Test]
    public void RespawnSlotsAreIndependent()
    {
        state.SetRespawn(0, new Vector2(1f, 2f));
        state.SetRespawn(1, new Vector2(3f, 4f));

        Assert.IsTrue(state.TryGetRespawn(0, out var first));
        Assert.IsTrue(state.TryGetRespawn(1, out var second));
        Assert.AreEqual(new Vector2(1f, 2f), first);
        Assert.AreEqual(new Vector2(3f, 4f), second);

        state.SetRespawn(0, new Vector2(5f, 6f));

        Assert.IsTrue(state.TryGetRespawn(0, out var moved));
        Assert.AreEqual(new Vector2(5f, 6f), moved);
        Assert.IsTrue(state.TryGetRespawn(1, out var untouched));
        Assert.AreEqual(new Vector2(3f, 4f), untouched);
    }

    [Test]
    public void MissingRespawnReturnsFalse()
    {
        Assert.IsFalse(state.TryGetRespawn(0, out _));
        Assert.IsFalse(state.TryGetRespawn(1, out _));
    }

    [Test]
    public void InvalidSlotIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetRespawn(2, Vector2.zero));
        Assert.IsFalse(state.TryGetRespawn(-1, out _));
    }

    [Test]
    public void ResetClearsProgress()
    {
        state.AddKey();
        state.LightPortal(1);
        state.SetBMass(4);
        state.SetRespawn(0, Vector2.one);
        state.SetLastRegion(7);

        state.Reset();

        Assert.AreEqual(0, state.Keys);
        Assert.AreEqual(0, state.LitPortalCount);
        Assert.AreEqual(1, state.BMass);
        Assert.AreEqual(0, state.LastRegion);
        Assert.IsFalse(state.TryGetRespawn(0, out _));
    }
}
