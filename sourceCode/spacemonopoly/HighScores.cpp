//---------------------------------------------------------------------------
#include <vcl.h>
#pragma hdrstop

#include "HighScores.h"
#include "CurrencyUnit.h"
#include "Game.h"

#include <stdio.h>
#include <stdlib.h>

#include <sys\stat.h>
#include <io.h>

//#include "HighForm.h"
#include "SMMain.h"
#include "CommaIt.h"
#include "crypt.h"

#pragma resource "*.dfm"
//=============FILE GLOBAL
struct ScoreRecord **Score;
//=============PROTOTYPES
bool LoadScores(void);
void CreateScores(void);
bool SaveScores(void);

void encryptScores(void);
void decryptScores(void);

void crypt(int);

THighScoresForm *HighScoresForm;

//---------------------------------------------------------------------------
__fastcall THighScoresForm::THighScoresForm(TComponent* Owner)
	: TForm(Owner)
{
}
//---------------------------------------------------------------------------
void HighScores(bool insert)
{
	CurrencyUnit& currency = Game::getInstance().getPrefs().getCurrencyUnit();

	large netwealth(0);
	Score = new struct ScoreRecord*[3];
    for (int i=0; i<3; i++)
    	Score[i] = new struct ScoreRecord[10];

	bool success = LoadScores();

	if (success == false)	//Highscores have NOT been loaded in
	{
     CreateScores();	//Highscore structure created with default names
     					// and scores
	}

    for (int j=0; j<3; j++)
    	for (int i=0;i<10;++i)
        	Score[j][i].newentry = false;


    if (insert == true)	//We want to insert some new highscores
    {
    	for (int pnum=0; pnum < numplays; ++pnum)
        {
            netwealth = Total(pnum);
        	for (int j=0;j<10;++j)
        	{
            	if (netwealth>Score[gridtype][j].Wealth)	//Player has a highscore
                {
                	//Shift old highscores down 1
                	for (int i=9;i>j;--i)
                	{
                    	if (i>0)
                    	{
                        Score[gridtype][i].Name		= Score[gridtype][i-1].Name;
                        Score[gridtype][i].Wealth	= Score[gridtype][i-1].Wealth;
                        Score[gridtype][i].Events	= Score[gridtype][i-1].Events;
                        Score[gridtype][i].newentry = Score[gridtype][i-1].newentry;
                        }
                    }

               		Score[gridtype][j].Name = Player[pnum].Name;
                    Score[gridtype][j].Wealth=netwealth;
                    Score[gridtype][j].newentry = true;
                    Score[gridtype][j].Events = false; //events were off (as they're not here yet!...)
                    break;
                }//end if netwealth>score...
            }//end 'for j=0...'
        }//end for pnum=0...

    }//end if insert = true...

    HighScoresForm->ListView1->Items->Clear();
    HighScoresForm->ListView2->Items->Clear();
    HighScoresForm->ListView3->Items->Clear();

    char cash[25];
	char string[26];
    for (int gridsize = 0; gridsize<3; gridsize++)
    	for (int pnum=0;pnum<10;++pnum)
    	{
			strset(cash,'\0');
        	strset(string,'\0');

        	//_i64toa(Score[gridsize][pnum].Wealth,cash,10);
            Score[gridsize][pnum].Wealth.output(cash);

	        //==========
 			comma(cash);
    	    //strcpy(string,currency->getUnit().c_str());
        	//strcat(string,cash);
        	//==========
        	char pos[3];
        	itoa(pnum+1, pos, 10);

			switch (gridsize)
        	{
        		case 0:
					HighScoresForm->ListView3->Items->Add();
					HighScoresForm->ListView3->Items->Item[pnum]->Caption = pos;
        			HighScoresForm->ListView3->Items->Item[pnum]->SubItems->Add(Score[0][pnum].Name);
			  		HighScoresForm->ListView3->Items->Item[pnum]->SubItems->Add(currency.output(cash));
        			break;
        		case 1:
        			HighScoresForm->ListView2->Items->Add();
					HighScoresForm->ListView2->Items->Item[pnum]->Caption = pos;
        			HighScoresForm->ListView2->Items->Item[pnum]->SubItems->Add(Score[1][pnum].Name);
                    HighScoresForm->ListView2->Items->Item[pnum]->SubItems->Add(currency.output(cash));
                    break;
            	case 2:
        			HighScoresForm->ListView1->Items->Add();
					HighScoresForm->ListView1->Items->Item[pnum]->Caption = pos;
        			HighScoresForm->ListView1->Items->Item[pnum]->SubItems->Add(Score[2][pnum].Name);
					HighScoresForm->ListView1->Items->Item[pnum]->SubItems->Add(currency.output(cash));
        			break;
        	}
    }

    if (insert)
    	switch(gridtype)
    	{
    		case 0:
        		HighScoresForm->PageControl1->ActivePage=HighScoresForm->TabSheet1;
        		break;
    		case 1:
        		HighScoresForm->PageControl1->ActivePage=HighScoresForm->TabSheet2;
        		break;
    		case 2:
        		HighScoresForm->PageControl1->ActivePage=HighScoresForm->TabSheet3;
        		break;
    	}

    for (int i=0; i<10; i++)
    	switch (gridtype)
        {
        	case 0:
            	HighScoresForm->ListView3->Items->Item[i]->Selected = Score[gridtype][i].newentry;
				break;
            case 1:
            	HighScoresForm->ListView2->Items->Item[i]->Selected = Score[gridtype][i].newentry;
				break;
            case 2:
            	HighScoresForm->ListView1->Items->Item[i]->Selected = Score[gridtype][i].newentry;
				break;
        }

    SaveScores();
	HighScoresForm->ShowModal();

    delete[] Score;
}
//===============================
// This should load scores into the 'Score' structure
// File should be called 'scores' and has structure Name /n
//													Score /n
//													totSum /n crude file correctness check
//													Events? /n (1=true) NOT IMPLEMENTED

bool LoadScores()
{
	if (access("scores", 0) != 0) //scores file doesn't exist
    	return false;

    decryptScores(); //decrypt the file before trying to access it.

	FILE *input;
	char buffer[64];
    char last;	//last character got from input
    int mode;	//mode=0 - Get name
    			//mode=1 - Get score
                //mode=2 - get totSum
                //mode=3 - Get events bool

    int count;
    int totSum, tempSum;

    bool corruptFile = false;

	if ( input = fopen("scores","rb") )
	{
    	for (int gridsize=0; gridsize<3; gridsize++)
			for (int pnum=0; pnum<10; ++pnum)	// Current player number (0 to 9)
        	{
				mode=0;
                tempSum = pnum;
                strset(buffer,'\0');

            	count=-1;

        		while (!feof(input))
            	{
            		last=fgetc(input);
                	++count;

					if (last == '\n')
        			{
                    	switch (mode)
                        {
                        	case 0:
                       			buffer[count] = '\0';
								Score[gridsize][pnum].Name = buffer;
                        		//Score[gridsize][pnum].Name[count+1] = '\0';
                                for (int bufPos = 0; bufPos<count; bufPos++)
                            	   	tempSum +=  buffer[bufPos];

                                break;
                            case 1:
//                				Score[gridsize][pnum].Wealth=_atoi64(buffer);
                  				Score[gridsize][pnum].Wealth=large(buffer);
                                for (int bufPos = 0; bufPos<count; bufPos++)
                                	tempSum +=  buffer[bufPos];
                        		break;
                        	case 2:
                            	totSum = atoi(buffer);
                                if (!corruptFile) corruptFile = (tempSum != totSum);
                                break;
                        }

            			++mode;
                    	count=-1;
                        strset(buffer, '\0');
                		goto skip;
            		}

            		buffer[count]=last;

            		if (mode == 3)
            			if (last == '1')
                			Score[gridsize][pnum].Events = true;
                		else Score[gridsize][pnum].Events = false;

            		skip:

            		if (mode == 4) break;
        		}
	   		}//enf for pnum
        //end for gridsize also

        fclose(input);

    	return !corruptFile; //returns true if the file is not corrupt
	}
	else // Couldn't open scores file
	{
    	return false;
	}

}
//=======================
void CreateScores() {
	for (int gridsize=0; gridsize<3;gridsize++)
		for (int pnum=0;pnum<10;++pnum)
		{
			Score[gridsize][pnum].Name = "David";
			Score[gridsize][pnum].Wealth = large(1000000);
			Score[gridsize][pnum].Events = false;
		}
}

//===============
bool SaveScores() {
	FILE *output;
	char cash[20];

	if ( output = fopen("scores","wb") ) {
    	for (int gridsize=0;gridsize<3;gridsize++) {
    		for (int pnum=0;pnum<10;++pnum)	{ // Current player number (0 to 9)

                int totSum=pnum;

        		for (int i=1; i<=Score[gridsize][pnum].Name.Length(); ++i) {
                	totSum += Score[gridsize][pnum].Name[i];
                	fputc(Score[gridsize][pnum].Name[i],output);
                }
        		fputc('\n',output);

            	strset(cash,'\0');

                Score[gridsize][pnum].Wealth.output(cash);

            	for (unsigned int i=0; i<strlen(cash); ++i) {
                	totSum+=cash[i];
                	fputc(cash[i],output);
                }
            	fputc('\n',output);

                /**
                * totSum = pnum + sum of chars in their name + sum of chars in net wealth
                * the pos of hacking the scores file
                */
                strset(cash, '\0');
                itoa(totSum, cash, 10);
            	for (unsigned int i=0; i<strlen(cash); ++i) {
                	fputc(cash[i],output);
                }
            	fputc('\n',output);

            	if (Score[gridsize][pnum].Events == true) {
                    fputc('1',output);
                } else {
                    fputc('0',output);
                }
				fputc('\n',output);
		   	}

        }

        fclose(output);
 		encryptScores(); //as the file has been saved

    	return true;
	} else { // Couldn't open scores file
    	return false;
	}
}
//===================
large Total(int pnum)
{
	large worth(0);
	for (int i=0; i<5; ++i) {
    	worth += Player[pnum].Shares[i] * Company[i].Value;
    }
	return worth+Player[pnum].Money;
}
//---------------------------------------------------------------------------

void encryptScores(void) {
	crypt(1);
}

void decryptScores(void) {
	crypt(2);
}

void crypt(int mode) {
	AnsiString i = "Space Monopoly";
    for (int j=0; j<20; j++) i += (j*2);

    i += "David1,000,0000";

	char *passbuf = i.c_str();
    int size = 49;

	unsigned char *buffer;
	struct stat statbuf;

    FILE *stream;

	//load file into buffer
	if ( stream = fopen("scores","rb+") )
    {
		fstat(fileno(stream),&statbuf);
		int filesize=statbuf.st_size; //4 = length of magic number
        buffer=new unsigned char[filesize];
        //char z[10];
        //Main->Label5->Caption = itoa(filesize, char z[10] , 10);

        if (!(mode==1)) //we are going to decrypt the data
		{
        	filesize -= 4;
        	char a[4] = {fgetc(stream), fgetc(stream), fgetc(stream), fgetc(stream)};
        	if (!( (a[0]=='s') && (a[1]=='m') && (a[2]=='v') && (a[3]=='1')))
        	{
            	fclose(stream);
        		return;	//The file is not encrypted so don't decrypt
	        }
    	}

        int i=0;
      	buffer[0]=fgetc(stream);

        while ( (i<filesize) && (buffer[i] != NULL) )
            buffer[++i]=fgetc(stream);

        filesize = i;
  		/*//find true filesize...
        for (int i=filesize; i>0; i--)
    	{
			if (buffer[i]==0)
    		{	filesize=i+1;
    			break;
    		}

    	}
  		*/


		if (mode == 1) encrypt(buffer,passbuf,filesize-1,size);
		else decrypt(buffer,passbuf,filesize-1,size);

		//save

		//fclose(stream);
		delete[] passbuf;

		//stream=fopen(Main->FileListBox1->FileName.c_str(),"w");

		// put file pointer back to start of file
		fseek(stream,-ftell(stream),1);

        //output the magic number when we are encrypting
        if (mode == 1)
        {
        	fputc('s',stream); fputc('m',stream); fputc('v',stream); fputc('1',stream);
        }

		for (int i=0; i<(filesize); i++)
			fputc(buffer[i], stream);

		//cleanup
		delete[] buffer;
		fclose(stream);
    }
}

