/**
* Standard quick, simple data compressor.
* We just store the (binary) compressed data and compression
* table, and do neccessary conversions in each accessor.
* 11/July/2002
*/
#ifndef COMPRESSOR_H
#define COMPRESSOR_H

#include "Vector.h"

class Compressor {

    private:
        Vector<bool> _data;
        Vector<unsigned char> _table;
  
        //-- finds the position of the specified character in the 
        //-- compression table
        int findPos(const unsigned char&); 
        
        //-- inserts the binary pattern associated with the specified
        //-- position
        void insertData(const int&);
        
    public:
        //-- constructors
        Compressor(const unsigned char* data,        //-- The data
                    const int length);      //-- The data length

        Compressor(const char* compressed,  //-- The compressed data
                    const char* table,      //-- The compression table
                    const int datalength,   //-- The data length
                    const int tablelength); //-- The table length

        //-- destructor
        ~Compressor();

        //-- Accessors
        char* getData() const;            //-- Get the decompressed data
        unsigned char* getCompressedData() const;  //-- Get the compressed data
        const char* getTable() const;           //-- Get the compression table

        const int getCompressedSize(void);      //-- Get # of compressed bytes
};

/**
* Default destructor
*/
inline Compressor::~Compressor()
{ }

/**
* Get # of compressed bytes
*/
inline const int Compressor::getCompressedSize(void) {
    return _data.size()/8 + ((_data.size()%8>0)?1:0);
}

#endif