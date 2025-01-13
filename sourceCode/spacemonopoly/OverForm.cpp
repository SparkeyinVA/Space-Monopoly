//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include <stdlib.h>

#include "OverForm.h"
#include "SMMain.h"
#include "CommaIt.h"
#include "HighScores.h"
#include "CurrencyUnit.h"
#include "Game.h"
#include "large.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TGameOver *GameOver;
//---------------------------------------------------------------------------
__fastcall TGameOver::TGameOver(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void Finish()
{
	CurrencyUnit& currency = Game::getInstance().getPrefs().getCurrencyUnit();

	char cash[20];	//Some string buffers

    //--- Output the "At go number 80 the game is over string
    GameOver->Label2->Caption =  "At go number " + AnsiString(turn) +
		" the game is finally over. Below lists the game's final standings...";



	//--- Biggest Company Info
	int bigcom=0,csize=0;//Biggest company number and company size

	for (int i=0; i<5; ++i) {
		if (ComSize(i) > csize)
    	{
    		csize=ComSize(i);
    		bigcom=i;
    	}
    }

    GameOver->Label7->Caption=Company[bigcom].Name;
	ltoa(csize,cash,10);
	comma(cash);
	GameOver->Label8->Caption = AnsiString(cash) + " squares";

	//--- Winning Player Info
	large dosh  = 0;	//Net wealth of winner
	int play    = 0;	//Winning player number

	for (int i=0; i<numplays; ++i) {
		if (dosh < Total(i)) {	//Current player richest
    		play = i;
        	dosh = Total(i);
        }
    }

	//--- Display winning player info
	GameOver->Label9->Caption = Player[play].Name;
    dosh.output(cash);
	comma(cash);
	GameOver->Label10->Caption = currency.output(cash);

	//--- Player Information
	GameOver->ListView1->Items->Clear();

	large netwealth, worth;

	for (int i=0; i<numplays; ++i) {
		GameOver->ListView1->Items->Add();
		GameOver->ListView1->Items->Item[i]->Caption=Player[i].Name;

		//--- Money Left over
    	//_i64toa(Player[i].Money,cash,10);
        Player[i].Money.output(cash);
		comma(cash);
		GameOver->ListView1->Items->Item[i]->SubItems->Add(currency.output(cash));

		//---Worth Of shares
		for (int j=0; j<5; ++j) {
   			worth += Player[i].Shares[j] * Company[j].Value;
        }

		//_i64toa(worth,cash,10);
        worth.output(cash);
		comma(cash);
		GameOver->ListView1->Items->Item[i]->SubItems->Add(currency.output(cash));

		//---Net Wealth
		netwealth=worth+Player[i].Money;
		//_i64toa(netwealth,cash,10);
        netwealth.output(cash);
		comma(cash);
		GameOver->ListView1->Items->Item[i]->SubItems->Add(currency.output(cash));
	}

	GameOver->ShowModal();
}
//===================================================


