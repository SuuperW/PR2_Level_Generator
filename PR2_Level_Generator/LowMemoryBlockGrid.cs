using System;
using System.Collections.Generic;
using System.Text;

namespace PR2_Level_Generator
{
	public class LowMemoryBlockGrid : IBlockGrid
	{
		SortedList<int, SortedList<int, Block>> blocks = new SortedList<int, SortedList<int, Block>>();

		public Block FirstBlock { get; private set; }
		private Block lastBlock;

		public Block PlaceBlock(int x, int y, int t)
		{
			Block ret = new Block() { X = x, Y = y, T = t };
			if (FirstBlock == null)
				FirstBlock = ret;
			ret.previous = lastBlock;
			if (lastBlock != null)
				ret.previous.next = ret;
			lastBlock = ret;

			SortedList<int, Block> list = blocks.GetValueOrDefault(x);
			if (list == null)
			{
				blocks[x] = new SortedList<int, Block>();
				list = blocks[x];
			}
			if (!list.ContainsKey(y))
				list[y] = ret;

			return ret;
		}

		public Block GetBlock(int x, int y)
		{
			SortedList<int, Block> list = blocks.GetValueOrDefault(x);
			Block ret = null;
			if (list != null)
				ret = list.GetValueOrDefault(y);

			if (ret == null)
				ret = new Block() { X = x, Y = y, T = BlockID.BLANK };

			return ret;
		}

		public void DeleteBlock(int x, int y)
		{
			SortedList<int, Block> list = blocks.GetValueOrDefault(x);
			if (list != null)
			{
				Block b = list.GetValueOrDefault(y);
				if (b != null)
				{
					if (list.Count == 1)
						blocks.Remove(x);
					else
						list.Remove(y);

					if (b.previous != null)
						b.previous.next = b.next;
					else if (b == FirstBlock)
						FirstBlock = b.next;
					if (b.next != null)
						b.next.previous = b.previous;
					else if (b == lastBlock)
						lastBlock = b.previous;
				}
			}
		}
	}
}
