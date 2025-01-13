//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include <stdlib.h>

#include "ComInfoForm.h"
#include "StockForm.h"
#include "SMMain.h"
#include "CommaIt.h";
#include "CurrencyUnit.h"
#include "Game.h"
#include "large.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
int comnum;
TComInfo *ComInfo;

//====PROTOTYPES....
void InfoButs();
void close(void);
//===========FILE GLOBALS
large maxbuy;

//---------------------------------------------------------------------------
__fastcall TComInfo::TComInfo(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void Info(int com)
{
	Game& game = Game::getInstance();

	comnum = com;
	ComInfo->Label1->Caption=Company[comnum].Name;
	ComInfo->Label1->Left=216-(ComInfo->Label1->Width)/2;
	ComInfo->Panel1->Color = Company[comnum].Color;

	char cash[20];

	ltoa(Company[comnum].Value,cash,10);
	comma(cash);
	ComInfo->Label8->Caption = game.getPrefs().getCurrencyUnit().output(cash);

	//_i64toa(Player[curplay].Shares[comnum],cash,10);
    Player[curplay].Shares[comnum].output(cash);

    comma(cash);
	ComInfo->Label7->Caption = "You own " + AnsiString(cash) +
    	(Player[curplay].Shares[comnum] != 1 ? " shares" : " share") +
    	" in this company";
    ComInfo->Label7->Left = (ComInfo->Width - ComInfo->Label7->Width)/2;

    maxbuy=(Player[curplay].Money)/(Company[comnum].Value);
    //_i64toa(maxbuy,cash,10);
    maxbuy.output(cash);

    comma(cash);

    ComInfo->Label6->Caption = "You can buy" +
    			AnsiString((maxbuy > 1 ? " up to " : " ")) + AnsiString(cash) +
    			(maxbuy != 1 ? " shares" : " share") + " in this company";
    ComInfo->Label6->Left = (ComInfo->Width - ComInfo->Label6->Width)/2;

    int size=ComSize(comnum);
    itoa(size,cash,10);
    if (size != 1)
    	strcat(cash," squares");
    else strcat(cash," square");
    ComInfo->Label3->Caption = cash;

	//Find Major shareholder
    large numshares=0;
    int player=0;
    for (int i=0; i<numplays; ++i)
    {
    	if (Player[i].Shares[comnum] > numshares)
        {
        	numshares = Player[i].Shares[comnum];
            player = i;
        }
    }

    if (numshares > 0)
    {
//    	_i64toa(Player[player].Shares[comnum],cash,10);
        Player[player].Shares[comnum].output(cash);
    	comma(cash);
    	ComInfo->Label9->Caption = Player[player].Name + " with " + cash +
        								(numshares > 1 ? " shares." : " share.");
    } else {
    	ComInfo->Label9->Caption = "Nobody.";
    }

    InfoButs();
	ComInfo->Show();
    Main->Enabled=false;

}
//==========================

void __fastcall TComInfo::BitBtn1Click(TObject *Sender)
{
	Main->Enabled=true;
   	ComInfo->Hide();
}
//---------------------------------------------------------------------------
void __fastcall TComInfo::BitBtn4Click(TObject *Sender)
{
	redo:
	--comnum;

	if (comnum<0) comnum=4;

	if (!Company[comnum].Exist) goto redo;

	Info(comnum);
}
//---------------------------------------------------------------------------
void __fastcall TComInfo::BitBtn5Click(TObject *Sender)
{
	redo:
	++comnum;

	if (comnum>4) comnum=0;

	if (!Company[comnum].Exist) goto redo;

    Info(comnum);

}
//---------------------------------------------------------------------------
void __fastcall TComInfo::BitBtn2Click(TObject *Sender)
{
	large amount(Edit2->Text.c_str());
	if ( (amount>maxbuy) || (amount<1) )
	{
		MessageBox(	NULL,"You can't buy that many shares you fraudster",
        				"I don't think so...",MB_ICONEXCLAMATION|MB_TASKMODAL);
    } else {
		Player[curplay].Shares[comnum] += amount;
		Player[curplay].Money -= (amount) * (Company[comnum].Value);
    	MoneyShow();
		Info(comnum);
    }

}
//---------------------------------------------------------------------------
void InfoButs()
{
	//The BuyShares/SellShares buttons aren't enabled until a blue square
	//has been chosen
	ComInfo->BitBtn2->Enabled=!sels;
	ComInfo->BitBtn3->Enabled=!sels;
    ComInfo->BitBtn7->Enabled=!sels;
    ComInfo->BitBtn8->Enabled=!sels;
}

//---------------------------------------------------------------------------
void __fastcall TComInfo::FormClose(TObject *Sender, TCloseAction &Action)
{
	close();
}
//---------------------------------------------------------------------------
void close(void)
{
	Main->Enabled=true;
   	ComInfo->Hide();
}

void __fastcall TComInfo::Edit1Enter(TObject *Sender)
{
	BitBtn3->Default=true;
	BitBtn2->Default=false;
}
//---------------------------------------------------------------------------

void __fastcall TComInfo::Edit2Enter(TObject *Sender)
{
	BitBtn3->Default=false;
	BitBtn2->Default=true;
}
//---------------------------------------------------------------------------



void __fastcall TComInfo::BitBtn3Click(TObject *Sender)
{
	large amount(Edit1->Text.c_str());

	if ( (amount>Player[curplay].Shares[comnum]) || (amount<1) )
	{
		MessageBox(	NULL,	"You can't sell that many shares you fraudster",
        						"I don't think so...",MB_ICONEXCLAMATION|MB_TASKMODAL);
	} else {
		Player[curplay].Shares[comnum] -= amount;
		Player[curplay].Money += (amount) * (Company[comnum].Value);
		MoneyShow();

		Info(comnum);
	}

}
//---------------------------------------------------------------------------
void __fastcall TComInfo::BitBtn7Click(TObject *Sender)
{
	Player[curplay].Money += (Player[curplay].Shares[comnum]) * (Company[comnum].Value);
	Player[curplay].Shares[comnum] = 0;

	MoneyShow();

	Info(comnum);
}
//---------------------------------------------------------------------------

void __fastcall TComInfo::BitBtn8Click(TObject *Sender)
{
	Player[curplay].Shares[comnum] += maxbuy;
	Player[curplay].Money -= (maxbuy) * (Company[comnum].Value);

	MoneyShow();

	Info(comnum);
}
//---------------------------------------------------------------------------

void __fastcall TComInfo::ToolButton1Click(TObject *Sender)
{
	Info(0);
}
//---------------------------------------------------------------------------

void __fastcall TComInfo::ToolButton2Click(TObject *Sender)
{
	Info(1);
}
//---------------------------------------------------------------------------

void __fastcall TComInfo::ToolButton3Click(TObject *Sender)
{
	Info(2);
}
//---------------------------------------------------------------------------

void __fastcall TComInfo::ToolButton4Click(TObject *Sender)
{
	Info(3);
}
//---------------------------------------------------------------------------

void __fastcall TComInfo::ToolButton5Click(TObject *Sender)
{
	Info(4);
}
//---------------------------------------------------------------------------

