//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop
#include <string.h>
#include <time.h>
#include <stdlib.h>

#include "large.h"
#include "Prefs.h"
#include "SMMain.h"
#include "Players.h"
#include "ComForm.h"
#include "ComInfoForm.h"
#include "Merger.h"
#include "SplitForm.h"
#include "OverForm.h"
#include "AboutForm.h"
#include "GridManager.h"
#include "HighScores.h"
#include "CurrencyUnit.h"
#include "ComEditForm.h"
#include "CommaIt.h"
#include "playerDetails.h"
#include "Preferences.h"
#include "Game.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TMain *Main;
//==============PROTOTYPES...

void CreateGrid(void);

void setGridSize(int);

void DrawSels(void);
void EraseChoices(void);
void CheckNewComp(int x,int y); //Check to see if a new company has been created
void CheckMerger(int x,int y);
void Merger(int com1,int com2);
void Spread(int x,int y,int to,int from);

void action(int x, int y);
bool closemain(int mode);
int comAdjoining(int,int,int&,int&,bool*,int&);
//==============GLOBALS...
int blwid, blhit, gridxsize, gridysize;
int iStart;
const int SMALL = 0, MEDIUM = 1, LARGE = 2;
int gridtype=SMALL;
int curplay;		//current player
int turn;			//Current turn
int maxturns=80;	//Maximum number of turns


int Choice [5][2]; //Possible Choices (5) - x and y coords
int numplays;
bool progress	= false; //Game NOT in progress when set to false
bool sels		= false; // Possible selections should be shown (toggle on/off)

bool hand = false; //true if the cursor is a hand

const int CLASSIC = 0, TOURNAMENT = 1;

int gametype=CLASSIC;	//Gametype 0 = classic game, 1=Tournament;

struct Record *Player;
struct ComRecord *Company;
//---------------------------------------------------------------------------
__fastcall TMain::TMain(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void __fastcall TMain::BitBtn1Click(TObject *Sender)
{
	Game& game = Game::getInstance();

    //Copy new grid in gridstore if in tournament mode
    if (gametype == TOURNAMENT)	//tournament
    {
	    for (int y=0;y<gridysize;++y)
		    for (int x=0;x<gridxsize;++x)
    		    gridStore[curplay][x][y] = game.getGrid()[x][y];

	    for (int i=0;i<5;++i)
        {
    	    ComStore[curplay*5+i].Exist = Company[i].Exist;
            ComStore[curplay*5+i].Value = Company[i].Value;
        }
    }
    ++curplay;
    if (curplay+1>numplays) curplay=0;	//goto next player

    //Copy new grid from gridstore
    if (gametype == TOURNAMENT)	//tournament
    {
	    for (int y=0;y<gridysize;++y)
		    for (int x=0;x<gridxsize;++x)
                game.getGrid()[x][y] = gridStore[curplay][x][y];

	    for (int i=0;i<5;++i)
        {
    	    Company[i].Exist = ComStore[curplay*5+i].Exist;
            Company[i].Value = ComStore[curplay*5+i].Value;
        }
    }

    switch (gametype)
    {
    	case CLASSIC:
	    	++turn;
        	if (turn == maxturns) progress=false;
    		break;

    	case TOURNAMENT:
		    if (curplay == 0) ++turn;
        	if ( (curplay == 0) && (turn == maxturns+1) ) progress=false;
    		break;
    }

    if (!progress) {	//END OF GAME!
	    Finish();
 	    HighScores(true);
        CreateComs();	//This should set all companies' existance to false
	    MainUpdate();
	    MenuUpdate();
    } else go();
}

//------------------------------------------------------------------------------
void CreateGrid() {
	Game::getInstance().getGrid().randomise();
    redrawGrid();
}
//------------------------------------------------------------------------------

void redrawGrid() {
	GameGrid& grid = Game::getInstance().getGrid();

	for (int dwn = 0; dwn<gridysize; dwn++) {
    	for (int acc=0; acc<gridxsize; acc++) {
            Main->ImageList1->Draw(Main->Screen->Canvas, acc*blwid, dwn*blhit, iStart + ( (acc+dwn)%2));

			if (grid[acc][dwn] == 1) //Circle
            	Main->ImageList1->Draw(Main->Screen->Canvas, acc*blwid, dwn*blhit, iStart+2);
			else if (grid[acc][dwn] == 2) //Diamond
            	Main->ImageList1->Draw(Main->Screen->Canvas, acc*blwid, dwn*blhit, iStart+3);
			else if (grid[acc][dwn] > 2) //Company
            	Main->ImageList1->Draw(Main->Screen->Canvas, acc*blwid, dwn*blhit, iStart+1+grid[acc][dwn]);
        }
    }

    DrawSels();

   	Main->Screen->Refresh();
}

//---------------------------------------------------------------------------
void __fastcall TMain::FormCreate(TObject *Sender)
{
	setGridSize(SMALL);
    CreateGrid();
    CreateComs();

    /**
    * Create a dummy record so that MainUpdate() doesn't hang (it needs a player name)
    */
    Player = new Record[1];
    Player[0].Name = "";

    //----- Load and set preferences if we can find the file
//    Prefs *pref = new Prefs();
//    if (pref->load("Preferences")) {
//   	currency = pref->getCurrencyUnit();
//        forceMergers = pref->mergersSet();
//        forceExpansion = pref->expansionSet();
//        hideDetails = pref->detailsSet();
//        allowCreation = pref->companiesSet();
//    }

    MainUpdate();
}
//---------------------------------------------------------------------------
void __fastcall TMain::NewGame1Click(TObject *Sender) {
    PlayInfo->Show();
    Main->Enabled=false;
    UpdateButs();
}
//---------------------------------------------------------------------------
void __fastcall TMain::CreateNewGrid1Click(TObject *Sender) {
    CreateGrid();
}
//---------------------------------------------------------------------------
void go() {
	GameGrid& grid = Game::getInstance().getGrid();

	int x,y;
    int xa,ya;

	sels=false; // Turn off selection drawing before redrawing grid
	redrawGrid();

    MoneyShow();

	//Is it ok to offer chances to create new companies?
	bool newok=false;

	for (int i=0;i<5;++i)
		if (!Company[i].Exist) newok=true;

	//Choose five squares that will be highlighted
	for (int i=0;i<5;++i) {
		redo:
    	bool cord=false; //Set to true if a circle or diamond surround a choice
    	bool ntocom=false;	//Set to true if choice is next to company
    	Choice[i][0]=rand() % gridxsize;
		Choice[i][1]=rand() % gridysize;


		//Ensure we are not offering an illegal selection...
		if (grid[Choice[i][0]][Choice[i][1]] != 0)	//Space already
        	goto redo;									//occupied...

    	if (i>0) {
    		for (int j=0; j<i; ++j) {
    			if ( (Choice[i][0] == Choice[j][0]) &&
                     (Choice[i][1] == Choice[j][1])) {
    				goto redo;	//Choice already chosen (out of the five)
                }
            }
        }

    	if (!newok) { //not ok to create new company
 			x = Choice[i][0], y = Choice[i][1];
    		xa = 0, ya = -1;

    		for (int k=0; k<5; ++k) {
	  			if (!( (x==0) && (xa==-1) ) &&
                    !( (y==0) && (ya==-1) ) &&
   	  		        !( (x==gridxsize-1) && (xa==1) ) &&
      		        !( (y==gridysize-1) && (ya==1) ) ) //ie. we're allowed to check the square
        		{
        			if ( (grid[x+xa][y+ya] == 1) || (grid[x+xa][y+ya] == 2) )
                    	cord=true; //cord = circle or diamond
            		if ( grid[x+xa][y+ya] > 2 ) //Choice next to company so is ok
						ntocom=true; //ntocom = next to company
				}

        		++xa;
        		if (xa>1) xa=-1;
            	if (xa == -1) ya=0;
            	if (xa == 0) ya=1;
            	if (xa == 1) ya=0;
			}

    		if ( (!ntocom) && (cord) ) {
        		goto redo; //Choice not next to company but next to a diamond or circle
            }
  		}
	}

	sels=true; // Enable selection drawing

	//Draw Possible selections...
	DrawSels();

    Main->Timer1->Enabled=true; // Timer enabled
    MainUpdate();

}

//================================================================

void __fastcall TMain::Timer1Timer(TObject *Sender) {
	//Redraw old selection
   	Main->Screen->Canvas->Brush->Color = clBlue;
   	Main->Screen->Canvas->Pen->Color = clBlack;

	int x = Choice[Main->Timer1->Tag][0]*(blwid)-1;
	int y = Choice[Main->Timer1->Tag][1]*(blhit)-1;
	Main->ImageList1->Draw(Main->Screen->Canvas, x+1, y+1, iStart+9);
    Timer1->Tag = (Timer1->Tag < 4 ? Timer1->Tag+1 : 0);

    //Draw new selection
   	Main->Screen->Canvas->Pen->Color = clYellow;

	x = Choice[Main->Timer1->Tag][0]*(blwid)-1;
	y = Choice[Main->Timer1->Tag][1]*(blhit)-1;
	Main->ImageList1->Draw(Main->Screen->Canvas, x+1, y+1, iStart+10);

	Main->Screen->Refresh();

}
//---------------------------------------------------------------------------
void DrawSels() {
    if (sels) { // We SHOULD draw selections
    	Main->Screen->Canvas->Brush->Color = clBlue;

    	int x,y;

    	for (int i=0; i<5; ++i) {
    		x=Choice[i][0]*blwid;
			y=Choice[i][1]*blhit;
			Main->ImageList1->Draw(Main->Screen->Canvas, x, y, iStart+9);

		}

		Main->Screen->Refresh();
	}
}
//==================================
void EraseChoices() {
	for (int i=0; i<5; ++i) {
		Choice[i][0] = (Choice[i][1] = -1);
    }
}
//==================================
void action(int x, int y)
{
	GameGrid& grid = Game::getInstance().getGrid();

	bool next[5]={false,false,false,false,false}; //An array to hold what company's are adjoining
	int diamond = 0;	//The number of adjoining diamonds
	int circle  = 0;	//The number of adjoining circles
	int comnext =-1;	//The company number that adjoins the choice
                        //(eg. next to company no. 2)

    int comadj = comAdjoining(x, y, circle, diamond, next, comnext);

	if (comadj == 0) {	//Choice not next to any existing company's
		if ( (circle>0) || (diamond>0) ) {
    		//New company formed
			NewCompany(x,y);
    	} else {
    		grid[x][y] = 2;
    		redrawGrid();
    	}
	}

	if (comadj == 1) {//Choice only next to one company
		grid[x][y] = comnext+3;
    	int val = UpdateGrid(x,y);
    	Company[comnext].Value += val;

		if (gametype == CLASSIC) {
			for (int i=0;i<numplays;++i) {
    			Player[i].Money += Player[i].Shares[comnext]*val;
            }
        }

    	if (gametype == TOURNAMENT) {	//in tourn. mode only increase money of current player
            Player[curplay].Money += Player[curplay].Shares[comnext]*val;
        }

    	if (Company[comnext].Value >= 3200) {
        	redrawGrid();
    		ComSplit(comnext);
        }

    	redrawGrid();
    	MoneyShow();
	}

	if (comadj>1) {	//Choice chosen between two company's! (MERGER)
		int coma,comb; //The two merging companies

		int i;
		for (i=0;i<5;++i) {
			if (next[i]==true) { coma=i; break; }
        }

		for (int j=i+1;j<5;j++) {
			if (next[j]==true) { comb=j; break; }
        }

		int comasize = ComSize(coma);	//Get sizes of the
		int combsize = ComSize(comb);	//two company's
		int newcom; //The number of the new company

		//Make coma the new company and comb the old company...
		if (combsize > comasize) {
        	newcom  = coma;
			coma    = comb;
			comb    = newcom;
        }

		//Now company a should be the new company, company b the old one
		grid[x][y] = 2;
    	redrawGrid();
 		Merger(coma,comb);
    	grid[x][y] = coma+3;
    	Spread(x,y,coma+3,comb+3);
    	Company[comb].Exist = false;
		action(x,y); // Check for another merger
	}

	MainUpdate();
    redrawGrid();
}

/**
* Function to return the number of companies next to a particular square.
* Also find's the number of adjoining circles and diamonds and stores them
* in circle and diamond respectively.
* @param x,y the x,y coords to check round
* @param circle an int where the number of circles should be stored
* @param diamond an int where the number of diamonds should be stored
*/
int comAdjoining(int x, int y, int& circle, int& diamond, bool *next, int& comnext)
{
	GameGrid& grid = Game::getInstance().getGrid();

    //An array to hold what company's are adjoining
    for (int i=0; i<5; i++) {
        next[i] = false;
    }

	int comadj  = 0;	//The number of adjoining company's
	diamond     = 0;	//The number of adjoining diamonds
	circle      = 0;	//The number of adjoining circles
	comnext     =-1;	//The company number that adjoins the choice
                        //(eg. next to company no. 2)

	int xa = 0, ya = -1;//What to add to to x and y in checks...

	for (int i=0; i<4; ++i) {
    	if (!( (x==0) && (xa==-1) ) &&
            !( (y==0) && (ya==-1) ) &&
 		    !( (x==gridxsize-1) && (xa==1) ) &&
		    !( (y==gridysize-1) && (ya==1) ) ) //Make sure coords are in bounds
    	{
    		switch ( grid[x + xa][y + ya] )
    		{
    			case 0:
    				break;
    			case 1:
    				++circle;
    				break;
    			case 2:
    				++diamond;
        			break;
    			default:
					if ( !(next[ (grid[x+xa][y+ya])-3 ]) ) ++(comadj);
                    next[ (grid[x+xa][y+ya])-3 ] = true;
 					comnext= grid[x+xa][y+ya]-3;
            		break;
        	}
    	}

		++xa;
		if (xa>1) {
            xa=-1;
        }

        ya = (xa == 0) ? 1 : 0;
/*		switch (xa) {
			case -1:
				ya=0;
				break;
			case 0:
				ya=1;
				break;
			case 1:
				ya=0;
				break;
        }
*/
	}

    return comadj;
}
//====================================
void Spread(int x,int y,int to,int from)
{
	GameGrid& grid = Game::getInstance().getGrid();

	for (int j=0;j<gridysize;++j) {
		for (int i=0;i<gridxsize;++i) {
    		if (grid[i][j] == from) grid[i][j]=to;
        }
    }
}
//======================================
int UpdateGrid(int x,int y) {
	GameGrid& grid = Game::getInstance().getGrid();

	int xa = 0, ya = -1;
	int value=100; //Stores the value of the square

	for (int i=0; i<4; ++i) {
    	if (!( (x==0) && (xa==-1) ) &&
            !( (y==0) && (ya==-1) ) &&
 		    !( (x==gridxsize-1) && (xa==1) ) &&
		    !( (y==gridysize-1) && (ya==1) ) ) //Make sure coords are in bounds
    	{
    		switch ( grid[x + xa][y + ya] )
    		{
    			case 1:	//circle
    				value += 500 ;
    				break;
    			case 2:	//diamond
    				value += 100 ;
            		grid[x+xa][y+ya] = grid[x][y];
        			break;
        	}
    	}

		++xa;
		if (xa>1) xa=-1;

        ya = (xa == 0) ? 1 : 0;
/*
        switch (xa) {
            case -1:
                ya=0;
                break;
            case 0:
				ya=1;
				break;
            case 1:
				ya=0;
				break;
        }
*/
    }

	return value;
}
//=====================================
void __fastcall TMain::AtlantisClick(TObject *Sender)
{
	Info(0);
}
//---------------------------------------------------------------------------
void __fastcall TMain::BetelgueseClick(TObject *Sender)
{
	Info(1);
}
//---------------------------------------------------------------------------
void __fastcall TMain::CannonClick(TObject *Sender)
{
	Info(2);
}
//---------------------------------------------------------------------------
void __fastcall TMain::DebeneseClick(TObject *Sender)
{
	Info(3);
}
//---------------------------------------------------------------------------
void __fastcall TMain::EridnusClick(TObject *Sender)
{
	Info(4);
}
//---------------------------------------------------------------------------
void MainUpdate()
{
	Main->BitBtn1->Enabled = !sels & progress;

	Main->Atlantis->Enabled=Company[0].Exist;
	Main->Betelguese->Enabled=Company[1].Exist;
	Main->Cannon->Enabled=Company[2].Exist;
	Main->Debenese->Enabled=Company[3].Exist;
	Main->Eridnus->Enabled=Company[4].Exist;

	//Create New Sidebar (Put in player's name + Money etc.)
	Main->Label5->Caption = Player[curplay].Name;
	Main->Label5->Left= (Main->Panel2->Width - Main->Label5->Width)/2;

    Main->Panel3->Caption = turn;
}
//=========================
void MoneyShow()
{
	//Convert player's money to a string then display it
    char cash[64];
   	Player[curplay].Money.output(cash);
    comma(cash);
    AnsiString full(cash);

//   	_i64toa(Player[curplay].Money,cash,10);

//    full  Player[curplay].Money;

//	comma(cash);
//	comma(full.c_str());


    full = Game::getInstance().getPrefs().getCurrencyUnit().output(cash);

    Main->Label6->Caption = full;
	Main->Label6->Left= ((Main->Panel2->Width)/2) - ((Main->Label6->Width)/2);

	//Draw dots next to companies that player can buy shares in
    large dosh = Player[curplay].Money;
    if ((dosh>=Company[0].Value) && (Company[0].Exist)) {
    	Main->Shape6->Pen->Color=clBlack;
    	Main->Shape6->Brush->Color=clWhite;
    } else {
    	Main->Shape6->Pen->Color=Company[0].Color;
    	Main->Shape6->Brush->Color=Company[0].Color;
    }

    if ((dosh>=Company[1].Value) && (Company[1].Exist)) {
       Main->Shape7->Pen->Color=clBlack;
    	Main->Shape7->Brush->Color=clWhite;
    } else {
    	Main->Shape7->Pen->Color=Company[1].Color;
    	Main->Shape7->Brush->Color=Company[1].Color;
    }

	if ((dosh>=Company[2].Value) && (Company[2].Exist)) {
       Main->Shape8->Pen->Color=clBlack;
    	Main->Shape8->Brush->Color=clWhite;
    } else {
    	Main->Shape8->Pen->Color=Company[2].Color;
    	Main->Shape8->Brush->Color=Company[2].Color;
    }

	if ((dosh>=Company[3].Value) && (Company[3].Exist)) {
       Main->Shape9->Pen->Color=clBlack;
    	Main->Shape9->Brush->Color=clWhite;
    } else {
    	Main->Shape9->Pen->Color=Company[3].Color;
    	Main->Shape9->Brush->Color=Company[3].Color;
    }

	if ((dosh>=Company[4].Value) && (Company[4].Exist)) {
    	Main->Shape10->Pen->Color=clBlack;
    	Main->Shape10->Brush->Color=clWhite;
    } else {
    	Main->Shape10->Pen->Color=Company[4].Color;
    	Main->Shape10->Brush->Color=Company[4].Color;
    }

    ComInfo->ToolButton1->Enabled = Company[0].Exist;
	ComInfo->ToolButton2->Enabled = Company[1].Exist;
	ComInfo->ToolButton3->Enabled = Company[2].Exist;
	ComInfo->ToolButton4->Enabled = Company[3].Exist;
	ComInfo->ToolButton5->Enabled = Company[4].Exist;

}
//========================
int ComSize(int com)
{
	GameGrid& grid = Game::getInstance().getGrid();

	int size=0; //size of company in squares
	for (int j=0;j<gridysize;++j)
		for (int i=0; i<gridxsize; ++i)
			if ( grid[i][j] == com+3) ++size;

	return size;
}


void __fastcall TMain::AboutSpaceMonopoly1Click(TObject *Sender)
{
	About->ShowModal();
}
//---------------------------------------------------------------------------
void __fastcall TMain::ViewHighScores1Click(TObject *Sender)
{
	HighScores(false);
}
//---------------------------------------------------------------------------
void MenuUpdate()
{
		Main->CreateNewGrid1->Enabled = !progress;	//Disabled when game in progress
		Main->GridSize1->Enabled = !progress;	//Disabled when game in progress
        Main->EndGame1->Enabled = progress;	//Enabled when game in progress
        Main->NewGame1->Enabled = !progress;
        Main->Preferences1->Enabled = !progress;

}
//==========================================================================
bool closemain(int mode)	//SURELY NOT??? NOooooo!!! Don't go!!
{
	int sure;

	if (progress)
    	sure = MessageBox(NULL, "If you quit now your score will not be recorded in the High Scores Table. "
        			" Click 'Yes' to quit Space Monopoly now, or 'No' to"
					" continue playing.",
        			"Are you sure you want to quit?", MB_ICONEXCLAMATION|MB_TASKMODAL|MB_YESNO);

    else
    	sure = MessageBox(NULL, "Are you sure you want to quit?"
          			"  Click 'Yes' to quit Space Monopoly now, or 'No' to"
                    " continue playing.",
        			"Are you sure you want to quit?", MB_ICONINFORMATION|MB_TASKMODAL|MB_YESNO);

	if (sure == IDYES) {
    	if (mode == 1) return true;
    	delete[] Player;
		delete[] Company;

    	Application->Terminate();
    	//Main->Close(); <-This gives stack overflow
    } else return false;

	return false;
}
//==============================
void __fastcall TMain::Quit1Click(TObject *Sender)
{
	closemain(0);
}
//---------------------------------------------------------------------------
void __fastcall TMain::EndGame1Click(TObject *Sender)
{

	//Get rid of choice boxes
	Main->Timer1->Enabled=false;
    sels=false;
    EraseChoices();
    redrawGrid();

    progress=false;

    //Show final standings and still allow a chance to get into highscore table
	Finish();
 	HighScores(true);

    CreateComs();	//This should set all companies' existance to false
	MainUpdate();
	MenuUpdate();
}
//---------------------------------------------------------------------------
void __fastcall TMain::ListBox1DblClick(TObject *Sender)
{
	Prefs& prefs = Game::getInstance().getPrefs();

	if (!prefs.detailsSet())
    {
   		char cash[64];
        Player[ListBox1->ItemIndex].Money.output(cash);
//    	strset(cash,'\0');

//   		_i64toa(Player[ListBox1->ItemIndex].Money,cash,10);
		comma(cash);
    	playerDetailsForm->Label3->Caption = prefs.getCurrencyUnit().output(cash);

    	int pnum = ListBox1->ItemIndex;

    	playerDetailsForm->GroupBox1->Caption = "Details of " + Player[pnum].Name;
		playerDetailsForm->ListView1->Items->Clear();
		playerDetailsForm->ListView1->Items->Add();

    	int comnum = 0;
    	large wealth = 0;

    	for (int i=0; i<5; i++)
    	{
    		if (Company[i].Exist)
        	{
        		char *string = new char[64];
     			playerDetailsForm->ListView1->Items->Add();

          		playerDetailsForm->ListView1->Items->Item[comnum]->Caption = Company[i].Name;
            	large numOfShares = Player[pnum].Shares[i];
//				_i64toa(numOfShares, string, 10);
                numOfShares.output(string);
				comma(string);
        		playerDetailsForm->ListView1->Items->Item[comnum]->SubItems->Add(string);
            	strset(string, '\0');

            	large worthOfShares = numOfShares * Company[i].Value;
            	worthOfShares.output(string);
            	comma(string);
            	playerDetailsForm->ListView1->Items->Item[comnum]->SubItems->Add(prefs.getCurrencyUnit().output(string));
            	strset(string, '\0');

            	wealth += worthOfShares;

           		comnum++;

	        }

    	}

        wealth.output(cash);
    	comma(cash);

    	playerDetailsForm->Label5->Caption = prefs.getCurrencyUnit().output(cash);

    	//strset(cash, '\0');
    	//_i64toa(wealth + Player[pnum].Money, cash, 10);

        (wealth + Player[pnum].Money).output(cash);
    	comma(cash);

		playerDetailsForm->Label7->Caption = prefs.getCurrencyUnit().output(cash);

    	playerDetailsForm->ShowModal();
    } else {
    	MessageBox(NULL, "Player details are hidden due to the preferences set for this game...",
        			"Game Details", MB_ICONASTERISK|MB_TASKMODAL);
    }

}
//---------------------------------------------------------------------------
void __fastcall TMain::N16by16Click(TObject *Sender)
{
    N16by16->Checked = true;
	setGridSize(SMALL);
}
//---------------------------------------------------------------------------

void __fastcall TMain::N20by20Click(TObject *Sender)
{
    N20by20->Checked = true;
	setGridSize(MEDIUM);
}
//---------------------------------------------------------------------------

void __fastcall TMain::N32by32Click(TObject *Sender)
{
    N32by32->Checked = true;
    setGridSize(LARGE);
}
//---------------------------------------------------------------------------

void setGridSize(int size)
{
    Main->Screen->Canvas->Brush->Color = Main->ScreenContainer->Color;
   	Main->Screen->Canvas->Pen->Color = Main->ScreenContainer->Color;
    Main->Screen->Canvas->Rectangle(0,0,Main->Screen->Width, Main->Screen->Height);

	switch (size)
    {
    	case SMALL:
        	iStart = 0;
			blwid=37, blhit=40, gridxsize = 16, gridysize = 12;
            gridtype = SMALL;
            maxturns = 80;
            break;
        case MEDIUM:
        	iStart = 11;
			blwid=27, blhit=27, gridxsize = 22, gridysize = 18;
            gridtype = MEDIUM;
            maxturns = 120;
            break;
        case LARGE:
      		iStart = 22;
			blwid=21, blhit=20, gridxsize = 28, gridysize = 24;
            gridtype = LARGE;
            maxturns = 160;
            break;
    }

	Game::getInstance().getGrid().resize(gridxsize, gridysize);
	Game::getInstance().getGrid().randomise();


    Main->Screen->SetBounds(0, 0, blwid*gridxsize,blhit*gridysize);
    redrawGrid();
}

void __fastcall TMain::Preferences1Click(TObject *Sender)
{
	preferencesForm->Show();
    Main->Enabled = false;
}

void __fastcall TMain::Image1MouseDown(TObject *Sender,
      TMouseButton Button, TShiftState Shift, int X, int Y)
{
	Prefs& prefs = Game::getInstance().getPrefs();

	if (progress) {
		int xbl=X/blwid;
		int ybl=Y/blhit;

		for (int i=0; i<5; ++i) {
			if ( (Choice[i][0] == xbl) && (Choice[i][1] == ybl) ) {
				//They've clicked on an available choice
				if (prefs.mergersSet() || prefs.expansionSet())
                {
                	bool condM = false; //condition for a square to be placed
                    bool condE = false;
    				bool next[5]={false,false,false,false,false}; //An array to hold what company's are adjoining
					int diamond=0;	//The number of adjoining diamonds
					int circle=0;	//The number of adjoining circles
					int comnext=-1;	//The company number that adjoins the choice
									//(eg. next to company no. 2)
					int circleH=0, diamondH=0; //circles/diamonds around chosen square

				    int comadj = comAdjoining(xbl, ybl, circleH, diamondH, next, comnext);

                    for (int ch=0;ch<5;ch++)
					{
                    	int comhere = comAdjoining(Choice[ch][0], Choice[ch][1],circle, diamond, next, comnext);
                     	if (prefs.mergersSet() && comhere > 1)
                        	condM = true;
                        if (prefs.expansionSet() && comhere>0)
                        	condE = true;
					}

                    if (prefs.mergersSet() && condM && comadj<2) //they haven't merged
                    {
                    	MessageBox(NULL,"You must choose a square that causes a merger",
        				"Game Information...",MB_ICONEXCLAMATION|MB_TASKMODAL);
                    	break;
                    }
                    if (prefs.expansionSet() && condE && (prefs.companiesSet() ?
                    								circleH==0 && diamondH==0 && comadj==0 :
								                    comadj==0)) //they haven't expanded
	                {
                    	if (prefs.companiesSet())
							MessageBox(NULL,"You must choose a square that expands a company (or creates a new one)",
                        	"Game Information...",MB_ICONEXCLAMATION|MB_TASKMODAL);
                    	else
							MessageBox(NULL,"You must choose a square that expands a company",
	        				"Game Information...",MB_ICONEXCLAMATION|MB_TASKMODAL);
                    	break;
                    }

                }

				Main->Timer1->Enabled=false;
    			sels=false;
				EraseChoices();
				action(xbl,ybl);
        		break;
        	}
    	}
    }

}
//---------------------------------------------------------------------------

void __fastcall TMain::Image1MouseMove(TObject *Sender, TShiftState Shift,
      int X, int Y)
{
	bool prevhand = hand;

	if (progress)
    {
		int xbl=X/blwid;
		int ybl=Y/blhit;

        hand = false;

        for (int i=0; i<5; ++i)
			if ( (Choice[i][0] == xbl) && (Choice[i][1] == ybl) )
         		hand = true;

        if (hand && !prevhand)
        	Main->Screen->Cursor = crHandPoint;
        else if (!hand && prevhand)
			Main->Screen->Cursor = crArrow;
    }

}
//---------------------------------------------------------------------------

void __fastcall TMain::FormCloseQuery(TObject *Sender, bool &CanClose)
{
    CanClose = closemain(1);

}
//---------------------------------------------------------------------------

void __fastcall TMain::Panel3Click(TObject *Sender)
{
GridManagerForm->Show();
}
//---------------------------------------------------------------------------

