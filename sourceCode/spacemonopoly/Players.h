//---------------------------------------------------------------------------
#ifndef PlayersH
#define PlayersH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
#include <vcl\ComCtrls.hpp>

#include "GameGrid.h"
//---------------------------------------------------------------------------
class TPlayInfo : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel2;
	TBitBtn *BitBtn3;
	TBitBtn *BitBtn4;
	TPageControl *PageControl1;
	TTabSheet *TabSheet1;
	TTabSheet *TabSheet2;
	TBitBtn *BitBtn1;
	TEdit *Edit1;
	TLabel *Label1;
	TListBox *ListBox1;
	TBitBtn *BitBtn2;
	TBitBtn *BitBtn6;
	TBitBtn *BitBtn5;
	TRadioGroup *RadioGroup1;
	void __fastcall BitBtn1Click(TObject *Sender);
	void __fastcall ListBox1Click(TObject *Sender);
	void __fastcall BitBtn2Click(TObject *Sender);
	void __fastcall BitBtn3Click(TObject *Sender);
	void __fastcall BitBtn4Click(TObject *Sender);

	void __fastcall FormClose(TObject *Sender, TCloseAction &Action);

	void __fastcall BitBtn5Click(TObject *Sender);
	void __fastcall BitBtn6Click(TObject *Sender);

private:	// User declarations
public:		// User declarations
	__fastcall TPlayInfo(TComponent* Owner);

    };
//---------------------------------------------------------------------------
extern TPlayInfo *PlayInfo;
extern class GameGrid *gridStore;

void CreateComs(void);
void UpdateButs(void);

//---------------------------------------------------------------------------
#endif
