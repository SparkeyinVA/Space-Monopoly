#ifndef GAMEGRID_H
#define GAMEGRID_H

#include "Grid.h"

class GameGrid : public Grid<int>
{
	private:


	public:
		GameGrid();
		GameGrid(int, int);

        void randomise(void);
};

inline GameGrid::GameGrid() : Grid<int>()
{ }

inline GameGrid::GameGrid(int x, int y) : Grid<int>(x, y)
{ }

#endif
 