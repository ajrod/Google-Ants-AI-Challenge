Google-Ants-AI-Challenge
========================

The AI Challenge is all about creating artificial intelligence that controls a colony of ants which fight against 
other colonies for domination in a competitive tournament. I participated in this tournament twice concurrently.

My bot in this repository I wrote independently, but I also spent much more time working on another bot with a team
for a course I took at University of Toronto. That bot was written in haskell and the codebase is much larger, and less
readable so I will not be putting it on github.

The reason I participated in this tournament was to experiment and research different techniques in AI.

Objectives
--------
The main objective of the game, is to conquer other ant colonies or by the end of the last turn have a higher score.
Score can only be increased by destroying an opponents colony.

The game starts off with each bot having a single ant. In order to get more ants, food must be located and harvested.
At the same time, you must defend your ant hill(s) or launch an attack yourself.

A game is divided up into X turns where each bot will get 250ms to issue orders to their ant colony per turn.



Strategy
--------
My bots main strategy is based on collaborative diffusion
using Antiobjects to create emergent collaboration between the ants. Read more about collaborative diffusion at
http://www.cs.colorado.edu/~ralex/papers/PDF/OOPSLA06antiobjects.pdf. 

Collaborative diffusion is extremely powerful
and allows path finding for an individual ant to work in constant time. It achieves this by putting all of the
computational effort into the tiles of the map. These tiles work as anti-objects and contain a diffusion score
assigned to it. These diffusion scores work as a sort of scent map in which ants can follow towards objectives. Tiles
that are considered good such as food emit a large scent where as tiles that are considered bad or dangerous, have
a negative effect on the surrounding scent. Tiles that are just plain intermediate tiles between more interesting 
tiles calculate their scent based on the adjacent tiles and thereby diffuse the scent across the map.

Run Time
--------
Run time: Run time is a major concern in this competition. Each turn bots are only given at most 250ms to parse the
game state, decide a strategy and issue orders to all ants. So time is a very limited resource when ant colonies
become large; each ant needs to receive a new order.

One of the advantages of using collaborative diffusion is that its run time is only based on the number
of tiles in the map. Let W be the number of horizontal tiles and H be the number of vertical tiles then at each
turn we run O(W*H) to update the diffusion scores. After the diffusion scores have been updated ants choose where
to move based on the adjacent tile with the highest diffusion score. This takes constant time per ant. The total
time for a turn is O(W*H + A) where A, is the number of ants that need to be given orders.

This run time is in contrast to more standard approaches such as setting a target or searching for a target
and using well known path finding algorithms such as BFS or A*. The problem with using these algorithms for each ant 
is that it can be very expensive. Consider the worst case for BFS and A* for issuing an order for a single ant, 
since the maximum search space is W*H in size the worse these algorithms can do for a single order is O(W*H). 
Using this same strategy for every ant in the colony we get O(W*H*A). As we can see on an average size map, 
this approach won't scale well with large ant colonies and some ants won't receive orders.
