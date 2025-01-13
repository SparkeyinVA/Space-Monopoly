//---------------------------------------------------------------------------
#ifndef ComInfoFormH
#define ComInfoFormH
//---------------------------------------------------------------------------
#include <vcl\Classes.hpp>
#include <vcl\Controls.hpp>
#include <vcl\StdCtrls.hpp>
#include <vcl\Forms.hpp>
#include <vcl\ExtCtrls.hpp>
#include <vcl\Buttons.hpp>
#include <ComCtrls.hpp>
#include <ToolWin.hpp>

#include "large.h"
//---------------------------------------------------------------------------
class TComInfo : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel2;
	TGroupBox *GroupBox1;
	TLabel *Label2;
	TLabel *Label3;
	TLabel *Label4;
	TLabel *Label5;
	TLabel *Label6;
	TBevel *Bevel1;
	TLabel *Label8;
	TLabel *Label9;
	TPanel *Panel1;
	TBitBtn *BitBtn4;
	TBitBtn *BitBtn5;
	TEdit *Edit1;
	TBitBtn *BitBtn3;
	TBitBtn *BitBtn2;
	TEdit *Edit2;
	TBevel *Bevel2;
	TBitBtn *BitBtn7;
	TBitBtn *BitBtn8;
	TLabel *Label1;
	TLabel *Label11;
	TLabel *Label13;
	TPanel *Panel3;
	TBitBtn *BitBtn1;
	TToolBar *ToolBar1;
	TToolButton *ToolButton1;
	TToolButton *ToolButton2;
	TToolButton *ToolButton3;
	TToolButton *ToolButton4;
	TToolButton *ToolButton5;
	TSplitter *Splitter1;
	TSplitter *Splitter2;
	TSplitter *Splitter3;
	TSplitter *Splitter4;
	TLabel *Label7;
	void __fastcall BitBtn1Click(TObject *Sender);
	void __fastcall BitBtn4Click(TObject *Sender);
	void __fastcall BitBtn5Click(TObject *Sender);
	void __fastcall BitBtn2Click(TObject *Sender);
	void __fastcall BitBtn3Click(TObject *Sender);
	void __fastcall FormClose(TObject *Sender, TCloseAction &Action);
	
	
	

	void __fastcall Edit1Enter(TObject *Sender);
	void __fastcall Edit2Enter(TObject *Sender);
	void __fastcall BitBtn7Click(TObject *Sender);
	void __fastcall BitBtn8Click(TObject *Sender);
	void __fastcall ToolButton1Click(TObject *Sender);
	void __fastcall ToolButton2Click(TObject *Sender);
	void __fastcall ToolButton3Click(TObject *Sender);
	void __fastcall ToolButton4Click(TObject *Sender);
	void __fastcall ToolButton5Click(TObject *Sender);
private:	// User declarations
public:		// User declarations
	__fastcall TComInfo(TComponent* Owner);
};
//---------------------------------------------------------------------------

void Info(int com);
extern int comnum;
extern large maxbuy;

extern TComInfo *ComInfo;
//---------------------------------------------------------------------------
#endif
