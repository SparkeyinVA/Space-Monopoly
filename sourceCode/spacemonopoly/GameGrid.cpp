#include "GameGrid.h"

#include <time.h>
#include <stdlib.h>

void GameGrid::randomise(void)
{	time_t t;
	srand((unsigned) time(&t)); // Get random num gen working

	for (int j=0;j<_y;++j)
		for (int i=0;i<_x;++i)
			_gridp[i][j] = (rand() % 30 == 1) ? 1 : 0;
}

