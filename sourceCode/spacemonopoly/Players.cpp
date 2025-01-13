//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include "Game.h"
#include "SMMain.h"
#include "Players.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TPlayInfo *PlayInfo;
//=== PROTOTYPES..
void close(void);

//=====G L O B A L S===
GameGrid *gridStore;
struct ComRecord *ComStore;
//---------------------------------------------------------------------------
__fastcall TPlayInfo::TPlayInfo(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void __fastcall TPlayInfo::BitBtn1Click(TObject *Sender) {
	//Maximum name length=15 chars + NULL
    AnsiString name = Edit1->Text.Trim();
	if (name.Length() > 0)
		ListBox1->Items->Add(name);

	UpdateButs();
	Edit1->SelectAll();
	UpdateButs();
}
//---------------------------------------------------------------------------
void UpdateButs() {
	if (PlayInfo->ListBox1->Items->Count == 0) { //No names entered...
    	PlayInfo->BitBtn5->Enabled = false;	//Move Up
    	PlayInfo->BitBtn6->Enabled = false;	//Move Down
    	PlayInfo->BitBtn2->Enabled = false;	//Remove
		PlayInfo->BitBtn3->Enabled = false;	//Go!
    } else {
		PlayInfo->BitBtn3->Enabled = true;	//Go!
    	if (PlayInfo->ListBox1->ItemIndex != -1) {
            PlayInfo->BitBtn5->Enabled = true;	//Move Up
    		PlayInfo->BitBtn6->Enabled = true;	//Move Down
    		PlayInfo->BitBtn2->Enabled = true;	//Remove
        } else {
        	PlayInfo->BitBtn5->Enabled = false;	//Move Up
    		PlayInfo->BitBtn6->Enabled = false;	//Move Down
    		PlayInfo->BitBtn2->Enabled = false;	//Remove
        }
    }
}
//==================================
void __fastcall TPlayInfo::ListBox1Click(TObject *Sender) {
	UpdateButs();
}
//---------------------------------------------------------------------------
void __fastcall TPlayInfo::BitBtn2Click(TObject *Sender) {
	PlayInfo->ListBox1->Items->Delete(PlayInfo->ListBox1->ItemIndex);
	UpdateButs();
}
//---------------------------------------------------------------------------
void __fastcall TPlayInfo::BitBtn3Click(TObject *Sender) {
	gametype=PlayInfo->RadioGroup1->ItemIndex;	//Get game type
	numplays=PlayInfo->ListBox1->Items->Count;	//number of players

	//Only allow tournament if there are 2 or more players
	if ( (gametype == TOURNAMENT) && (numplays == 1) ) gametype=CLASSIC;

	Main->ListBox1->Clear();

	if (numplays>0) {
		Player = new struct Record[numplays];	//Reserve 'numplays' structures
		for (int i=0; i<numplays; ++i) {
    		Player[i].Money = 6000;
  			Player[i].Name = PlayInfo->ListBox1->Items->Strings[i];
			for (int j=0; j<5; ++j)
  				Player[i].Shares[j]=0;

			//Put players' names into Listview
    	    Main->ListBox1->Items->Add(Player[i].Name);
    	}

    	//Make numplays copies of the grid if game in tournament mode:
		//When the end of the turn comes, the current grid is copied into
    	//the current players gridstore, and the next players' grid is then
    	//used.

		Game::getInstance().getGrid().clear();
	    redrawGrid();

		progress=true;
		CreateComs();
    	MenuUpdate();
    	turn=0;
		curplay = 0;

    	if (gametype == TOURNAMENT) {	//Tournament
			//Create identical grids
        	gridStore = new GameGrid[numplays];

        	for (int i=0;i<numplays;++i) {
        		gridStore[i] = Game::getInstance().getGrid(); //the global grid
            }

        	//Create company info store
        	ComStore = new struct ComRecord[numplays*5];
        	for (int i=0;i<numplays; ++i) {
        		for (int j=0;j<5;++j) {
        			ComStore[i*5+j].Exist = false;
                	ComStore[i*5+j].Value = 0;
        		}
            }
        }

		go();
		Main->Enabled = true;
		PlayInfo->Hide();
	} //end if(numplays>0)
}
//---------------------------------------------------------------------------
void CreateComs() {
 	Company = new struct ComRecord[5];	//Reserve 5 spaces for company's

   	//Set Company names
	Company[0].Name = "Atlantis Ltd.";
	Company[1].Name = "Betelguese";
	Company[2].Name = "Cannon Inc.";
	Company[3].Name = "Debenese";
	Company[4].Name = "Eridnus Corp.";
	Company[0].Color=clRed;
	Company[1].Color=clLime;
	Company[2].Color=clYellow;
	Company[3].Color=clTeal;
	Company[4].Color=clBlue;

	for (int i=0; i<5;++i) {
    	Company[i].Value=0;
    	Company[i].Exist=false;
    }

	Main->Shape1->Brush->Color = Company[0].Color;
	Main->Shape2->Brush->Color = Company[1].Color;
	Main->Shape3->Brush->Color = Company[2].Color;
	Main->Shape4->Brush->Color = Company[3].Color;
	Main->Shape5->Brush->Color = Company[4].Color;
}
//===================
void __fastcall TPlayInfo::BitBtn4Click(TObject *Sender) {
	close();
}
//---------------------------------------------------------------------------
void close() {	//Player clicked 'Cancel' or Close button
	Main->Enabled = true;
	PlayInfo->Hide();
}
//===========================================================================
void __fastcall TPlayInfo::FormClose(TObject *Sender, TCloseAction &Action) {
	close();
}
//===========================================================================
void __fastcall TPlayInfo::BitBtn5Click(TObject *Sender) {
	int pos = ListBox1->ItemIndex;

	if (pos > 0) {
		char buffer[25];

		//copy item into buffer
		strcpy(buffer,ListBox1->Items->Strings[pos].c_str());
		ListBox1->Items->Strings[pos] = ListBox1->Items->Strings[pos-1];
    	ListBox1->Items->Strings[pos-1] = buffer;
    	//Reselect Item just moved
		ListBox1->ItemIndex=pos-1;
	}
}
//---------------------------------------------------------------------------
void __fastcall TPlayInfo::BitBtn6Click(TObject *Sender) {
	int pos = ListBox1->ItemIndex;

	if (pos < ListBox1->Items->Count - 1) {
        AnsiString buffer;

		//copy item into buffer
		buffer = ListBox1->Items->Strings[pos];
		ListBox1->Items->Strings[pos]   = ListBox1->Items->Strings[pos+1];
    	ListBox1->Items->Strings[pos+1] = buffer;
    	//Reselect Item just moved
		ListBox1->ItemIndex=pos+1;
	}
}
//---------------------------------------------------------------------------
void ComButs() {
	Main->Atlantis->Caption     = Company[0].Name;
	Main->Betelguese->Caption   = Company[1].Name;
	Main->Cannon->Caption       = Company[2].Name;
	Main->Debenese->Caption     = Company[3].Name;
	Main->Eridnus->Caption      = Company[4].Name;
}

