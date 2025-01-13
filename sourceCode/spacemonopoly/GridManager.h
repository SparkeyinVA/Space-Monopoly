//---------------------------------------------------------------------------
#ifndef GridManagerH
#define GridManagerH
//---------------------------------------------------------------------------
#include <Classes.hpp>
#include <Controls.hpp>
#include <StdCtrls.hpp>
#include <Forms.hpp>
#include <ComCtrls.hpp>
#include <ExtCtrls.hpp>
//---------------------------------------------------------------------------
class TGridManagerForm : public TForm
{
__published:	// IDE-managed Components
    TPanel *Panel1;
    TPanel *Panel2;
    TPanel *Panel3;
    TPageControl *PageControl1;
    TTabSheet *TabSheet1;
    TTabSheet *TabSheet2;
    TPanel *Panel4;
    TButton *Button1;
    TButton *Button2;
    TButton *Button3;
    TButton *Button4;
    TScrollBox *ScrollBox1;
    TImage *Screen;
    TPanel *Panel5;
    TPanel *Panel6;
    TGroupBox *GroupBox1;
    TListView *ListView1;
private:	// User declarations
public:		// User declarations
    __fastcall TGridManagerForm(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern PACKAGE TGridManagerForm *GridManagerForm;
//---------------------------------------------------------------------------
#endif
