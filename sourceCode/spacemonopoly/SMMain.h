//---------------------------------------------------------------------------
#ifndef SMMainH
#define SMMainH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Menus.hpp>
#include <vcl\Buttons.hpp>

#include "GameGrid.h"
#include "large.h"

//---------------------------------------------------------------------------
class TMain : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel2;
	TMainMenu *MainMenu1;
	TMenuItem *Project1;
	TMenuItem *Quit1;
	TMenuItem *N1;
	TMenuItem *Help1;
	TMenuItem *AboutSpaceMonopoly1;
	TMenuItem *NewGame1;
	TMenuItem *ViewHighScores1;
	TMenuItem *Options1;
	TTimer *Timer1;
	TMenuItem *CreateNewGrid1;
	TMenuItem *EndGame1;
    TMenuItem *GridSize1;
    TMenuItem *N16by16;
    TMenuItem *N20by20;
    TMenuItem *N32by32;
	TMenuItem *N2;
	TMenuItem *Preferences1;
	TImageList *ImageList1;
    TScrollBox *ScreenContainer;
    TImage *Screen;
    TPanel *Panel4;
    TShape *Shape5;
    TShape *Shape10;
    TShape *Shape9;
    TShape *Shape4;
    TShape *Shape8;
    TShape *Shape3;
	TBitBtn *Eridnus;
	TBitBtn *Debenese;
	TBitBtn *Cannon;
	TBitBtn *Betelguese;
	TBitBtn *Atlantis;
    TShape *Shape2;
    TShape *Shape7;
    TShape *Shape6;
    TShape *Shape1;
    TPanel *Panel1;
    TLabel *Label6;
    TLabel *Label5;
    TPanel *Panel5;
    TBitBtn *BitBtn1;
    TPanel *Panel3;
    TPanel *Panel6;
    TPanel *Panel7;
    TPanel *Panel8;
    TPanel *Panel9;
    TPanel *Panel10;
    TListBox *ListBox1;
    TBevel *Bevel5;
    TLabel *Label1;
	void __fastcall BitBtn1Click(TObject *Sender);

	void __fastcall FormCreate(TObject *Sender);

	void __fastcall NewGame1Click(TObject *Sender);
	void __fastcall CreateNewGrid1Click(TObject *Sender);
	void __fastcall Timer1Timer(TObject *Sender);
	void __fastcall AtlantisClick(TObject *Sender);
	void __fastcall BetelgueseClick(TObject *Sender);
	void __fastcall CannonClick(TObject *Sender);
	void __fastcall DebeneseClick(TObject *Sender);
	void __fastcall EridnusClick(TObject *Sender);
	void __fastcall AboutSpaceMonopoly1Click(TObject *Sender);
	void __fastcall ViewHighScores1Click(TObject *Sender);

	void __fastcall Quit1Click(TObject *Sender);
	void __fastcall EndGame1Click(TObject *Sender);
    void __fastcall ListBox1DblClick(TObject *Sender);

    void __fastcall N16by16Click(TObject *Sender);
    void __fastcall N20by20Click(TObject *Sender);
    void __fastcall N32by32Click(TObject *Sender);


	void __fastcall Preferences1Click(TObject *Sender);




	void __fastcall Image1MouseDown(TObject *Sender, TMouseButton Button,
          TShiftState Shift, int X, int Y);
	void __fastcall Image1MouseMove(TObject *Sender, TShiftState Shift, int X,
          int Y);
	void __fastcall FormCloseQuery(TObject *Sender, bool &CanClose);
    void __fastcall Panel3Click(TObject *Sender);
private:	// User declarations

public:		// User declarations

	struct 	coords	{
				int x;
                int y;
    }choice[5];

	__fastcall TMain(TComponent* Owner);
};
//---------------------------------------------------------------------------

extern int gametype;
extern int gridtype;

extern const int CLASSIC, TOURNAMENT;
extern const int SMALL, MEDIUM, LARGE;

extern bool progress; //Set to true when game in progress
extern bool sels; //Set when blue selections shown
extern int turn; //stores the turn number
extern int numplays,curplay;
extern struct Record {
	AnsiString Name;	//Player's Name
	large Money; //Pounds
	large Shares[5];	//No of shares in each company...
} *Player;

extern struct ComRecord	{
				AnsiString Name;	//Company Name
                int Value;		//Value of share (Pounds)
                bool Exist;	//Company created?
                int Color; //Company color
} *Company, *ComStore;

extern TMain *Main;

void go(void);
int UpdateGrid(int x,int y);
void redrawGrid(void);
void MainUpdate(void);
void MoneyShow(void);
int ComSize(int com);
void MenuUpdate(void);
//---------------------------------------------------------------------------
#endif
