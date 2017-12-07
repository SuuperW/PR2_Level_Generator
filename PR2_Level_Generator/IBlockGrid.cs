using System;
using System.Collections.Generic;
using System.Text;

namespace PR2_Level_Generator
{
	public interface IBlockGrid
	{
		Block PlaceBlock(int x, int y, int t);
		Block GetBlock(int x, int y);

		void DeleteBlock(int x, int y);

		Block FirstBlock { get; }
	}
}
