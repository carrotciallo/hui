using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class LabPixelArtTests
{
    [Test]
    public void PixelArtParticipatesInSelectionPartitionAndClone()
    {
        var sprite = MakeSprite();
        var texture = sprite.texture;
        try
        {
            var data = new LabLevelData();
            data.partitions.Add(new LabLevelPartition
            {
                name = "A",
                bounds = new RectInt(0, 0, 10, 10)
            });
            Assert.IsTrue(data.AddPixelArt(sprite, new Vector2Int(2, 2), new Vector2Int(2, 2), 3));

            var area = new List<RectInt> { new RectInt(2, 2, 1, 1) };
            var capture = data.CaptureContents(area);
            Assert.AreEqual(1, capture.Count);
            Assert.AreEqual(1, data.MoveCapturedContents(capture, new Vector2Int(1, 1)));
            Assert.AreEqual(new Vector2Int(3, 3), data.pixelArts[0].origin);

            Assert.IsTrue(data.MovePartition(0, new Vector2Int(4, 2), true));
            Assert.AreEqual(new Vector2Int(7, 5), data.pixelArts[0].origin);

            var clone = data.Clone();
            clone.pixelArts[0] = new LabPixelArtSpec(sprite, Vector2Int.zero, Vector2Int.one);
            Assert.AreEqual(new Vector2Int(7, 5), data.pixelArts[0].origin, "Clone 必须是独立列表");
        }
        finally
        {
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void RuntimeBuildScalesSpriteToCellFootprintWithoutCollider()
    {
        var sprite = MakeSprite();
        var texture = sprite.texture;
        var root = new GameObject("PixelArtRuntimeTest");
        try
        {
            var level = root.AddComponent<LabLevel>();
            level.Data.AddPixelArt(sprite, new Vector2Int(2, 3), new Vector2Int(2, 3), 4);

            level.BuildRuntime(root.transform, null);

            var generated = root.transform.Find(LabLevel.GeneratedRootName);
            Assert.IsNotNull(generated);
            var art = generated.Find("PixelArt");
            Assert.IsNotNull(art);
            Assert.AreEqual(new Vector3(3f, 4.5f, 0f), art.position);
            Assert.AreEqual(new Vector3(2f, 3f, 1f), art.localScale);
            Assert.IsNull(art.GetComponent<Collider2D>());
            Assert.AreEqual(sprite, art.GetComponent<SpriteRenderer>().sprite);
            Assert.AreEqual(4, art.GetComponent<SpriteRenderer>().sortingOrder);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    static Sprite MakeSprite()
    {
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var pixels = new Color32[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f),
            TuningLab.CellPixels,
            0,
            SpriteMeshType.FullRect);
    }
}
