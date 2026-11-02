using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// World.unity 是编辑器生成的产物，地板完全靠 Tilemap。Tile 资源没生效时 SetTile 会静默失败，
/// 产物变成一张没有地面的世界，玩家一出生就往 FallFloor 掉并无限重生。
public sealed class WorldSceneTests
{
    const string ScenePath = "Assets/Scenes/World.unity";

    [Test]
    public void WorldSceneHasPaintedGround()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            int tileCount = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Tilemap>(true))
                .Sum(tilemap => tilemap.GetUsedTilesCount());

            Assert.Greater(tileCount, 0, $"{ScenePath} 里所有 Tilemap 都是空的：重新跑 GameJam → Build World Scene");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
