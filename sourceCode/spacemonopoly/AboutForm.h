//---------------------------------------------------------------------------
#ifndef AboutFormH
#define AboutFormH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
//---------------------------------------------------------------------------
class TAbout : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel1;
	TPanel *Panel2;
	TLabel *Label1;
	TLabel *Label2;
	TLabel *Label3;
	TBitBtn *BitBtn1;
	TBevel *Bevel1;
	TPanel *Panel3;
	TImage *Image1;
	TLabel *Label9;
	TLabel *Label5;
	TLabel *Label4;
private:	// User declarations
public:		// User declarations
	__fastcall TAbout(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TAbout *About;
//---------------------------------------------------------------------------
#endif
