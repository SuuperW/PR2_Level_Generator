using System;
using Xunit;

using PR2_Level_Generator;

namespace PR2_Level_Generator_Tests
{
	public class TestFastBlockGrid
	{
		[Fact]
		public void TestPlaceAndGet()
		{
			// Place the blocks
			FastBlockGrid grid = new FastBlockGrid();
			grid.PlaceBlock(2, 5, 0);
			grid.PlaceBlock(2, 8, 1);
			grid.PlaceBlock(2, -5, 2);
			grid.PlaceBlock(-24, 59, 3);

			// Assert that those blocks are in place
			Assert.Equal(0, grid.GetBlock(2, 5).T);
			Assert.Equal(1, grid.GetBlock(2, 8).T);
			Assert.Equal(2, grid.GetBlock(2, -5).T);
			Assert.Equal(3, grid.GetBlock(-24, 59).T);

			// Assert that empty spaces are still empty
			Assert.Equal(BlockID.BLANK, grid.GetBlock(0, 0).T);
			Assert.Equal(BlockID.BLANK, grid.GetBlock(2, 7).T);
			Assert.Equal(BlockID.BLANK, grid.GetBlock(-24, 60).T);
		}

		[Fact]
		public void TestDelete()
		{
			// Place some blocks
			FastBlockGrid grid = new FastBlockGrid();
			grid.PlaceBlock(2, 5, 0);
			grid.PlaceBlock(2, 8, 1);
			grid.PlaceBlock(4, 0, 2);

			// Delete and then assert that those spaces are blank
			grid.DeleteBlock(2, 8);
			Assert.Equal(BlockID.BLANK, grid.GetBlock(2, 8).T);
			grid.DeleteBlock(2, 5);
			Assert.Equal(BlockID.BLANK, grid.GetBlock(2, 5).T);
			grid.DeleteBlock(4, 0);
			Assert.Equal(BlockID.BLANK, grid.GetBlock(4, 0).T);
		}

		[Fact]
		public void TestMapLoad()
		{
			MapLE map = new MapLE(typeof(FastBlockGrid));

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
			MapLE map = new MapLE(typeof(FastBlockGrid));
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
