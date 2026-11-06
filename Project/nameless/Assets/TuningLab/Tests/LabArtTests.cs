using NUnit.Framework;
using UnityEngine;

public sealed class LabArtTests
{
    [Test]
    public void ReapplyingSkinPreservesDoorCollisionAndTracksOpenResetAndVisibility()
    {
        var go = new GameObject("ArtDoorTest");
        var playerGo = new GameObject("KeyCarrier");
        var keyGo = new GameObject("TestKey");
        try
        {
            var door = go.AddComponent<LabDoor>();
            var source = go.GetComponent<SpriteRenderer>();
            var collider = go.GetComponent<BoxCollider2D>();
            var size = collider.size;
            door.ApplyShape();
            door.ApplyShape();
            Assert.AreEqual(1, go.GetComponents<LabArtPresenter>().Length);
            var art = go.GetComponent<LabArtPresenter>();
            var display = go.transform.Find("SelectedArt").GetComponent<SpriteRenderer>();
            Assert.AreEqual("gate_locked_close", display.sprite.name);
            Assert.AreEqual(size, collider.size);
            var player = playerGo.AddComponent<LabPlayer>();
            Assert.IsTrue(player.TryCollectKey(keyGo.AddComponent<LabKey>()));
            Assert.IsTrue(door.TryOpen(player));
            art.Refresh();
            Assert.AreEqual("gate_locked_open", display.sprite.name);
            Assert.IsFalse(collider.enabled);
            door.ResetDoor();
            art.Refresh();
            Assert.IsTrue(collider.enabled);
            Assert.AreEqual("gate_locked_close", display.sprite.name);
            source.color = new Color(1, 1, 1, 0.2f);
            source.enabled = false;
            art.Refresh();
            Assert.IsFalse(display.enabled);
            Assert.AreEqual(0.2f, display.color.a);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(keyGo);
        }
    }

    [Test]
    public void MergedTerrainTilesWithoutChangingWorldBoundsOrCollisionScale()
    {
        var go = new GameObject("ArtTerrainTest");
        try
        {
            go.transform.localScale = new Vector3(7, 3, 1);
            var source = go.AddComponent<SpriteRenderer>();
            source.sprite = TuningLab.WhiteSprite;
            var before = source.bounds.size;
            LabArt.Apply(source, "terrain", true);
            var display = go.transform.Find("SelectedArt").GetComponent<SpriteRenderer>();
            Assert.AreEqual(before, display.bounds.size);
            Assert.AreEqual(new Vector2(7, 3), display.size);
            Assert.AreEqual(new Vector3(7, 3, 1), go.transform.localScale);
            Assert.AreEqual(16, display.sprite.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, display.sprite.texture.filterMode);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
