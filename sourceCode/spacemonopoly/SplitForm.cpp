//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include <stdlib.h>

#include "SMMain.h"
#include "CommaIt.h"
#include "SplitForm.h"
#include "CurrencyUnit.h"
#include "Game.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TSplit *Split;
//---------------------------------------------------------------------------
__fastcall TSplit::TSplit(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void ComSplit(int com)
{
	Game& game = Game::getInstance();

	char cash[20];
    AnsiString string;

	//---Shares in x have split message---
	Split->Label6->Caption = "Shares in " + Company[com].Name + " have split two for one.";

	//---New price message---
	string = "The previous share price of ";// + currency->getUnit();
	ltoa(Company[com].Value,cash,10);
	comma(cash);
	string += game.getPrefs().getCurrencyUnit().output(cash) + " has been changed to ";
	Company[com].Value /= 2;
	ltoa(Company[com].Value,cash,10);
	comma(cash);
	string += game.getPrefs().getCurrencyUnit().output(cash);
	Split->Label7->Caption = string;

	//----------Player info---------------
	Split->ListView1->Items->Clear();
	int i=0,j=0;
	if (gametype == 1) i=curplay;
	//--------
	loop:

	//---Insert player's name
	Split->ListView1->Items->Add();
	Split->ListView1->Items->Item[j]->Caption=Player[i].Name;

    //---Shares Before
	//_i64toa(Player[i].Shares[com],cash,10);
    Player[i].Shares[com].output(cash);
    comma(cash);
	Split->ListView1->Items->Item[j]->SubItems->Add(cash);

	Player[i].Shares[com] *= 2;

    //---Shares After
	//_i64toa(Player[i].Shares[com],cash,10);
    Player[i].Shares[com].output(cash);
    comma(cash);
	Split->ListView1->Items->Item[j]->SubItems->Add(cash);

	if (gametype == 0)
	{
		++i;
    	++j;
 		if (i<numplays) goto loop;
	}
	Split->ShowModal();
}
//===========================
