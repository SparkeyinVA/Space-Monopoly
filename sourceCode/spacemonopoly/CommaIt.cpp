//---------------------------------------------------------------------------
#include <vcl\vcl.h>

#include "CommaIt.h"
//---------------------------------------------------------------------------
//This little function shoves commas into strings that represent numbers
//eg. from this- "1234567" to "1,234,567"
void comma(char *string) {
    for (int i=strlen(string)-3; i>0; i -= 3) {
        string[strlen(string)+1]='\0';
        for (int j=strlen(string); j>i; --j)
            string[j] = string[j-1];
        string[i]=',';
    }
}
//=============
 