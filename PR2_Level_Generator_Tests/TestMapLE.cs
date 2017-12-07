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
			Assert.Equal(0, map.CowboyChance);

			Assert.Empty(map.avItems);
			Assert.False(map.HasPassword);
			Assert.False(map.Published);

			Assert.Equal(GameModes.RACE, map.GameMode);

			Assert.Equal("", map.GetSetting("title"));
			Assert.Equal("", map.GetSetting("credits"));
		}
	}
}
