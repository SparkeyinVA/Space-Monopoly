#ifndef GAME_H
#define GAME_H

#include "Prefs.h"
#include "GameGrid.h"

/**
* Stores the state of everything in the game.  Implements the Singleton
* pattern so that only one of these exists - essentially a global class.
*/
class Game
{	private:
		static Game* _instance;

		Game();
		Prefs* _prefs;
        GameGrid* _grid;

	public:
		static Game& getInstance();

        Prefs& getPrefs() const;
		GameGrid& getGrid() const;

};

inline Game::Game() : _prefs(new Prefs()), _grid(new GameGrid(16, 16))
{ }

inline Game& Game::getInstance()
{	return (*_instance);
}

inline Prefs& Game::getPrefs() const
{	return *_prefs;
}

inline GameGrid& Game::getGrid() const
{	return *_grid;
}

#endif
