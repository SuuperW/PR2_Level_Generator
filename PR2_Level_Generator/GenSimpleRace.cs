using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace PR2_Level_Generator
{
    public class GenSimpleRace : ILevelGenerator
    {
        public GenSimpleRace()
        {
			parameters = new SortedDictionary<string, double>();
			parameters.Add("length", 160);
			parameters.Add("height", 9);
			parameters.Add("min_fill", 2);
			parameters.Add("max_fill", 5);
			parameters.Add("easy", 0);
			parameters.Add("nice_ending", 0);
			parameters.Add("block_type", 0);
			parameters.Add("border_type", -1);
			parameters.Add("floor_thickness", 1);
 			parameters.Add("seed", 0);
            Map = new MapLE();
        }

		SortedDictionary<string, double> parameters;
		public string[] GetParamNames()
		{
			string[] ret = new string[parameters.Count];
			parameters.Keys.CopyTo(ret, 0);
			return ret;
		}
		public double GetParamValue(string name)
		{
			return parameters[name];
		}
		public void SetParamValue(string name, double value)
		{
			parameters[name] = value;
		}

        public int Length
        {
            get { return (int)parameters["length"]; }
            set { parameters["length"] = value; }
        }
        public int Height
        {
            get { return (int)parameters["height"]; }
            set { parameters["height"] = value; }
        }
        public double Min_Fill
        {
            get { return parameters["min_fill"]; }
            set { parameters["min_fill"] = value; }
        }
        public double Max_Fill
        {
            get { return parameters["max_fill"]; }
            set { parameters["max_fill"] = value; }
        }
        public int Easy
        {
            get { return (int)parameters["easy"]; }
            set { parameters["easy"] = value; }
        }
        public int Nice_Ending
        {
            get { return (int)parameters["nice_ending"]; }
            set { parameters["nice_ending"] = value; }
        }
        public int Block_Type
        {
            get { return (int)parameters["block_type"]; }
            set { parameters["block_type"] = value; }
        }
        public int Border_Type
        {
            get { return (int)parameters["border_type"]; }
            set { parameters["border_type"] = value; }
        }
		public int Floor_Thickness
		{
            get { return (int)parameters["floor_thickness"]; }
            set { parameters["floor_thickness"] = value; }
		}
        public int Seed
        {
            get { return (int)parameters["seed"]; }
            set { parameters["seed"] = value; }
        }

        public MapLE Map { get; private set; }
        private const int BLOCK_UNDER = 101;
        private const int BLOCK_BLOCKED = 102;
        private const int BLOCK_FINISHABLE = 103;

        Random R;

        System.Diagnostics.Stopwatch t = new System.Diagnostics.Stopwatch();
        public int LastSeed { get; private set; }
        public void GenerateMap()
        {
            t.Restart();

            Map.ClearBlocks();
			Map.BGC = 0xbbbbdd;
            // Starting points; floor; ceiling; walls; finish.
            PlaceBordersAndEnds();

            int rSeed = Seed;
            if (rSeed == 0)
                rSeed = Environment.TickCount;
            R = new Random(rSeed);
            LastSeed = rSeed;

            if (Max_Fill > Height - 3)
                Max_Fill = Height - 3;
            if (Min_Fill > Height - 3)
                Min_Fill = Height - 3;

            // Loop through the spots and place randoms!
            for (int i = 2; i < Length - 2; i++)
            {
                int x = 50 + i;

                // Which spaces can be entered?
                List<int> openFromLeft = GetOpenSpots(x);
                PlaceFillBlocks(x, openFromLeft);
                PlaceMetaBlocks(x, openFromLeft);
            }

            // Clean up meta blocks
            Map.ClearType(BLOCK_BLOCKED);
            Map.ClearType(BLOCK_UNDER);

            if (Easy != -1 && new Block() { T = Block_Type }.IsSolid())
                BlockTraps();

            t.Stop();
            Console.WriteLine("Map Generated. [" + (t.ElapsedMilliseconds / 1000.0) + "s]");
        }

        private void PlaceBordersAndEnds()
        {
            Map.AddBlock(51, 48 + Height, BlockID.P1);
            Map.AddBlock(51, 48 + Height, BlockID.P2);
            Map.AddBlock(51, 48 + Height, BlockID.P3);
            Map.AddBlock(51, 48 + Height, BlockID.P4);

            int border = Border_Type == -1 ? Block_Type : Border_Type;

			// Floor 
			for (int iT = 0; iT < Floor_Thickness; iT++)
			{
				for (int i = 0; i < Length; i++)
				{
					Map.AddBlock(50 + i, 50 + Height - 1 + iT, border);
					Map.AddBlock(50 + i, 50 - iT, border);
				}
			}
            Map.AddBlock(50 + Length - 2, 50, BlockID.Finish);

			int endingBlockID = Nice_Ending != 0 ? BlockID.Up : border;
			for (int i = 1; i < Height - 1; i++)
			{
				Map.AddBlock(50, 50 + i, border);
                    Map.AddBlock(50 + Length - 1, 50 + i, BlockID.Up);
			}

            // Top row is inaccessible.
            if (Map.GetBlock(51, 50).IsSolid())
                Map.AddBlock(51, 51, BLOCK_BLOCKED);
        }
        private List<int> GetOpenSpots(int x)
        {
            List<int> ret = new List<int>();
            for (int iY = 1; iY < Height - 1; iY++)
            {
                int y = iY + 50;
                Block left = Map.GetBlock(x - 1, y);
                // Is this spot reachable?
                if (!left.IsSolid() && left.T != BLOCK_BLOCKED)
                { //    easy reach               can stand at left column                 head glitch space is accessible
                    if (left.T != BLOCK_UNDER || (Map.GetBlock(x - 1, y + 1).IsSolid() || !Map.GetBlock(x - 2, y + 1).IsSolid()))
                        ret.Add(y);
                }
            }

            return ret;
        }
        private void PlaceFillBlocks(int x, List<int> currentlyOpen)
        {
            int leaveOpen = currentlyOpen[R.Next(0, currentlyOpen.Count)];
			int fillCount = (int)Math.Round(R.NextDouble() * (Max_Fill - Min_Fill) + Min_Fill);
            while (fillCount > 0)
            {
                int y = 51 + R.Next(0, Height - 2);
                if (!Map.GetBlock(x, y).IsSolid()) // Space is not already used.
                {
                    bool place = true;
                    if (y == leaveOpen)
                        place = false;
                    else
                        currentlyOpen.Remove(y);

                    if (place)
                    {
                        Map.AddBlock(x, y, Block_Type);
                        fillCount--;
                    }
                }
            }
        }
        private void PlaceMetaBlocks(int x, List<int> openFromLeft)
        {
            // Fill with BLOCKED to cover any spaces not accessible from the open spaces
            for (int iY = 1; iY < Height - 1; iY++)
            {
                if (!Map.BlockExists(x, 50 + iY))
                    Map.AddBlock(x, 50 + iY, BLOCK_BLOCKED);
            }

            // Delete any BLOCKED blocks accessible from the open spaces, and possibly replace some with UNDER
            for (int iOpen = 0; iOpen < openFromLeft.Count; iOpen++)
            {
                int y = openFromLeft[iOpen];
                // Fall to real block (or floor)
                Block b = Map.GetBlock(x, y + 1);
                while (b.T == BLOCK_BLOCKED && y < 50 + Height - 2)
                {
                    y++;
                    b = Map.GetBlock(x, y + 1);
                }
                Map.DeleteBlock(x, y); // can stand above the found solid block


                // Go up. !ASSUME! infinite jumping ability.
                b = Map.GetBlock(x, y - 1);
                while (b.T == BLOCK_BLOCKED && y > 51)
                {
                    Map.DeleteBlock(x, y);
                    y--;
                    b = Map.GetBlock(x, y - 1);
                }

                // b is now the ceiling for this space - should it be converted to an UNDER?
                if (Map.GetBlock(x, y).T == BLOCK_BLOCKED)
                {
                    if (CheckBlockedToUnder(x, y))
                        Map.ReplaceBlock(x, y, BLOCK_UNDER);
                }
            }
        }
        /// <summary>
        /// Check if a space should be considered UNDER, instead of BLOCKED.
        /// Assumes above space was already checked to be solid and below to be not solid.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private bool CheckBlockedToUnder(int x, int y)
        {
            if (Easy == 1)
            {
                return false;
            }
            else
            {
                Block left = Map.GetBlock(x - 1, y);
                return !(left.IsSolid() || left.T == BLOCK_BLOCKED || left.T == BLOCK_UNDER);
            }
        }

        private void BlockTraps()
        {
            Map.ReplaceBlock(50 + Length - 2, 52, BLOCK_FINISHABLE);
            
            List<Point> finishableFrom = new List<Point>();
            finishableFrom.Add(new Point(50 + Length - 2, 52));
            for (int i = 0; i < finishableFrom.Count; i++)
            {
                List<Point> newFinishables = GetNewFinishables(finishableFrom[i]);
                for (int iF = 0; iF < newFinishables.Count; iF++)
                {
                    Map.ReplaceBlock(newFinishables[iF].X, newFinishables[iF].Y, BLOCK_FINISHABLE);
                    finishableFrom.Add(newFinishables[iF]);
                }
            }

            // Check if level was determined impossible.
            if (Map.GetBlock(51, 50 + Height - 3).T != BLOCK_FINISHABLE && Map.GetBlock(52, 50 + Height - 2).T != BLOCK_FINISHABLE)
            {
                Map.BGC = 0xbb1100;
                Console.WriteLine("ERROR: Level is not finishable.");
                return;
            }

            // Replace blocked off areas with Block_Type
            for (int iX = 52; iX < 50 + Length - 1; iX++)
            {
                for (int iY = 51; iY < 50 + Height - 1; iY++)
                {
                    if (!Map.BlockExists(iX, iY))
                        Map.AddBlock(iX, iY, Block_Type);
                }
            }

            Map.ClearType(BLOCK_FINISHABLE);
        }
        private List<Point> GetNewFinishables(Point p)
        {
            List<Point> ret = new List<Point>();

            // Can reach p from above?
            Block b = Map.GetBlock(p.X, p.Y - 1);
            if (b.T == 99 || b.T == BLOCK_FINISHABLE)
                ret.Add(new Point(p.X, p.Y - 1));

            // Can reach p from below?
            if (!b.IsSolid())
            {
                b = Map.GetBlock(p.X, p.Y + 1);
                if (b.T == 99 || b.T == BLOCK_FINISHABLE)
                    ret.Add(new Point(p.X, p.Y + 1));
            }

            // Can reach p from the left?
            b = Map.GetBlock(p.X - 1, p.Y);
            Block b2;
            if (b.T == 99 || b.T == BLOCK_FINISHABLE)
            {
                b2 = Map.GetBlock(p.X - 1, p.Y - 1);
                if (b2.T == 99 || b2.T == BLOCK_FINISHABLE) // left-up is blank
                    ret.Add(new Point(p.X - 1, p.Y));
                else
                {
                    b2 = Map.GetBlock(p.X - 1, p.Y + 1);
                    if (b2.IsSolid()) // can stand to left
                        ret.Add(new Point(p.X - 1, p.Y));
                    else if (Easy != 1)
                    {
                        b2 = Map.GetBlock(p.X - 2, p.Y);
                        if (b2.T == 99 || b2.T == BLOCK_FINISHABLE) // two spaces left is open
                        {
                            b2 = Map.GetBlock(p.X - 2, p.Y - 1);
                            if (b2.T == 99 || b2.T == BLOCK_FINISHABLE) // two spaces left and one up is open
                                ret.Add(new Point(p.X - 1, p.Y));
                        }
                    }
                }
            }

            // Can reach p from the right?
            b = Map.GetBlock(p.X + 1, p.Y);
            if (b.T == 99 || b.T == BLOCK_FINISHABLE)
            {
                b2 = Map.GetBlock(p.X + 1, p.Y - 1);
                if (b2.T == 99 || b2.T == BLOCK_FINISHABLE)
                    ret.Add(new Point(p.X + 1, p.Y));
                else
                {
                    b2 = Map.GetBlock(p.X + 1, p.Y + 1);
                    if (b2.IsSolid())
                        ret.Add(new Point(p.X + 1, p.Y));
                    else if (Easy != 1)
                    {
                        b2 = Map.GetBlock(p.X + 2, p.Y);
                        if (b2.T == 99 || b2.T == BLOCK_FINISHABLE)
                        {
                            b2 = Map.GetBlock(p.X + 2, p.Y - 1);
                            if (b2.T == 99 || b2.T == BLOCK_FINISHABLE)
                                ret.Add(new Point(p.X + 1, p.Y));
                        }
                    }
                }
            }

			for (int i = 0; i < ret.Count; i++)
			{
				if (Map.GetBlock(ret[i].X, ret[i].Y).T == BLOCK_FINISHABLE)
				{
					ret.RemoveAt(i);
					i--;
				}
			}

            return ret;
        }

		public string GetSaveString()
		{
			StringBuilder ret = new StringBuilder();
			ret.Append(this.GetType().ToString());
			foreach (KeyValuePair<string, double> kvp in parameters)
			{
				ret.Append("\n" + kvp.Key + ":" + kvp.Value);
			}

			return ret.ToString();
		}
    }
}
