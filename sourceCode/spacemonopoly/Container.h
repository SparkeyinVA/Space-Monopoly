//// Container ////

#ifndef CONTAINER_H
#define CONTAINER_H

#include "Basic.h"

class Container
{
    protected:
        Size _size;
    public:
        //Constructors
        Container(Size);
        Container();

        //Accessors
        Size size() const;
};

Container::Container(Size s) : _size(s) { };
Container::Container() : _size(0) { };

Size Container::size() const {
    return _size;
}

#endif
