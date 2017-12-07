using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections;

namespace PR2_Level_Generator
{
	public class FastBlockGrid : IBlockGrid
	{
		private List<List<Block>> blocks = new List<List<Block>>();
		private int xStart;
		private List<int> yStart;

		public Block FirstBlock { get; private set; }
		private Block last;

		private void CreateIndex(int x, int y)
		{
			// if this is for the very first block
			if (blocks.Count == 0 || blocks[0] == null)
			{
				xStart = x;
				blocks = new List<List<Block>>();
				yStart = new List<int>();
			}

			// Create x index.
			int bX = x - xStart;
			if (bX < 0) // Need to insert lists at beginning of list.
			{
				//blocks.InsertRange(0, new List<Block>[-bX]);
				//yStart.InsertRange(0, new int[-bX]);
				blocks.InsertRange(0, new NullCollection<List<Block>>(-bX));
				yStart.InsertRange(0, new ZeroCollection(-bX));
				xStart = x;
				bX = x - xStart;
			}
			else if (bX > blocks.Count - 1) // at end of list
			{
				//for (int i = blocks.Count; i <= bX; i++)
				//{
				//	blocks.Add(null);
				//	yStart.Add(0);
				//}
				yStart.AddRange(new ZeroCollection(bX - yStart.Count + 1));
				blocks.AddRange(new NullCollection<List<Block>>(bX - blocks.Count + 1));
			}
			if (blocks[bX] == null || blocks[bX][0].X != x) // create new list for this X location
			{
				blocks[bX] = new List<Block>();
				yStart[bX] = y;
			}

			// create y index
			int bY = y - yStart[bX];
			if (bY < 0)
			{
				//blocks[bX].InsertRange(0, new Block[-bY]);
				blocks[bX].InsertRange(0, new NullCollection<Block>(-bY));
				yStart[bX] = y;
			}
			else if (bY > blocks[bX].Count - 1)
			{
				//for (int i = blocks[bX].Count; i <= bY; i++)
				//	blocks[bX].Add(null);
				blocks[bX].AddRange(new NullCollection<Block>(bY - blocks[bX].Count + 1));
			}
		}
		private bool BlockExists(int x, int y)
		{
			x -= xStart;
			if (x < 0 || x >= blocks.Count || blocks[x] == null)
				return false;

			y -= yStart[x];
			if (y < 0 || y >= blocks[x].Count || blocks[x][y] == null)
				return false;

			Block b = blocks[x][y];
			return b.X == x + xStart && b.Y == y + yStart[x];
		}

		public Block PlaceBlock(int x, int y, int t)
		{
			CreateIndex(x, y);

			Block ret = new Block() { X = x, Y = y, T = t };
			if (FirstBlock == null)
				FirstBlock = ret;
			ret.previous = last;
			if (last != null)
				ret.previous.next = ret;
			last = ret;

			x -= xStart;
			y -= yStart[x];
			blocks[x][y] = ret;
			return ret;
		}

		public Block GetBlock(int x, int y)
		{
			if (!BlockExists(x, y))
				return new Block() { X = x, Y = y, T = BlockID.BLANK };

			x -= xStart;
			y -= yStart[x];
			return blocks[x][y];
		}

		public void DeleteBlock(int x, int y)
		{
			Block b = GetBlock(x, y);
			if (b.T == BlockID.BLANK)
				return;

			if (b.previous != null)
				b.previous.next = b.next;
			if (b.next != null)
				b.next.previous = b.previous;

			// clean up blocks lists
			x -= xStart;
			y -= yStart[x];
			RemoveIndex(x, y);
		}
		private void RemoveIndex(int x, int y)
		{
			if (blocks[x].Count == 1)
			{
				blocks[x] = null;
				if (x == 0)
				{
					int toRemove = 1;
					while (blocks[toRemove] == null)
						toRemove++;
					blocks.RemoveRange(0, toRemove);
					yStart.RemoveRange(0, toRemove);
					xStart -= toRemove;
				}
				else if (x == blocks.Count - 1)
				{
					int removeFrom = blocks.Count - 1;
					while (blocks[removeFrom - 1] == null)
						removeFrom--;
					blocks.RemoveRange(removeFrom, blocks.Count - removeFrom);
					yStart.RemoveRange(removeFrom, yStart.Count - removeFrom);
				}
			}
			else
			{
				blocks[x][y] = null;
				if (y == 0)
				{
					int toRemove = 1;
					while (blocks[x][toRemove] == null)
						toRemove++;
					blocks[x].RemoveRange(0, toRemove);
					yStart[x] -= toRemove;
				}
				else if (y == blocks[x].Count - 1)
				{
					int removeFrom = blocks[x].Count - 1;
					while (blocks[x][removeFrom - 1] == null)
						removeFrom--;
					blocks[x].RemoveRange(removeFrom, blocks[x].Count - removeFrom);
				}
			}
		}

		public bool MoveBlock(int x, int y, int moveX, int moveY)
		{
			Block b = GetBlock(x, y);
			if (b.T == BlockID.BLANK)
				return false;

			Block destination = GetBlock(x + moveX, y + moveY);
			if (destination.T == BlockID.Push)
			{
				if (!MoveBlock(x + moveX, y + moveY, moveX, moveY))
					return false;
			}
			else if (destination.T != BlockID.BLANK)
				return false;

			RemoveIndex(b.X - xStart, b.Y - yStart[b.X - xStart]);
			b.X += moveX;
			b.Y += moveY;
			CreateIndex(b.X, b.Y);
			blocks[b.X - xStart][b.Y - yStart[b.X - xStart]] = b;

			return true;
		}

		private class NullCollection<T> : ICollection<T> where T : class
		{
			public int Count { get; private set; }

			public bool IsReadOnly => true;

			public NullCollection(int size)
			{
				Count = size;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public IEnumerator<T> GetEnumerator()
			{
				return new NullEnumerator<T>(Count);
			}

			public void Add(T item)
			{
				Count++;
			}

			public void Clear()
			{
				throw new NotImplementedException();
			}

			public bool Contains(T item)
			{
				return item == null;
			}

			public void CopyTo(T[] array, int arrayIndex)
			{
				//int end = arrayIndex + Count;
				//for (int i = arrayIndex; i < end; i++)
				//	array[i] = null;
			}

			public bool Remove(T item)
			{
				if (Count > 1)
				{
					Count--;
					return true;
				}
				else
					return false;
			}

			class NullEnumerator<T2> : IEnumerator<T2> where T2 : class
			{
				private int position = -1;
				private int size;

				public NullEnumerator(int size)
				{
					this.size = size;
				}

				public T2 Current => null;

				object IEnumerator.Current => null;

				public void Dispose() { }

				public bool MoveNext()
				{
					position++;
					return position < size;
				}

				public void Reset()
				{
					position = -1;
				}
			}
		}

		private class ZeroCollection : ICollection<int>
		{
			public int Count { get; private set; }

			public bool IsReadOnly => true;

			public ZeroCollection(int size)
			{
				Count = size;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public IEnumerator<int> GetEnumerator()
			{
				return new ZeroEnumerator(Count);
			}

			public void Add(int item)
			{
				Count++;
			}

			public void Clear()
			{
				throw new NotImplementedException();
			}

			public bool Contains(int item)
			{
				return item == 0;
			}

			public void CopyTo(int[] array, int arrayIndex)
			{
				//int end = arrayIndex + Count;
				//for (int i = arrayIndex; i < end; i++)
				//	array[i] = 0;
			}

			public bool Remove(int item)
			{
				if (Count > 1)
				{
					Count--;
					return true;
				}
				else
					return false;
			}

			class ZeroEnumerator : IEnumerator<int>
			{
				private int position = -1;
				private int size;

				public ZeroEnumerator(int size)
				{
					this.size = size;
				}

				public int Current => 0;

				object IEnumerator.Current => 0;

				public void Dispose() { }

				public bool MoveNext()
				{
					position++;
					return position < size;
				}

				public void Reset()
				{
					position = -1;
				}
			}
		}
	}
}
