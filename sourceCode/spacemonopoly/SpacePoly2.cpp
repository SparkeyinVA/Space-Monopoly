//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop
//---------------------------------------------------------------------------
USEFORM("SMMain.cpp", Main);
USERES("SpacePoly2.res");
USEFORM("Players.cpp", PlayInfo);
USEFORM("ComForm.cpp", NewCom);
USEFORM("ComInfoForm.cpp", ComInfo);
USEFORM("Merger.cpp", MergForm);
USEFORM("SplitForm.cpp", Split);
USEFORM("OverForm.cpp", GameOver);
USEFORM("AboutForm.cpp", About);
USEUNIT("CommaIt.cpp");
USEFORM("playerDetails.cpp", playerDetailsForm);
USEFORM("HighScores.cpp", HighScoresForm);
USEUNIT("crypt.cpp");
USEFORM("Preferences.cpp", preferencesForm);
USEUNIT("CurrencyUnit.cpp");
USEUNIT("Prefs.cpp");
USEUNIT("Grid.cpp");
USEUNIT("GameGrid.cpp");
USEUNIT("Game.cpp");
USEFORM("GridManager.cpp", GridManagerForm);
USEUNIT("large.cpp");
//---------------------------------------------------------------------------
WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int) {
	try	{
		Application->Initialize();
		Application->Title = "Space Monopoly v1.2";
		Application->CreateForm(__classid(TMain), &Main);
        Application->CreateForm(__classid(TPlayInfo), &PlayInfo);
        Application->CreateForm(__classid(TNewCom), &NewCom);
        Application->CreateForm(__classid(TComInfo), &ComInfo);
        Application->CreateForm(__classid(TMergForm), &MergForm);
        Application->CreateForm(__classid(TSplit), &Split);
        Application->CreateForm(__classid(TGameOver), &GameOver);
        Application->CreateForm(__classid(TAbout), &About);
        Application->CreateForm(__classid(THighScoresForm), &HighScoresForm);
        Application->CreateForm(__classid(TplayerDetailsForm), &playerDetailsForm);
        Application->CreateForm(__classid(TpreferencesForm), &preferencesForm);
        Application->CreateForm(__classid(TGridManagerForm), &GridManagerForm);
        Application->Run();
	} catch (Exception &exception) {
		Application->ShowException(&exception);
	}
	return 0;
}
//---------------------------------------------------------------------------
