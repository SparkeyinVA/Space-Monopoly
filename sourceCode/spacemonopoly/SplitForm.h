//---------------------------------------------------------------------------
#ifndef SplitFormH
#define SplitFormH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
#include <vcl\ComCtrls.hpp>
//---------------------------------------------------------------------------
class TSplit : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel2;
	TPanel *Panel1;
	TListView *ListView1;
	TPanel *Panel3;
	TLabel *Label7;
	TLabel *Label6;
	TPanel *Panel4;
	TBitBtn *BitBtn1;
private:	// User declarations
public:		// User declarations
	__fastcall TSplit(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern TSplit *Split;

void ComSplit(int com);


//---------------------------------------------------------------------------
#endif
