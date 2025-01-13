//---------------------------------------------------------------------------
#include <vcl.h>
#pragma hdrstop

#include "Preferences.h"
#include "CurrencyUnit.h"
#include "SMMain.h"
#include "Game.h"
//---------------------------------------------------------------------------
#pragma package(smart_init)
#pragma resource "*.dfm"
TpreferencesForm *preferencesForm;
//---------------------------------------------------------------------------
__fastcall TpreferencesForm::TpreferencesForm(TComponent* Owner)
	: TForm(Owner)
{ }
//---------------------------------------------------------------------------
void __fastcall TpreferencesForm::OkayClick(TObject *Sender)
{	closeForm();
}
//---------------------------------------------------------------------------
void TpreferencesForm::closeForm(void)
{	setChanges();//should also save

	Main->Enabled = true;
	preferencesForm->Hide();
}
//---------------------------------------------------------------------------
void TpreferencesForm::setChanges(void)
{	_pref.setMergers(preferencesForm->forceMergers->Checked);
	_pref.setExpansion(preferencesForm->forceExpansion->Checked);
	_pref.setCompanies(preferencesForm->allowCreation->Checked);
	_pref.setDetails(preferencesForm->hideDetails->Checked);

	_pref.setCurrencyUnit(CurrencyUnit(unitSelector->Text, prefix->Checked));

	Game::getInstance().getPrefs() = _pref;
}
//---------------------------------------------------------------------------
void TpreferencesForm::restorePrefs(void)
{	preferencesForm->forceMergers->Checked	 = _pref.mergersSet();
	preferencesForm->forceExpansion->Checked = _pref.expansionSet();
    preferencesForm->allowCreation->Checked	 = _pref.companiesSet();
	preferencesForm->hideDetails->Checked	 = _pref.detailsSet();

	CurrencyUnit cu = _pref.getCurrencyUnit();
    preferencesForm->unitSelector->Text = cu.getUnit();
    preferencesForm->prefix->Checked = cu.isPrefix();
	preferencesForm->postfix->Checked = cu.isPostfix();
}
//---------------------------------------------------------------------------
//Store the current preferences in case we need to restore them.
void __fastcall TpreferencesForm::FormShow(TObject *Sender)
{	_pref = Game::getInstance().getPrefs();
}
//---------------------------------------------------------------------------
void __fastcall TpreferencesForm::CancelClick(TObject *Sender)
{	restorePrefs();
	closeForm();
}
//---------------------------------------------------------------------------
void __fastcall TpreferencesForm::FormClose(TObject *Sender,
      TCloseAction &Action)
{	restorePrefs();
	closeForm();
}
//---------------------------------------------------------------------------
void __fastcall TpreferencesForm::forceExpansionClick(TObject *Sender)
{	allowCreation->Enabled = forceExpansion->Checked;
}
//---------------------------------------------------------------------------
void __fastcall TpreferencesForm::saveButtonClick(TObject *Sender)
{	setChanges();
	_pref.save("Preferences");
    closeForm();
}

