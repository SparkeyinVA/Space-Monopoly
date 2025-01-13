//// A standard Vector template class ////

#ifndef VECTOR_H
#define VECTOR_H

#include <assert.h>
#include "Container.h"
#include "Basic.h"

template <class ELT> class Vector : public Container {
	private:
		ELT *_block;
		Size _capacity;
		void resize(Size newSize);
	public:
	        //---- constructors ----
		Vector();                           //default
        Vector(Size size);
		Vector(const Vector<ELT>& vec);     //copy
		~Vector();                          //destructor
		Vector<ELT>& operator=(const Vector<ELT>&);     //assignment 

        	//---- accessors ----
        const ELT& operator[](Index index) const;
        ELT* toArray() const;

        	//---- mutators ----
		ELT& operator[](Index index);
		void insert(const ELT& elt);
		void remove();
		void append(const Vector<ELT>& vec);
        const ELT& item() const;
};

/**
* Changes the capacity of the vector to specified capacity.
* Note the new capacity has to be greater than the size. 
*/
template<class ELT>
void Vector<ELT>::resize(Size newCapacity) {
    if (newCapacity < _size) return;

	_capacity = newCapacity;
	ELT *grown = new ELT[_capacity];

	for (Index i=0; Size(i)<_size; ++i)
		grown[i] = _block[i];

	delete[] _block;
	_block = grown;
}

/**
* Default constructor - reserves a capacity of 2
*/
template<class ELT>
inline Vector<ELT>::Vector() : Container(), _capacity(2), _block(new ELT[2])
{ }

/**
* Construct a vector of the specified size, setting capacity to twice this value
*/
template<class ELT>
inline Vector<ELT>::Vector(Size size) :
	Container(), _capacity(2*(size > 0 ? size : 1)), _block(new ELT[2*(size > 0 ? size : 1)])
{ }

/**
* Copy constructor
*/
template<class ELT>
Vector<ELT>::Vector(const Vector<ELT>& vec) :
    Container(vec._size),
	_capacity(vec._capacity),
	_block(new ELT[_capacity])
{
    for (Index i=0; Size(i)<_size; ++i)
		_block[i] = vec._block[i];
}

/**
* Destructor
*/
template<class ELT>
inline Vector<ELT>::~Vector() {
	if (_block != 0) delete[] _block;
}

/**
* Assignment constructor
*/
template<class ELT>
Vector<ELT>& Vector<ELT>::operator=(const Vector<ELT>& vec) {
    if (this != &vec) {
        if (_capacity != vec._capacity) {
            delete[] _block;
            _capacity = vec._capacity;
            _block = new ELT[_capacity];
        }
        _size = vec._size;

        for (Index i=0; i<_size; ++i)
            _block[i] = vec._block[i];
    }

    return *this;
}

/**
* Accesses an element by its vector position
*/
template<class ELT>
inline const ELT& Vector<ELT>::operator[](Index index) const {
    assert(Size(index) < _size);
    return _block[index];
}

/**
* Set an element by its vector position
*/
template<class ELT>
inline ELT& Vector<ELT>::operator[](Index index) {
    assert(Size(index) < _size);
    return _block[index];
}

/**
* Inserts at the end of this vector
*/
template<class ELT>
void Vector<ELT>::insert(const ELT& elt) {
    if (_capacity == _size) resize(2*_capacity);    //resize if limit reached
    _block[_size++] = elt;
}

/**
* Removes the last element from this vector
*/
template<class ELT>
inline void Vector<ELT>::remove() {
    if (_size > 0) _block[_size--] = 0;
}

/**
* Appends the supplied vector to the end of this vector
*/
template<class ELT>
void Vector<ELT>::append(const Vector<ELT>& vec) {
    unsigned int newSize = _size + vec._size;
    if (newSize > _capacity) resize(newSize*2);

    for (Index i=0; Size(i)<vec._size; ++i)
        _block[_size+i] = vec._block[i];

    _size = newSize;

}

/**
* Returns the last element in this vector
*/
template<class ELT>
inline const ELT& Vector<ELT>::item() const {
    assert(_size > 0);
    return _block[_size - 1];
}

template<class ELT>
ELT* Vector<ELT>::toArray() const {
	ELT* toReturn = new ELT[_size];
	for (int i=0; Size(i)<_size; ++i) {
		toReturn[i] = _block[i];
	}
	
	return toReturn;
}


#endif
