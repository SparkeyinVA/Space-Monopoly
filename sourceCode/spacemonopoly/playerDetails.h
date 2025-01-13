//---------------------------------------------------------------------------
#ifndef playerDetailsH
#define playerDetailsH
//---------------------------------------------------------------------------
#include <Classes.hpp>
#include <Controls.hpp>
#include <StdCtrls.hpp>
#include <Forms.hpp>
#include <ExtCtrls.hpp>
#include <ComCtrls.hpp>
//---------------------------------------------------------------------------
class TplayerDetailsForm : public TForm
{
__published:	// IDE-managed Components
    TPanel *Panel2;
    TButton *Button1;
    TGroupBox *GroupBox1;
    TListView *ListView1;
    TLabel *Label1;
    TLabel *Label2;
    TLabel *Label3;
    TLabel *Label4;
    TLabel *Label5;
    TLabel *Label6;
    TLabel *Label7;
private:	// User declarations
public:		// User declarations
    __fastcall TplayerDetailsForm(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern PACKAGE TplayerDetailsForm *playerDetailsForm;
//---------------------------------------------------------------------------
#endif
