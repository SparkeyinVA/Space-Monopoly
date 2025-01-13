//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop
#include <string.h>
#include <stdlib.h>

#include "SMMain.h"
#include "Merger.h"
#include "CommaIt.h"
#include "CurrencyUnit.h"
#include "Game.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TMergForm *MergForm;
//---------------------------------------------------------------------------
__fastcall TMergForm::TMergForm(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------

void Merger(int coma,int comb)
{
	Game& game = Game::getInstance();

	MergForm->Label6->Caption = "There has been a merger between "
    			+ Company[coma].Name + " and " + Company[comb].Name;

	MergForm->Label7->Caption = "The new company will take the name of " +
    														Company[coma].Name;

	Company[coma].Value += Company[comb].Value;

	char cash[20];
	ltoa(Company[coma].Value,cash,10);
	comma(cash);
	MergForm->Label8->Caption="The cost of " + Company[coma].Name
    						+ " shares will be "	+ game.getPrefs().getCurrencyUnit().output(cash);

	MergForm->ListView1->Columns->Items[1]->Caption="Shares in " + Company[coma].Name;
	MergForm->ListView1->Columns->Items[2]->Caption="Shares in " + Company[comb].Name;

	MergForm->ListView1->Items->Clear();
	large profit;
	int i=0,j=0;
	if (gametype == 1) i=curplay;
	//----------
	loop:

	MergForm->ListView1->Items->Add();
	MergForm->ListView1->Items->Item[j]->Caption=Player[i].Name;

	//---Shares in company A
//	_i64toa(Player[i].Shares[coma],cash,10);
    Player[i].Shares[coma].output(cash);
	comma(cash);
	MergForm->ListView1->Items->Item[j]->SubItems->Add(cash);

	//---Shares in company B
//	_i64toa(Player[i].Shares[comb],cash,10);
    Player[i].Shares[comb].output(cash);
	comma(cash);
	MergForm->ListView1->Items->Item[j]->SubItems->Add(cash);

	//---Calculate and Display Money earnt
	profit= (Player[i].Shares[comb])*(Company[comb].Value);
//	_i64toa(profit,cash,10);
    profit.output(cash);
	comma(cash);
	Player[i].Money += profit;
	MergForm->ListView1->Items->Item[j]->SubItems->Add(game.getPrefs().getCurrencyUnit().output(cash));

	if (gametype == CLASSIC)
	{
		++i;
    	++j;
		if (i<numplays) goto loop;
	}
	//------------


	MoneyShow();
	MergForm->ShowModal();

    /*
	int start,end;
	if (gametype == CLASSIC) 	{start=0; end=numplays;}
	if (gametype == TOURNAMENT)	{start=curplay; end=curplay;}
    */
	for (int i = (gametype==CLASSIC ? 0 : curplay);
    		i < (gametype==CLASSIC ? numplays : curplay);
            ++i)
		Player[i].Shares[coma] += Player[i].Shares[comb];

}
//===============================
