//---------------------------------------------------------------------------
#ifndef PreferencesH
#define PreferencesH
//---------------------------------------------------------------------------
#include <Classes.hpp>
#include <Controls.hpp>
#include <StdCtrls.hpp>
#include <Forms.hpp>
#include <ComCtrls.hpp>
#include <ExtCtrls.hpp>

#include "Prefs.h"
//---------------------------------------------------------------------------
class TpreferencesForm : public TForm
{
__published:	// IDE-managed Components
	TPanel *Panel1;
	TPageControl *PageControl1;
	TTabSheet *TabSheet1;
	TTabSheet *TabSheet2;
	TButton *Okay;
	TButton *Cancel;
	TGroupBox *GroupBox2;
	TCheckBox *forceMergers;
	TCheckBox *forceExpansion;
	TLabel *Label1;
	TCheckBox *hideDetails;
	TGroupBox *GroupBox1;
	TLabel *Label2;
	TComboBox *unitSelector;
	TRadioButton *prefix;
	TRadioButton *postfix;
	TPanel *Panel2;
	TCheckBox *allowCreation;
	TButton *saveButton;
	void __fastcall OkayClick(TObject *Sender);
	void __fastcall FormShow(TObject *Sender);
	void __fastcall CancelClick(TObject *Sender);

	void __fastcall FormClose(TObject *Sender, TCloseAction &Action);
	void __fastcall forceExpansionClick(TObject *Sender);
	void __fastcall saveButtonClick(TObject *Sender);
private:	// User declarations
	Prefs _pref; //caches the set of preferences before modification
	void restorePrefs(void);
	void setChanges(void);
	void closeForm(void);

public:		// User declarations
	__fastcall TpreferencesForm(TComponent* Owner);
};

//---------------------------------------------------------------------------
extern PACKAGE TpreferencesForm *preferencesForm;
//---------------------------------------------------------------------------
#endif
