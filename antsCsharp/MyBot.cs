//Copyright (C) 2013 <copyright holders>
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of this 
//software and associated documentation files (the "Software"), to deal in the Software without 
//restriction, including without limitation the rights to use, copy, modify, merge, publish, 
//distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom 
//the Software is furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or 
//substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
//INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR 
//PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
//ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
//ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
//SOFTWARE.

//Author: Alex Rodrigues

using System.Collections.Generic;

namespace Ants
{
    /// <summary>
    /// This is my bot submitted to the  Google Ants AI Challenge. It's main strategy is based on collaborative diffusion
    /// using Antiobjects to create emergent collaboration between the ants. Read more about collaborative diffusion at
    /// http://www.cs.colorado.edu/~ralex/papers/PDF/OOPSLA06antiobjects.pdf. Collaborative diffusion is extremely powerful
    /// and allows path finding for an individual ant to work in constant time. It achieves this by putting all of the
    /// computational effort into the tiles of the map. These tiles work as anti-objects and contain a diffusion score
    /// assigned to it. These diffusion scores work as a sort of scent map in which ants can follow towards objectives. Tiles
    /// that are considered good such as food emit a large scent where as tiles that are considered bad or dangerous, have
    /// a negative effect on the surrounding scent. Tiles that are just plain intermediate tiles between more interesting 
    /// tiles calculate their scent based on the adjacent tiles and thereby diffuse the scent across the map.
    /// 
    /// Run time: Run time is a major concern in this competition. Each turn bots are only given at most 250ms to parse the
    /// game state, decide a strategy and issue orders to all ants. So time is a very limited resource when ant colonies
    /// become large; each ant needs to receive a new order.
    /// 
    /// One of the advantages of using collaborative diffusion is that its run time is only based on the number
    /// of tiles in the map. Let W be the number of horizontal tiles and H be the number of vertical tiles then at each
    /// turn we run O(W*H) to update the diffusion scores. After the diffusion scores have been updated ants choose where
    /// to move based on the adjacent tile with the highest diffusion score. This takes constant time per ant. The total
    /// time for a turn is O(W*H + A) where A, is the number of ants that need to be given orders.
    /// 
    /// This run time is in contrast to more standard approaches such as setting a target or searching for a target
    /// and using well known path finding algorithms such as BFS or A*. The problem with using these algorithms for each ant 
    /// is that it can be very expensive. Consider the worst case for BFS and A* for issuing an order for a single ant, 
    /// since the maximum search space is W*H in size the worse these algorithms can do for a single order is O(W*H). 
    /// Using this same strategy for every ant in the colony we get O(W*H*A). As we can see on an average size map, 
    /// this approach won't scale well with large ant colonies and some ants won't receive orders.
    /// </summary>
    internal class MyBot : Bot
    {
        /// <summary>
        /// The diffusion weight assigned to one of hills when it is in danger.
        /// </summary>
        public const double HILL_IN_DANGER = 550000;
        /// <summary>
        /// The diffusion weight assigned to a water tile. Ants instantly die when stepping on this tile so the
        /// weight should remain 0.
        /// </summary>
        public const double WATER_SCORE = 0;

        /// <summary>
        /// The diffusion weight for a food tile on diffusion layer 1.
        /// </summary>
        public const double FOOD_SCORE_D1 = 5000;
        /// <summary>
        /// The diffusion weight for an unexplored tile on diffusion layer 1.
        /// </summary>
        public const double UNSEEN_SCORE_D1 = 3500;
        /// <summary>
        /// The diffusion weight for an unexplored tile on diffusion layer 2.
        /// </summary>
        public const double UNSEEN_SCORE_D2 = 200000;
        /// <summary>
        /// The diffusion weight for enemy hills on diffusion layer 1.
        /// </summary>
        public const double ENEMYHILL_SCORE_D1 = 50000;
        /// <summary>
        /// The diffusion weight for enemy hills on diffusion layer 2.
        /// </summary>
        public const double ENEMYHILL_SCORE_D2 = 1000000;
        /// <summary>
        /// The diffusion weight for my hills on both diffusion layer.
        /// </summary>
        public const double MYHILL_SCORE = 0;
        /// <summary>
        /// The diffusion weight for my ants. The weight should remain zero to prevent friendly ants from killing each other.
        /// </summary>
        public const double MY_ANT_SCORE_D1 = 0;
        /// <summary>
        /// The diffusion weight for enemy ants on diffusion layer 2.
        /// </summary>
        public const double ENEMY_ANT_SCORE_D2 = 100000;
        /// <summary>
        /// The diffusion weight for unknown tiles for both diffusion layers.
        /// </summary>
        public const double UNKNOWN = 0;
        //controls the rate of diffusion for diffusion layer 1
        public const double D1_COEFFICIENT = 0.25;
        //controls the rate of diffusion for diffusion layer 2
        public const double D2_COEFFICIENT = 0.50;
        /// <summary>
        /// True iff the ant colony is in attack mode. Attack mode makes the ants more aggressive and will prefer to kill enemy ants over harvesting food.
        /// </summary>
        public bool attackMode = false;

        // DoTurn is run once per turn
        public override void DoTurn(GameState state)
        {
            state.expectedLocation.Clear();
            //attack until our number of ants falls below 250 from 300
            if (!attackMode && state.MyAnts.Count > 300) attackMode = true;
            if (attackMode && state.MyAnts.Count < 250) attackMode = false;

            this.diffuseOne(state);
            this.diffuseTwo(state);
            // loop through all my ants and try to give them orders
            foreach (Ant ant in state.MyAnts)
            {
                Location up = state.GetDestination(ant, Direction.North);
                Location right = state.GetDestination(ant, Direction.East);
                Location left = state.GetDestination(ant, Direction.West);
                Location down = state.GetDestination(ant, Direction.South);

                List<Tup4> adjacentTiles = new List<Tup4>();

                //use diffusion layer 1 this layer prefers to harvest food and grow the ant colony
                if (!attackMode)
                {
                    Tup4 n1 = new Tup4(state.diffusionOne[up.Row, up.Col], Direction.North, up.Row, up.Col);
                    Tup4 e1 = new Tup4(state.diffusionOne[right.Row, right.Col], Direction.East, right.Row, right.Col);
                    Tup4 w1 = new Tup4(state.diffusionOne[left.Row, left.Col], Direction.West, left.Row, left.Col);
                    Tup4 s1 = new Tup4(state.diffusionOne[down.Row, down.Col], Direction.South, down.Row, down.Col);
                    adjacentTiles.Add(n1);
                    adjacentTiles.Add(e1);
                    adjacentTiles.Add(s1);
                    adjacentTiles.Add(w1);
                }
                else
                {
                    Tup4 n2 = new Tup4(state.diffusionTwo[up.Row, up.Col], Direction.North, up.Row, up.Col);
                    Tup4 e2 = new Tup4(state.diffusionTwo[right.Row, right.Col], Direction.East, right.Row, right.Col);
                    Tup4 w2 = new Tup4(state.diffusionTwo[left.Row, left.Col], Direction.West, left.Row, left.Col);
                    Tup4 s2 = new Tup4(state.diffusionTwo[down.Row, down.Col], Direction.South, down.Row, down.Col);
                    //use diffusion layer 2, this layer prefers to be aggressive
                    adjacentTiles.Add(n2);
                    adjacentTiles.Add(s2);
                    adjacentTiles.Add(e2);
                    adjacentTiles.Add(w2);
                }

                double maxDiffusion = 0; //assume the ant can't move
                Direction direction = Direction.East;
                int ct = 0;
                int row = 0;
                int col = 0;
                //check the four adjacent tiles to move
                while (ct < 4)
                {
                   //check if this tile has a higher diffusion score and that it is not occupied already by a friendly ant
                    if (adjacentTiles[ct].Item1 > maxDiffusion && state.diffusionOne[adjacentTiles[ct].Item3, adjacentTiles[ct].Item4] > 0 && state.GetIsUnoccupied(new Location(adjacentTiles[ct].Item3, adjacentTiles[ct].Item4)))
                    {
                        maxDiffusion = adjacentTiles[ct].Item1;
                        direction = adjacentTiles[ct].Item2;
                        row = adjacentTiles[ct].Item3;
                        col = adjacentTiles[ct].Item4;
                    }
                    ct += 1;
                }

                //ants will only move onto tiles with a diffusion score > 0
                if (maxDiffusion > 0)
                {
                    IssueOrder(ant, direction);
                    state.diffusionOne[row, col] = 0;
                    state.expectedLocation[state.GetDestination(ant, direction)] = ant;
                }
                else
                {
                    //ant could not move expected location is the same
                    state.expectedLocation[new Location(ant.Row, ant.Col)] = ant;
                }
                // check if we have time left to calculate more orders
                if (state.TimeRemaining < 10)
                {
                    //exit the loop immediately to prevent the bot from timing out
                   break;
                }
            }
        }


        /// <summary>
        /// Calculate the current diffusion scores for layer 1.
        /// </summary>
        /// <param name="state">The state.</param>
        public void diffuseOne(GameState state)
        {
            for (int row = 0; row < state.Height; row++)
            {
                for (int col = 0; col < state.Width; col++)
                {
                    if (state.map[row, col] == Tile.Water)
                    {
                        state.diffusionOne[row, col] = WATER_SCORE;
                    }

                    else if (state.map[row, col] == Tile.Ant)
                    {
                        state.diffusionOne[row, col] = MY_ANT_SCORE_D1;

                        Location location = new Location(row, col);
                        if (state.ants.ContainsKey(location))
                        {
                            Ant ant = state.ants[location];

                            if (ant.Team != 0)
                            {
                                List<AntHill> myHills = state.MyHills;
                                foreach (AntHill myHill in myHills)
                                {
                                    if (state.GetDistance(myHill, ant) <= 12)
                                    {
                                        state.diffusionOne[row, col] = HILL_IN_DANGER;

                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (state.map[row, col] == Tile.Food)
                    {
                        state.diffusionOne[row, col] = FOOD_SCORE_D1;
                    }
                    else if (state.map[row, col] == Tile.Unseen)
                    {
                        state.diffusionOne[row, col] = UNSEEN_SCORE_D1;
                    }
                    else if (state.isEnemyHill(row, col))
                    {
                        state.diffusionOne[row, col] = ENEMYHILL_SCORE_D1;
                    }
                    else if (state.isMyHill(row, col))
                    {
                        state.diffusionOne[row, col] = MYHILL_SCORE;
                    }
                    else
                    {
                        double u = state.diffusionOne[row, col];
                        Location L = new Location(row, col);
                        Location up = state.GetDestination(L, Direction.North);
                        Location right = state.GetDestination(L, Direction.East);
                        Location left = state.GetDestination(L, Direction.West);
                        Location down = state.GetDestination(L, Direction.South);
                        state.diffusionOne[row, col] = u + D1_COEFFICIENT * (1
                                                     + state.diffusionOne[up.Row, up.Col]
                                                     + state.diffusionOne[down.Row, down.Col]
                                                     + state.diffusionOne[left.Row, left.Col]
                                                     + state.diffusionOne[right.Row, right.Col]
                                                     - u * 4);
                    }
                }
            }
        }
        /// <summary>
        /// Calculate the current diffusion scores for layer 2.
        /// </summary>
        /// <param name="state">The state.</param>
        public void diffuseTwo(GameState state)
        {
            for (int row = 0; row < state.Height; row++)
            {
                for (int col = 0; col < state.Width; col++)
                {
                    if (state.map[row, col] == Tile.Water) state.diffusionTwo[row, col] = WATER_SCORE;

                    else if (state.map[row, col] == Tile.Ant
                        && state.ants.ContainsKey(new Location(row, col))
                        && state.ants[new Location(row, col)].Team != 0)
                    {
                        state.diffusionTwo[row, col] = ENEMY_ANT_SCORE_D2;
                    }
                    else if (state.map[row, col] == Tile.Unseen)
                    {
                        state.diffusionTwo[row, col] = UNSEEN_SCORE_D2;
                    }
                    else if (state.isEnemyHill(row, col))
                    {
                        state.diffusionTwo[row, col] = ENEMYHILL_SCORE_D2;
                    }
                    else if (state.isMyHill(row, col))
                    {
                        state.diffusionTwo[row, col] = MYHILL_SCORE;
                    }
                    else
                    {
                        double u = state.diffusionTwo[row, col];
                        Location location = new Location(row, col);
                        Location up = state.GetDestination(location, Direction.North);
                        Location right = state.GetDestination(location, Direction.East);
                        Location left = state.GetDestination(location, Direction.West);
                        Location down = state.GetDestination(location, Direction.South);
                        state.diffusionTwo[row, col] = u + D2_COEFFICIENT * (1
                                                     + state.diffusionTwo[up.Row, up.Col]
                                                     + state.diffusionTwo[down.Row, down.Col]
                                                     + state.diffusionTwo[left.Row, left.Col]
                                                     + state.diffusionTwo[right.Row, right.Col]
                                                     - u * 4);
                    }
                }
            }
        }

        public static void Main(string[] args)
        {
            new Ants().PlayGame(new MyBot());
        }
    }

    /// <summary>
    /// A custom four item Tuple.
    /// </summary>
    public class Tup4
    {
        public double Item1;
        public Direction Item2;
        public int Item3;
        public int Item4;

        public Tup4(double i1, Direction i2, int i3, int i4)
        {
            Item1 = i1;
            Item2 = i2;
            Item3 = i3;
            Item4 = i4;
        }
    }
}