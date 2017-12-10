using System;
using Xunit;

using PR2_Level_Generator;

namespace PR2_Level_Generator_Tests
{
	public class TestMapLE
	{
		[Fact]
		public void TestLoadBlank()
		{
			MapLE map = new MapLE();
			map.LoadLevel("");

			VerifyDefaultMapSettings(map);
		}

		[Fact]
		public void TestReloadBlank()
		{
			MapLE map = new MapLE();
			map.LoadLevel("");
			map.LoadLevel(map.GetData());

			VerifyDefaultMapSettings(map);
		}

		private void VerifyDefaultMapSettings(MapLE map)
		{
			Assert.Equal(-1, map.Song);
			Assert.Equal(0, map.MinimumRank);
			Assert.Equal(0.0, map.Gravity);
			Assert.Equal(0, map.TimeLimit);
			Assert.Equal(5, map.CowboyChance);

			Assert.Empty(map.avItems);
			Assert.False(map.HasPassword);
			Assert.False(map.Published);

			Assert.Equal(GameModes.RACE, map.GameMode);

			Assert.Equal("", map.GetSetting("title"));
			Assert.Equal("", map.GetSetting("credits"));
		}

		[Fact]
		public void TestMapLoad()
		{
			MapLE map = new MapLE();

			// Load some blocks
			map.LoadLevel("data=m3`0`2;5,0;3;1,2;-8;2```````-1&title=testing");
			// Assert those blocks exist
			Assert.Equal(0, map.GetBlock(2, 5).T);
			Assert.Equal(1, map.GetBlock(2, 8).T);
			Assert.Equal(2, map.GetBlock(4, 0).T);

			// Load a blank level
			map.LoadLevel("");
			// Assert that a block is placed at the origin
			Assert.Equal(0, map.GetBlock(0, 0).T);
		}

		[Fact]
		public void TestMapGetData()
		{
			MapLE map = new MapLE();
			// Place some blocks
			map.AddBlock(2, 5, 0);
			map.AddBlock(2, 8, 1);
			map.AddBlock(4, 0, 2);

			// Assert that GetData returns correct block data
			string data = map.GetDataParam(false);
			string blockData = data.Split('`')[2];
			Assert.Equal("2;5,0;3;1,2;-8;2", blockData);
		}
	}
}
