//---------------------------------------------------------------------------
#ifndef HighScoresH
#define HighScoresH
//---------------------------------------------------------------------------
#include <Classes.hpp>
#include <Controls.hpp>
#include <StdCtrls.hpp>
#include <Forms.hpp>
#include <ComCtrls.hpp>
#include <ExtCtrls.hpp>
#include "large.h"
//---------------------------------------------------------------------------
class THighScoresForm : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel1;
	TPageControl *PageControl1;
	TTabSheet *TabSheet1;
	TTabSheet *TabSheet2;
	TTabSheet *TabSheet3;
	TPanel *Panel2;
	TListView *ListView1;
	TPanel *Panel3;
	TListView *ListView2;
	TPanel *Panel4;
	TListView *ListView3;
	TPanel *Panel5;
	TButton *Okay;
private:	// User declarations
public:		// User declarations
	__fastcall THighScoresForm(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern PACKAGE THighScoresForm *HighScoresForm;
void HighScores(bool);
extern struct ScoreRecord {
    AnsiString Name;
    large Wealth;
    bool Events;
    bool newentry;
} **Score;

large Total(int Player);
//---------------------------------------------------------------------------
#endif
