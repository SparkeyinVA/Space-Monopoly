//---------------------------------------------------------------------------
#include <vcl.h>
#pragma hdrstop

#include <stdio.h>
#include <stdlib.h>
#include "Prefs.h"
//---------------------------------------------------------------------------
#pragma package(smart_init)

bool Prefs::load(const char* file)
{	FILE *in = fopen(file, "r");
    if (in == NULL)
    	return false;

    char unit[4];
    for (int i=0; i<4; i++) {
    	unit[i] = fgetc(in);
    	if (unit[i] == '\n') { unit[i] = '\0'; break; }
    }

    _cu.setUnit(AnsiString(unit));

    fgetc(in) == '1' ? _cu.setPrefix() : _cu.setPostfix();
    _mergers = (fgetc(in) == '1');
	_expansion = (fgetc(in) == '1');
    _details = (fgetc(in) == '1');
	_companies = (fgetc(in) == '1');

    if (fclose(in) == EOF) return false;
    else return true;
}

bool Prefs::save(const char* file) const
{
	/**
    * output length + currency unit (AnsiString) + '/n'
    *        prefix        (bool)
    *        mergers
	*        expansion
    *        details
    *		 companies
    */
	FILE *out = fopen(file, "w");
    if (out == NULL) return false;


	fputs(_cu.getUnit().c_str(), out);
    fputc('\n', out);

	fputc(_cu.isPrefix() ? '1' : '0', out);
    fputc(_mergers ? '1' : '0', out);
	fputc(_expansion? '1' : '0', out);
	fputc(_details ? '1' : '0', out);
    fputc(_companies ? '1' : '0', out);

    if (fclose(out) == EOF) return false;

    return true;
}

const Prefs& Prefs::operator=(const Prefs& prefs)
{	_cu			= prefs._cu;
	_mergers	= prefs._mergers;
	_expansion	= prefs._expansion;
	_details	= prefs._details;
	_companies	= prefs._companies;
	return *this;
}
