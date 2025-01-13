//---------------------------------------------------------------------------
#ifndef ComFormH
#define ComFormH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\Buttons.hpp>
#include <vcl\ExtCtrls.hpp>
//---------------------------------------------------------------------------
class TNewCom : public TForm
{
__published:	// IDE-managed Components
	TBitBtn *BitBtn1;
	TPanel *Panel1;
	TLabel *Label1;
	TLabel *Label2;
	TLabel *Label3;
	TLabel *Label4;
private:	// User declarations
public:		// User declarations
	__fastcall TNewCom(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TNewCom *NewCom;
void NewCompany(int x,int y);
//---------------------------------------------------------------------------
#endif
