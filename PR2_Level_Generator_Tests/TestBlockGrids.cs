using System;
using Xunit;

using PR2_Level_Generator;

namespace PR2_Level_Generator_Tests
{
	public class TestBlockGrids
	{
		[Theory]
		[InlineData(typeof(FastBlockGrid))]
		[InlineData(typeof(LowMemoryBlockGrid))]
		public void TestPlaceAndGet(Type gridType)
		{
			// Place the blocks
			IBlockGrid grid = Activator.CreateInstance(gridType) as IBlockGrid;
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

		[Theory]
		[InlineData(typeof(FastBlockGrid))]
		[InlineData(typeof(LowMemoryBlockGrid))]
		public void TestDelete(Type gridType)
		{
			// Place some blocks
			IBlockGrid grid = Activator.CreateInstance(gridType) as IBlockGrid;
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
    }
}
