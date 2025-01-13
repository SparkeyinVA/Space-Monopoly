#ifndef PREFS_H
#define PREFS_H

#include "CurrencyUnit.h"

class Prefs 
{
	private:
       	CurrencyUnit _cu;
        bool _mergers;
        bool _expansion;
        bool _details;
        bool _companies;

	public:
    	Prefs(void);

		const Prefs& operator=(const Prefs&); //assignment operator

		//Accessors
		CurrencyUnit getCurrencyUnit(void) const;
        bool mergersSet(void) const;
        bool expansionSet(void) const;
        bool detailsSet(void) const;
		bool companiesSet(void) const;

		//Mutators
        void setCurrencyUnit(CurrencyUnit cu);
        void setMergers(bool);
        void setExpansion(bool);
        void setDetails(bool);
		void setCompanies(bool);

        bool load(const char* file);
        bool save(const char* file) const;
};

inline Prefs::Prefs() : _cu(CurrencyUnit()), _mergers(false),
				 		_expansion(false), _companies(false), _details(false)
{ }

inline CurrencyUnit Prefs::getCurrencyUnit(void) const { return _cu; }
inline bool Prefs::mergersSet(void)   const	{ return _mergers; }
inline bool Prefs::expansionSet(void) const	{ return _expansion; }
inline bool Prefs::detailsSet(void)   const	{ return _details; }
inline bool Prefs::companiesSet(void) const	{ return _companies; }

inline void Prefs::setCurrencyUnit(CurrencyUnit c) { _cu = c; }
inline void Prefs::setMergers(bool flag)	{ _mergers = flag; }
inline void Prefs::setExpansion(bool flag)	{ _expansion = flag; }
inline void Prefs::setDetails(bool flag)	{ _details = flag; }
inline void Prefs::setCompanies(bool flag)	{ _companies = flag; }

#endif
