//---------------------------------------------------------------------------
#ifndef MergerH
#define MergerH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
#include <vcl\ComCtrls.hpp>
//---------------------------------------------------------------------------
class TMergForm : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel1;
	TPanel *Panel2;
	TListView *ListView1;
	TPanel *Panel3;
	TPanel *Panel4;
	TLabel *Label6;
	TLabel *Label7;
	TLabel *Label8;
	TPanel *Panel5;
	TBitBtn *BitBtn1;
private:	// User declarations
public:		// User declarations
	__fastcall TMergForm(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TMergForm *MergForm;
void Merger(int coma,int comb);
//---------------------------------------------------------------------------
#endif
