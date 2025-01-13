//---------------------------------------------------------------------------
#ifndef OverFormH
#define OverFormH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
#include <vcl\ComCtrls.hpp>
//---------------------------------------------------------------------------
class TGameOver : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel2;
	TListView *ListView1;
	TPanel *Panel4;
	TLabel *Label6;
	TLabel *Label5;
	TLabel *Label4;
	TLabel *Label3;
	TLabel *Label2;
	TLabel *Label7;
	TLabel *Label8;
	TLabel *Label9;
	TLabel *Label10;
	TPanel *Panel3;
	TPanel *Panel1;
	TBitBtn *BitBtn1;
private:	// User declarations
public:		// User declarations
	__fastcall TGameOver(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TGameOver *GameOver;

//====PROTOTYPES...
void Finish(void);

//---------------------------------------------------------------------------
#endif
