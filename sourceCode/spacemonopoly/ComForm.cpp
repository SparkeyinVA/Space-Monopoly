//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include <stdlib.h>

#include "ComForm.h"
#include "SMMain.h"
#include "Game.h"
#include "GameGrid.h"
#include "Players.h"
#include "ComInfoForm.h"
#include "CurrencyUnit.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TNewCom *NewCom;
//---------------------------------------------------------------------------
__fastcall TNewCom::TNewCom(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void NewCompany(int x,int y)
{
	Game& game = Game::getInstance();
	GameGrid& grid = game.getGrid();

	for (int i=0;i<5;++i) {
    	if (!Company[i].Exist) {
    		grid[x][y]=i+3;
     		Company[i].Exist = true;
        	Company[i].Value = UpdateGrid(x,y);

        	if (gametype == 0)
        		for (int j=0; j<numplays; ++j)
        			Player[j].Shares[i] = 0;	//Reset players' shares to 0

     		Player[curplay].Shares[i] = 5;
        	Player[curplay].Money += Company[i].Value;

			NewCom->Label2->Caption = "The new company's name is " + Company[i].Name;
        	NewCom->Label2->Left=180-(NewCom->Label2->Width)/2;

        	NewCom->Label4->Caption="The shares will start at " +
			       	game.getPrefs().getCurrencyUnit().output(Company[i].Value) + " each.";
        	NewCom->Label4->Left=180-(NewCom->Label4->Width)/2;

        	NewCom->Label3->Caption=Player[curplay].Name +
		            	" will get five free shares in the company and " +
        		        game.getPrefs().getCurrencyUnit().output(Company[i].Value);
        	NewCom->Label3->Left=180-(NewCom->Label3->Width)/2;

        	redrawGrid();
			MoneyShow();
    		NewCom->ShowModal();
        	Info(i);
        	break;
		}
	}
}
//===============================


