//---------------------------------------------------------------------------
#include <vcl\vcl.h>
#pragma hdrstop

#include <stdio.h>
#include <stdlib.h>

#include "HighForm.h"
#include "SMMain.h"
#include "CommaIt.h"
//---------------------------------------------------------------------------
#pragma resource "*.dfm"
THigh *High;
//=============FILE GLOBAL
struct ScoreRecord *Score;


//=============PROTOTYPES
bool LoadScores(void);
void CreateScores(void);
bool SaveScores(void);
unsigned long int Total(int pnum);

//---------------------------------------------------------------------------
__fastcall THigh::THigh(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void HighScores(bool insert)
{

	unsigned long int netwealth;
	Score=new struct ScoreRecord[10];
	bool success = LoadScores();

	High->ListBox1->Clear();
	High->ListBox2->Clear();
	High->ListBox3->Clear();

	if (success == false)	//Highscores have NOT been loaded in
	{
     CreateScores();	//Highscore structure created with default names
     					// and scores
	}

    for (int i=0;i<10;++i)
        Score[i].newentry = false;


    if (insert == true)	//We want to insert some new highscores
    {
        //Find Winner
		int pwon=0; //This stores the number of the player that won the game
        unsigned long int highest=0;	//This is the highest wealth (used to
        							//check for winner)
        for (int i=0; i<numplays; ++i)
        {
           	netwealth=Total(i);
           	if (netwealth>highest)
           	{
               	highest = netwealth;
               	pwon=i;
               }
    	}


    	for (int pnum=0;pnum<numplays;++pnum)
        {
            netwealth = Total(pnum);
        	for (int j=0;j<10;++j)
        	{
            	if (netwealth>Score[j].Wealth)	//Player has a highscore
                {
                	//Shift old highscores down 1
                	for (int i=9;i>j;--i)
                	{
                    	if (i>0)
                    	{
                        strcpy(Score[i].Name,Score[i-1].Name);
                        Score[i].Wealth=Score[i-1].Wealth;
                        Score[i].Won=Score[i-1].Won;
                        Score[i].newentry=Score[i-1].newentry;
                        }
                    }

               		strcpy(Score[j].Name,Player[pnum].Name);
                    Score[j].Wealth=netwealth;
                    Score[j].newentry = true;
                    if (pnum == pwon)
                    	Score[j].Won=true;
                    else Score[j].Won = false;

                    break;
                }//end if netwealth>score...
            }//end 'for j=0...'
        }//end for pnum=0...


    //Put a red block next to highscores which are new entries... Horrid code, eh?
    if (Score[0].newentry) High->Label5->Color=clRed;
    else High->Label5->Color=clBtnFace;
    if (Score[1].newentry) High->Label6->Color=clRed;
    else High->Label6->Color=clBtnFace;
	if (Score[2].newentry) High->Label7->Color=clRed;
   else High->Label7->Color=clBtnFace;
	if (Score[3].newentry) High->Label8->Color=clRed;
  else High->Label8->Color=clBtnFace;
	if (Score[4].newentry) High->Label9->Color=clRed;
   else High->Label9->Color=clBtnFace;
	if (Score[5].newentry) High->Label10->Color=clRed;
   else High->Label10->Color=clBtnFace;
	if (Score[6].newentry) High->Label11->Color=clRed;
else High->Label11->Color=clBtnFace;
	if (Score[7].newentry) High->Label12->Color=clRed;
 else High->Label12->Color=clBtnFace;
	if (Score[8].newentry) High->Label13->Color=clRed;
   else High->Label13->Color=clBtnFace;
	if (Score[9].newentry) High->Label14->Color=clRed;
   else High->Label14->Color=clBtnFace;



    }//end if insert = true...

    char cash[25];
	char string[26];
    for (int pnum=0;pnum<10;++pnum)
    {
		strset(cash,'\0');
        strset(string,'\0');

		High->ListBox1->Items->Add(Score[pnum].Name);
        ltoa(Score[pnum].Wealth,cash,10);
        //==========
 		comma(cash);
        string[0]=currency;
        strcat(string,cash);
        //==========
		High->ListBox2->Items->Add(string);
        if (Score[pnum].Won == true)
        	High->ListBox3->Items->Add("Yes");
        else High->ListBox3->Items->Add("No");
    }
	High->ShowModal();
	SaveScores();
    delete[] Score;
}
//===============================
// This should load scores into the 'Score' structure
// File should be called 'scores' and has structure Name /n
//													Score /n
//													Won? /n (1=true)

bool LoadScores()
{
	FILE *input;
	char buffer[20];
    unsigned char last;	//last character got from input
    int mode;	//mode=0 - Get name
    			//mode=1 - Get score
                //mode=2 - Get winning bool

    int count;

	if ( input = fopen("scores","rb") )
	{
    	for (int pnum=0;pnum<10;++pnum)	// Current player number (0 to 9)
        {
			mode=0;
        	for (unsigned int i=0;i<strlen(Score[pnum].Name);++i)
            	strset(Score[pnum].Name,'\0');
            for (unsigned int i=0;i<strlen(buffer);++i)
            	strset(buffer,'\0');
            count=-1;
        	while (!feof(input))
            {
            	last=fgetc(input);
                ++count;

				if (last == '\n')
        		{
                if (mode == 0) strcpy(Score[pnum].Name,buffer);
                if (mode == 1) Score[pnum].Wealth=atol(buffer);

            		++mode;
                    count=-1;
                	goto skip;
            	}
            	buffer[count]=last;
            	if (mode == 2)
            		if (last == '1')
                		Score[pnum].Won = true;
                	else Score[pnum].Won = false;
            	skip:

            	if (mode == 3) break;
        	}
	   	}
        fclose(input);
    	return true;
	}
	else // Couldn't open scores file
	{
    	return false;
	}

}
//=======================

void CreateScores()
{
	for (int pnum=0;pnum<10;++pnum)
	{
		strset(Score[pnum].Name,'\0');
		strcpy(Score[pnum].Name,"David");
		Score[pnum].Wealth = 1000000;
		Score[pnum].Won = false;
	}

}

//===============
bool SaveScores()
{
	FILE *output;
	char cash[20];

	if ( output = fopen("scores","wb") )
	{
    	for (int pnum=0;pnum<10;++pnum)	// Current player number (0 to 9)
        {

        	for (unsigned int i=0; i<strlen(Score[pnum].Name); ++i)
            	fputc(Score[pnum].Name[i],output);
        	fputc('\n',output);


         	for (unsigned int i=0;i<strlen(cash);++i)
            	strset(cash,'\0');
            ltoa(Score[pnum].Wealth,cash,10);
            for (unsigned int i=0;i<strlen(cash);++i)
            	fputc(cash[i],output);
            fputc('\n',output);

            if (Score[pnum].Won == true) fputc('1',output);
            else fputc('0',output);
			fputc('\n',output);

	   	}
        fclose(output);
    	return true;
	}
	else // Couldn't open scores file
	{
    	return false;
	}



}
//===================
unsigned long int Total(int pnum)
{
	unsigned long int worth = 0;
	for (int i=0; i<5; ++i)
    	worth += Player[pnum].Shares[i] * Company[i].Value;
	return worth+Player[pnum].Money;
}

