//---------------------------------------------------------------------------
#include "CurrencyUnit.h"
#include <stdlib.h>

//---------------------------------------------------------------------------
bool CurrencyUnit::setUnit(AnsiString u)
{	if ( (u.Length() > 3) || (u.Length() < 1) )
    	return false;
    else _unit = u;

    return true;
}

const AnsiString CurrencyUnit::output(__int64 amount) const
{	char buffer[20];
    _i64toa(amount, buffer, 10);
	return output(AnsiString(buffer));
}


