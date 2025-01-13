//---------------------------------------------------------------------------
#ifndef HighFormH
#define HighFormH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
//---------------------------------------------------------------------------
class THigh : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel1;
	TBitBtn *BitBtn1;
	TPanel *Panel2;
	TListBox *ListBox1;
	TLabel *Label1;
	TLabel *Label2;
	TLabel *Label3;
	TLabel *Label4;
	TBevel *Bevel1;
	TListBox *ListBox2;
	TListBox *ListBox3;
	TLabel *Label14;
	TLabel *Label13;
	TLabel *Label12;
	TLabel *Label11;
	TLabel *Label10;
	TLabel *Label9;
	TLabel *Label8;
	TLabel *Label7;
	TLabel *Label6;
	TLabel *Label5;
private:	// User declarations
public:		// User declarations
	__fastcall THigh(TComponent* Owner);
};
//---------------------------------------------------------------------------
extern THigh *High;
void HighScores(bool);
extern struct ScoreRecord{
			char Name[16];
			unsigned long int Wealth;
			bool Won;
            bool newentry;
            } *Score;
unsigned long int Total(int Player);
//---------------------------------------------------------------------------
#endif
