using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace PR2_Level_Generator
{
    class GenMaze : ILevelGenerator
    {
        public GenMaze()
        {
			parameters = new SortedDictionary<string, double>();
			parameters.Add("size", 2);
			parameters.Add("branch", 0.9);
			parameters.Add("sq_size", 3);
			parameters.Add("circle", 0);
			parameters.Add("items", 0);
			parameters.Add("happys", 0);
			parameters.Add("vanish", 20);
			parameters.Add("vanish_fill", 33);
			parameters.Add("water", 25);
			parameters.Add("water_fill", 40);
			parameters.Add("move", 20);
			parameters.Add("move_fill", 10);
			parameters.Add("crumble", 20);
			parameters.Add("crumble_fill", 30);
			parameters.Add("brick", 20);
 			parameters.Add("brick_fill", 33);
			parameters.Add("bomb", 5);
			parameters.Add("bomb_fill", 5);
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

        #region "Parameter Properties"
        public int Size
        {
            get { return (int)parameters["size"]; }
            set { parameters["size"] = value; }
        }
        public double Branch
        {
            get { return parameters["branch"]; }
            set { parameters["branch"] = value; }
        }
        public int Sq_Size
        {
            get { return (int)parameters["sq_size"]; }
            set { parameters["sq_size"] = value; }
        }
        public double Circle
        {
            get { return parameters["circle"]; }
            set { parameters["circle"] = value; }
        }
        public double Items
        {
            get { return parameters["items"]; }
            set { parameters["items"] = value; }
        }
        public double Happys
        {
            get { return parameters["happys"]; }
            set { parameters["happys"] = value; }
        }

        // Fill chances
        public double Vanish
        {
            get { return parameters["vanish"]; }
            set { parameters["vanish"] = value; }
        }
        public double Vanish_Fill
        {
            get { return parameters["vanish_fill"]; }
            set { parameters["vanish_fill"] = value; }
        }
        public double Water
        {
            get { return parameters["water"]; }
            set { parameters["water"] = value; }
        }
        public double Water_Fill
        {
            get { return parameters["water_fill"]; }
            set { parameters["water_fill"] = value; }
        }
        public double Move
        {
            get { return parameters["move"]; }
            set { parameters["move"] = value; }
        }
        public double Move_Fill
        {
            get { return parameters["move_fill"]; }
            set { parameters["move_fill"] = value; }
        }
        public double Crumble
        {
            get { return parameters["crumble"]; }
            set { parameters["crumble"] = value; }
        }
        public double Crumble_Fill
        {
            get { return parameters["crumble_fill"]; }
            set { parameters["crumble_fill"] = value; }
        }
        public double Brick
        {
            get { return parameters["brick"]; }
            set { parameters["brick"] = value; }
        }
        public double Brick_Fill
        {
            get { return parameters["brick_fill"]; }
            set { parameters["brick_fill"] = value; }
        }
        public double Bomb
        {
            get { return parameters["bomb"]; }
            set { parameters["bomb"] = value; }
        }
        public double Bomb_Fill
        {
            get { return parameters["bomb_fill"]; }
            set { parameters["bomb_fill"] = value; }
        }

        public int Seed
        {
            get { return (int)parameters["seed"]; }
            set { parameters["seed"] = value; }
        }
        #endregion


        // Starting location
        private Point Start = new Point();
        private Point Finish = new Point();
        private bool[][] roomMade;

        public MapLE Map { get; private set; }
        private string ArtStr;

        Random R;
        public int LastSeed { get; private set; }

		CancellationTokenSource cts;

        public Task<bool> GenerateMap(CancellationTokenSource cts)
        {
			this.cts = cts;

			if (Sq_Size > 1000 || Size > 100000)
			{
				Console.WriteLine("GenMaze will not generate mazes that large!");
				return Task.FromResult(true); // true because it was not cancelled
			}

            int rSeed = Seed;
            if (rSeed == 0)
                rSeed = Environment.TickCount;
            R = new Random(rSeed);
            LastSeed = rSeed;

            Map.ClearBlocks();
            roomMade = new bool[0][];

            Generate();
			if (cts.IsCancellationRequested)
				return Task.FromResult(false);

            // Add the stuff onto DataStr (m3, BG, etc.)
            Map.BGC = 0x00111111;
            Map.artCodes[8] = ArtStr;

            Console.WriteLine("Map Generated");
			return Task.FromResult(true);
        }

        private void Generate()
        {
            // Re-size
            Array.Resize(ref roomMade, Size);
            for (int i = 0; i < Size; i++)
                Array.Resize(ref roomMade[i], Size);
            GenerateShell();

            // Begin
            Start = new Point(R.Next(0, Size), R.Next(0, Size));
            Map.AddBlock(Start.X * Sq_Size + Sq_Size / 2, Start.Y * Sq_Size + Sq_Size / 2, BlockID.P1);
            Map.AddBlock(Start.X * Sq_Size + Sq_Size / 2, Start.Y * Sq_Size + Sq_Size / 2, BlockID.P2);
            Map.AddBlock(Start.X * Sq_Size + Sq_Size / 2, Start.Y * Sq_Size + Sq_Size / 2, BlockID.P3);
            Map.AddBlock(Start.X * Sq_Size + Sq_Size / 2, Start.Y * Sq_Size + Sq_Size / 2, BlockID.P4);
            roomMade[Start.X][Start.Y] = true;

            List<Point> branchableRooms = new List<Point>(Size * Size);
            branchableRooms.Add(Start);
            while (branchableRooms.Count > 0)
            {
                // Find rooms that can be branched from.
                List<Point> canBranchFrom = getRoomsToBranchFrom(branchableRooms);
                if (canBranchFrom.Count > 0)
                {
                    // Pick a room to start a branch from.
                    int index = R.Next(0, canBranchFrom.Count);
                    Point currentRoom = canBranchFrom[index];

                    do
                    {
                        int Dir = getBranchDirection(currentRoom.X, currentRoom.Y, true);
                        removeWall(currentRoom.X, currentRoom.Y, Dir);
                        if (Dir == 0)
                            currentRoom.Y -= 1;
                        else if (Dir == 1)
                            currentRoom.X += 1;
                        else if (Dir == 2)
                            currentRoom.Y += 1;
                        else if (Dir == 3)
                            currentRoom.X -= 1;
                        else if (Dir == -1)
                            break;

                        roomMade[currentRoom.X][currentRoom.Y] = true;
                        branchableRooms.Add(currentRoom);
                    } while (R.NextDouble() > Branch); // continue this branch until RNG says stop
                }

				if (cts.IsCancellationRequested)
					return;
            }

            // Special stuffs
            if (Sq_Size > 3)
            {
                for (int iX = 0; iX < Size; iX++)
                {
                    for (int iY = 0; iY < Size; iY++)
                        fillRoom(iX, iY);

					if (cts.IsCancellationRequested)
						return;
                }
            }
            placeCeilingBlocks();

            do
            {
                Finish = new Point(R.Next(0, Size), R.Next(0, Size));
            } while (Finish == Start);
            Map.ReplaceBlock(Finish.X * Sq_Size + Sq_Size / 2, Finish.Y * Sq_Size + Sq_Size / 2, BlockID.Finish);

			if (cts.IsCancellationRequested)
				return;

            generateArt();
        }
        private void GenerateShell()
        {
            for (int iX = 0; iX <= Size; iX++)
            {
                for (int iY = 0; iY <= Sq_Size * Size; iY++)
                    Map.ReplaceBlock(iX * Sq_Size, iY, BlockID.BB0);

				if (cts.IsCancellationRequested)
					return;
            }
            for (int iY = 0; iY <= Size; iY++)
            {
                for (int iX = 0; iX <= Sq_Size * Size; iX++)
                    Map.ReplaceBlock(iX, iY * Sq_Size, BlockID.BB0);

				if (cts.IsCancellationRequested)
					return;
         }
        }

        // Used in generation
        private void removeWall(int X, int Y, int Dir)
        {
            int L = (int)Math.Floor((Sq_Size / 3) + 0.5);
            if (L % 2 == Sq_Size % 2)
                L += 1;

            int S = (int)Math.Floor((Sq_Size * 0.5) - ((L - 1) * 0.5));

            for (int i = S; i < S + L; i++)
            {
                if (Dir == 0)
                    Map.ReplaceBlock(X * Sq_Size + i, Y * Sq_Size, BlockID.Water);
                else if (Dir == 1)
                    Map.ReplaceBlock(X * Sq_Size + Sq_Size, Y * Sq_Size + i, BlockID.Water);
                else if (Dir == 2)
                    Map.ReplaceBlock(X * Sq_Size + i, Y * Sq_Size + Sq_Size, BlockID.Water);
                else if (Dir == 3)
                    Map.ReplaceBlock(X * Sq_Size, Y * Sq_Size + i, BlockID.Water);
            }
        }

        private List<Point> getRoomsToBranchFrom(List<Point> potentiallyBranchableRooms)
        {
            List<Point> ret = new List<Point>();
            int i = 0;
            while (i != potentiallyBranchableRooms.Count || ret.Count == 5000)
            {
                Point Pt = potentiallyBranchableRooms[i];
                if (getBranchDirection(Pt.X, Pt.Y, false) != -1)
                {
                    ret.Add(new Point(Pt.X, Pt.Y));
                }
                else
                {
                    potentiallyBranchableRooms.RemoveAt(i);
                    i -= 1;
                }

                i += 1;
            }

            return ret;
        }
        private int getBranchDirection(int X, int Y, bool allowCircles = false)
        {
            List<int> PosDirs = new List<int>(4);
            if (allowCircles && Circle > R.NextDouble())
            {
                if (X - 1 >= 0)
                    PosDirs.Add(3);
                if (X + 1 < Size)
                    PosDirs.Add(1);
                if (Y - 1 >= 0)
                    PosDirs.Add(0);
                if (Y + 1 < Size)
                    PosDirs.Add(2);
            }
            else
            {
                if (X - 1 >= 0 && !roomMade[X - 1][Y])
                    PosDirs.Add(3);
                if (X + 1 < Size && !roomMade[X + 1][Y])
                    PosDirs.Add(1);
                if (Y - 1 >= 0 && !roomMade[X][Y - 1])
                    PosDirs.Add(0);
                if (Y + 1 < Size && !roomMade[X][Y + 1])
                    PosDirs.Add(2);
            }

            if (PosDirs.Count == 0)
                return -1;

            return PosDirs[R.Next(0, PosDirs.Count)];
        }

        // TODO: Make this work nicer. Items and happys should be percentages.
        private void placeCeilingBlocks()
        {
            int X;
            int Y;
            for (int i = 0; i < Items; i++)
            {
                X = R.Next(0, Size);
                Y = R.Next(0, Size);
                Map.ReplaceBlock(X * Sq_Size + 1, Y * Sq_Size, BlockID.Item);
                Map.ReplaceBlock(X * Sq_Size + Sq_Size - 1, Y * Sq_Size, BlockID.Item);

				if (cts.IsCancellationRequested)
					return;
            }
            for (int i = 0; i < Happys; i++)
            {
                X = R.Next(0, Size);
                Y = R.Next(0, Size);
                Map.ReplaceBlock(X * Sq_Size + 1, Y * Sq_Size, BlockID.Happy);
                Map.ReplaceBlock(X * Sq_Size + Sq_Size - 1, Y * Sq_Size, BlockID.Happy);

				if (cts.IsCancellationRequested)
					return;
            }
        }

        private void fillRoom(int X, int Y)
        {
            // Don't put anything in the starting room.
            if (X == Start.X && Y == Start.Y)
                return;

            // order to place (least desired to most desired): mine, move, crumble, vanish, brick, water
            int[] types = new int[] { BlockID.Mine, BlockID.Move, BlockID.Crumble, BlockID.Vanish, BlockID.Brick, BlockID.Water };
            double[] chances = new double[] { Bomb, Move, Crumble, Vanish, Brick, Water };
            double[] fills = new double[] { Bomb_Fill, Move_Fill, Crumble_Fill, Vanish_Fill, Brick_Fill, Water_Fill };

            for (int iT = 0; iT < types.Length; iT++)
            {
                if (chances[iT] > R.NextDouble() * 100)
                {
                    int blockX;
                    int blockY;
                    // How many?
                    int count = (int)((Sq_Size - 1) * (Sq_Size - 1) * fills[iT] / 100);

                    for (int i = 0; i < count; i++)
                    {
                        if (types[iT] == BlockID.Brick) // Smaller fill space to avoid blocking the path from side to top.
                        {
                            blockX = R.Next(2, Sq_Size - 1);
                            blockY = R.Next(2, Sq_Size - 1);
                        }
                        else
                        {
                            blockX = R.Next(1, Sq_Size);
                            blockY = R.Next(1, Sq_Size);
                        }
                        blockX = X * Sq_Size + blockX;
                        blockY = Y * Sq_Size + blockY;
                        Map.ReplaceBlock(blockX, blockY, types[iT]);
                    }
                }
            }
        }

        string[] Dig = new string[] {"10;0;0;24;-10;0;0;-24", "3;-4;0;24;-5;0;10;0", "10;0;0;12;-10;0;0;12;10;0"
			, "10;0;0;12;-10;0;10;0;0;12;-10;0", "0;12;10;0;0;-12;0;24", "-10;0;0;12;10;0;0;12;-10;0"
			, "-10;0;0;24;10;0;0;-12;-10;0", "10;0;0;24", "10;0;0;12;-10;0;0;-12;0;24;10;0;0;-12"
			, "10;0;0;-24;-10;0;0;12;10;0"};
        Point[] DigS = new Point[] { new Point(0, 3), new Point(2, 7), new Point(0, 3)
			, new Point(0, 3), new Point(0, 3), new Point(10, 3)
			, new Point(10, 3), new Point(0, 3), new Point(0, 3)
			, new Point(0, 27)};
        private void generateArt()
        {
            if (Size > 100)
            {
                // return;
            }

            int Stp = 1;
            if (Size > 60)
                Stp = 2;

            StringBuilder str = new StringBuilder("t2,ceeeeee,");
            for (int iX = 0; iX < Size; iX += Stp)
            {
                for (int iY = 0; iY < Size; iY += Stp)
                {
                    string Dstr = iX.ToString();
                    int D = int.Parse(Dstr[0].ToString());
                    int X = (iX * Sq_Size * 30) + 2970 + (Sq_Size * 15);
                    int Y = (iY * Sq_Size * 30) + 3000 + (Sq_Size * 15);
                    if (Dstr.Length == 1)
                        X += 7;

                    str.Append("d" + (X + DigS[D].X) + ";" + (Y + DigS[D].Y));
                    str.Append(";" + Dig[D] + ",");
                    if (Dstr.Length > 1)
                    {
                        X += 15;
                        D = int.Parse(Dstr[1].ToString());
                        str.Append("d" + (X + DigS[D].X) + ";" + (Y + DigS[D].Y));
                        str.Append(";" + Dig[D] + ",");
                    }
                    else
                        X += 8;

                    // Y loc
                    X += 45;
                    Dstr = iY.ToString();
                    D = int.Parse(Dstr[0].ToString());
                    if (Dstr.Length == 1)
                        X += 7;

                    str.Append("d" + (X + DigS[D].X) + ";" + (Y + DigS[D].Y));
                    str.Append(";" + Dig[D] + ",");
                    if (Dstr.Length > 1)
                    {
                        X += 15;
                        D = int.Parse(Dstr[1].ToString());
                        str.Append("d" + (X + DigS[D].X) + ";" + (Y + DigS[D].Y));
                        str.Append(";" + Dig[D] + ",");
                    }
                }

				if (cts.IsCancellationRequested)
					return;
            }

            // Green finish coordinates at start
            str.Append("c00ff00,");
            string Dstrf = Finish.X.ToString();
            int Df = int.Parse(Dstrf[0].ToString());
            int Xf = (Start.X * Sq_Size * 30) + 2970 + (Sq_Size * 15);
            int Yf = (Start.Y * Sq_Size * 30) + 3030 + (Sq_Size * 15);
            if (Dstrf.Length == 1)
                Xf += 7;

            str.Append("d" + (Xf + DigS[Df].X) + ";" + (Yf + DigS[Df].Y));
            str.Append(";" + Dig[Df] + ",");
            if (Dstrf.Length > 1)
            {
                Xf += 15;
                Df = int.Parse(Dstrf[1].ToString());
                str.Append("d" + (Xf + DigS[Df].X) + ";" + (Yf + DigS[Df].Y));
                str.Append(";" + Dig[Df] + ",");
            }
            else
                Xf += 8;

            // Y loc
            Xf += 45;
            Dstrf = Finish.Y.ToString();
            Df = int.Parse(Dstrf[0].ToString());
            if (Dstrf.Length == 1)
                Xf += 7;

            str.Append("d" + (Xf + DigS[Df].X) + ";" + (Yf + DigS[Df].Y));
            str.Append(";" + Dig[Df] + ",");
            if (Dstrf.Length > 1)
            {
                Xf += 15;
                Df = int.Parse(Dstrf[1].ToString());
                str.Append("d" + (Xf + DigS[Df].X) + ";" + (Yf + DigS[Df].Y));
                str.Append(";" + Dig[Df] + ",");
            }
            // Remove trailing ,
            str.Remove(str.Length - 1, 1);

            ArtStr = str.ToString();
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
