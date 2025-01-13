//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include <stdlib.h>

#include "StockForm.h"
#include "ComInfoForm.h"
#include "SMMain.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
TStock *Stock;
//---------------------------------------------------------------------------
__fastcall TStock::TStock(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void __fastcall TStock::BitBtn2Click(TObject *Sender)
{
	__int64 amount; // Number of shares to buy
	amount=atoi(Edit1->Text.c_str());
	if (Label2->Caption == "Buy")	//Player is buying shares
	{
		if ( (amount>maxbuy) | (amount<1) )
		{
			MessageBox(	NULL,"You can't buy that many shares you fraudster",
        				"I don't think so...",0);
		} else {
			Player[curplay].Shares[comnum] += amount;
			Player[curplay].Money -= (amount) * (Company[comnum].Value);
			ComInfo->Enabled=true;
			//	ComInfo->Hide();
     		Stock->Hide();

			MoneyShow();

			Info();
		}
	} else {	//Player is selling shares
		if ( (amount>Player[curplay].Shares[comnum]) | (amount<1) )
		{
			MessageBox(	NULL,	"You can't sell that many shares you fraudster",
        						"I don't think so...",0);
		} else {
			Player[curplay].Shares[comnum] -= amount;
			Player[curplay].Money += (amount) * (Company[comnum].Value);
			ComInfo->Enabled=true;
			//	ComInfo->Hide();
     		Stock->Hide();
			MoneyShow();

			Info();
		}


	}

}
//---------------------------------------------------------------------------
void __fastcall TStock::BitBtn1Click(TObject *Sender)
{
	ComInfo->Enabled=true;
 	//ComInfo->Hide();
 	//	MoneyShow();
	Stock->Hide();

	Info();
}
//---------------------------------------------------------------------------
void __fastcall TStock::BitBtn3Click(TObject *Sender)
{
	if  (Stock->Label2->Caption == "Buy")	//Player buying max num of shares
	{
		Player[curplay].Shares[comnum] += maxbuy;
		Player[curplay].Money -= (maxbuy) * (Company[comnum].Value);
	} else {	//Player selling max num of shares
		Player[curplay].Money += (Player[curplay].Shares[comnum]) * (Company[comnum].Value);
		Player[curplay].Shares[comnum] = 0;
	}

	ComInfo->Enabled=true;
 	//	ComInfo->Hide();
  	Stock->Hide();
	MoneyShow();

	Info();
}
//---------------------------------------------------------------------------