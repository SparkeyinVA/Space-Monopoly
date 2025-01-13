#ifndef CURRENCY_UNIT
#define CURRENCY_UNIT

#include <vcl.h>

class CurrencyUnit //: public TObject
{
	private:
    	AnsiString _unit;	//ie. unit="£"
        bool _prefix;	//before=true, after = false;
	public:
    	CurrencyUnit();
    	CurrencyUnit(AnsiString unit, bool prefix);
    	bool setUnit(AnsiString u);
        const AnsiString getUnit(void) const;
        void setPostfix(void);
        void setPrefix(void);
		bool isPrefix(void) const;
		bool isPostfix(void) const;
        const AnsiString output(AnsiString amount) const;
        const AnsiString output(char * amount) const;
        const AnsiString output(__int64 amount) const;
};

inline CurrencyUnit::CurrencyUnit() :
	_unit(AnsiString("£")), _prefix(true)
{ }

inline CurrencyUnit::CurrencyUnit(AnsiString unit, bool prefix) :
	_unit(unit), _prefix(prefix)
{ }

inline const AnsiString CurrencyUnit::getUnit() const { return _unit; }

inline void CurrencyUnit::setPrefix(void) { _prefix = true; }
inline void CurrencyUnit::setPostfix(void) { _prefix = false; }

inline bool CurrencyUnit::isPrefix(void) const { return _prefix; }
inline bool CurrencyUnit::isPostfix(void) const { return !_prefix; }

inline const AnsiString CurrencyUnit::output(AnsiString amount) const
{	return _prefix ? getUnit() + amount : amount + getUnit();
}

inline const AnsiString CurrencyUnit::output(char *amount) const
{	return output(AnsiString(amount));
}


#endif
