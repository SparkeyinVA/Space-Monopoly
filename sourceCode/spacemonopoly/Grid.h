#ifndef GRID_H
#define GRID_H

template <class ELT> class Grid
{	protected:
		ELT **_gridp;
        int _x;
        int _y;

	private:
		void init(void);
		void free(void);

    public:
    	Grid();
		Grid(int xsize, int ysize);
        ~Grid();
		Grid(const Grid<ELT>&); //copy constructor
        const Grid<ELT>& operator=(const Grid<ELT>&); //assignment operator

		//array access/assignment
		ELT* operator[](int);
		const ELT* operator[](int) const;

		void clear(void);
		void resize(int x, int y);
};

template <class ELT>
inline Grid<ELT>::Grid(int xsize, int ysize) : _x(xsize), _y(ysize)
{	init();
}

template <class ELT>
inline Grid<ELT>::Grid() : _x(16), _y(16)
{	init();
}


template <class ELT>
void Grid<ELT>::init(void)
{	_gridp = new ELT*[_x];
    for (int ix=0; ix<_x; ++ix)
    	_gridp[ix] = new ELT[_y];
}

template <class ELT>
inline Grid<ELT>::~Grid()
{	free();
}

template <class ELT>
Grid<ELT>::Grid(const Grid<ELT>& grid) : _x(grid._x), _y(grid._y)
{	init();
	for (int iy=0; iy<_y; ++iy)
		for (int ix; ix<_x; ++ix)
			_gridp[ix][iy] = grid._gridp[ix][iy];
}

template <class ELT>
const Grid<ELT>& Grid<ELT>::operator=(const Grid<ELT>& grid)
{	if (&grid == this) return *this; //check for self assignment

	if ((grid._x != _x) || (grid._y != _y)) resize(grid._x, grid._y);

    for (int iy=0; iy<_y; ++iy)
		for (int ix=0; ix<_x; ++ix)
			_gridp[ix][iy] = grid._gridp[ix][iy];

	return *this;
}

template <class ELT>
void Grid<ELT>::clear(void)
{	for (int iy=0; iy<_y; ++iy)
		for (int ix; ix<_x; ++ix)
			_gridp[ix][iy] = 0;
}

//accessor
template <class ELT>
const ELT* Grid<ELT>::operator[](int xpos) const {
	return _gridp[xpos];
}

//mutator
template <class ELT>
ELT* Grid<ELT>::operator[](int xpos) {
	return _gridp[xpos];
}

template <class ELT>
void Grid<ELT>::resize(int x, int y)
{	free();
	_x = x; _y = y;
	_gridp = new ELT*[_x];
    for (int i=0; i<_x; ++i)
    	_gridp[i] = new ELT[_y];
}

template <class ELT>
void Grid<ELT>::free(void) {
	for (int i=0; i<_x; ++i) {
    	delete[] _gridp[i];
    }
    delete[] _gridp;
}

#endif
